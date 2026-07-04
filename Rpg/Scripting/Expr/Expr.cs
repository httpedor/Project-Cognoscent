using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;
public abstract class BaseExpr : ISerializable
{
    public static BaseExpr Deserialize(Stream stream)
    {
        var typeName = stream.ReadString();
        var type = Type.GetType("Rpg.Scripting." + typeName);
        if (type == null)
            throw new Exception("Failed to get expression type: " + typeName);

        // If the resolved type is an open generic (e.g. EnumExpr`1), read the
        // generic type arguments that were written by ToBytes and close the type.
        if (type.IsGenericTypeDefinition)
        {
            var genericParams = type.GetGenericArguments();
            var typeArgs = new Type[genericParams.Length];
            for (int i = 0; i < genericParams.Length; i++)
            {
                var argTypeName = stream.ReadString();
                typeArgs[i] = Type.GetType(argTypeName)
                    ?? throw new Exception($"Failed to resolve generic type argument: {argTypeName}");
            }
            type = type.MakeGenericType(typeArgs);
        }

        if (!type.IsSubclassOf(typeof(BaseExpr)))
            throw new Exception("Type is not an expression: " + typeName);
        if (type.GetConstructor(new[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get expression constructor: " + typeName);

        return (BaseExpr)Activator.CreateInstance(type, stream)!;
    }
    public static T Deserialize<T>(Stream stream) where T : BaseExpr
    {
        var expr = Deserialize(stream);
        if (expr is T tExpr)
            return tExpr;
        throw new Exception($"Failed to deserialize expression. Expected {typeof(T).Name}, got {expr.GetType().Name}");
    }

    public virtual void ToBytes(Stream stream)
    {
        //No need for full name since all expressions are in the same namespace
        var type = GetType();
        stream.WriteString(type.Name);

        // For generic types (e.g. EnumExpr<StatModifierType>), also write
        // the assembly-qualified names of the type arguments so Deserialize
        // can reconstruct the closed generic type.
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                stream.WriteString(arg.AssemblyQualifiedName!);
            }
        }
    }

    public virtual object? BaseEval(EvalContext ctx) => throw new NotImplementedException("Eval not implemented for " + GetType().Name);
}
public abstract class Expr<T> : BaseExpr
{
    public abstract T Eval(EvalContext ctx);

    public T Eval(Entity? target, Entity? source, Component? targetComponent = null)
    {
        return Eval(new EvalContext()
        {
            Target = target,
            Caller = source,
            TargetComponent = targetComponent,
            Board = target?.Board
        });
    }
    public override object? BaseEval(EvalContext ctx)
    {
        return Eval(ctx);
    }
    public T Eval(Entity target)
    {
        return Eval(target, target, null);
    }
    public T Eval()
    {
        return Eval(null, null, null);
    }
}
public class CastExpr<T> : Expr<T>
{
    public readonly BaseExpr Source;

    private static readonly ConcurrentDictionary<(Type Source, Type Target), MethodInfo?> _userDefinedConversionCache
        = new ConcurrentDictionary<(Type Source, Type Target), MethodInfo?>();

    public CastExpr(BaseExpr source)
    {
        Source = source;
    }

    public CastExpr(Stream stream)
    {
        Source = BaseExpr.Deserialize(stream);
    }

    private static bool TryUserDefinedOperatorConvert(object? value, Type targetType, out object? result)
    {
        result = null;
        if (value == null)
            return false;

        var sourceType = value.GetType();
        var key = (Source: sourceType, Target: targetType);

        var method = _userDefinedConversionCache.GetOrAdd(key, k =>
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            return sourceType
                .GetMethods(flags)
                .Concat(targetType.GetMethods(flags))
                .FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit")
                                      && m.ReturnType == targetType
                                      && m.GetParameters().Length == 1
                                      && m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType));
        });

        if (method == null)
            return false;

        result = method.Invoke(null, new object?[] { value });
        return true;
    }

    public static T CastOp(object? value, EvalContext ctx)
    {
        while (value is BaseExpr expr)
        {
            value = expr.BaseEval(ctx);
        }

        if (value == null)
        {
            if (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null)
                return default!;
            throw new InvalidCastException($"Cannot cast null to {typeof(T).Name}");
        }

        if (value is T tValue)
            return tValue;

        if (typeof(Component).IsAssignableFrom(typeof(T)) && value is Entity entity)
        {
            var comp = entity.GetComponent(Component.GetComponentId(typeof(T)));
            if (comp is T tComp)
                return tComp;
        }
        if (typeof(Entity).IsAssignableFrom(typeof(T)) && value is Component component)
        {
            var ent = component.Entity;
            if (ent is T tEnt)
                return tEnt;
        }

        if (TryUserDefinedOperatorConvert(value, typeof(T), out var customConverted)
            && customConverted is T tCustom)
        {
            return tCustom;
        }

        try
        {
            var converted = Convert.ChangeType(value, typeof(T));
            if (converted is T tConverted)
                return tConverted;
        }
        catch
        {
            // fall through to exception
        }

        throw new InvalidCastException($"Cannot cast value of type {value.GetType().Name} to {typeof(T).Name}");
    }

    public override T Eval(EvalContext ctx)
    {
        var value = Source.BaseEval(ctx);
        return CastOp(value, ctx);
    }
}
public class WithVariablesExpr<T> : Expr<T>
{
    public readonly ArrayExpr<object> Variables;
    public readonly Expr<T> InnerExpr;

    public WithVariablesExpr(ArrayExpr<object> variables, Expr<T> innerExpr)
    {
        Variables = variables;
        InnerExpr = innerExpr;
    }
    public WithVariablesExpr(Stream stream)
    {
        Variables = BaseExpr.Deserialize<ArrayExpr<object>>(stream);
        InnerExpr = BaseExpr.Deserialize<Expr<T>>(stream);
    }

    public override T Eval(EvalContext ctx)
    {
        var variables = Variables.Eval(ctx).ToArray();
        var newVariables = new object[variables.Length + ctx.Variables.Length];
        Array.Copy(variables, newVariables, variables.Length);
        Array.Copy(ctx.Variables, 0, newVariables, variables.Length, ctx.Variables.Length);
        var newCtx = ctx.WithVariables(newVariables);
        return InnerExpr.Eval(newCtx);
    }
}
public class EscapeExpr : Expr<BaseExpr>
{
    public BaseExpr InnerExpr;
    public EscapeExpr(BaseExpr innerExpr)
    {
        InnerExpr = innerExpr;
    }
    public EscapeExpr(Stream stream)
    {
        InnerExpr = BaseExpr.Deserialize(stream);
    }
    public override BaseExpr Eval(EvalContext ctx)
    {
        return InnerExpr;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        InnerExpr.ToBytes(stream);
    }
}
public class VarExpr<T> : Expr<T>
{
    public readonly int VariableID;

    public VarExpr(int variableID)
    {
        VariableID = variableID;
    }
    public VarExpr(Stream stream)
    {
        VariableID = stream.ReadInt32();
    }

    public override T Eval(EvalContext ctx)
    {
        object? value = ctx.Variables[VariableID];
        
        return CastExpr<T>.CastOp(value, ctx);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(VariableID);
    }
}
public sealed class RunExprFromLibraryExpr : BaseExpr
{
    public readonly string LibraryId;
    public readonly string ExprName;
    public readonly ArrayExpr<object>? Args;
    public RunExprFromLibraryExpr(string libraryId, string exprName, ArrayExpr<object>? args = null)
    {
        LibraryId = libraryId;
        ExprName = exprName;
        Args = args;
    }
    public RunExprFromLibraryExpr(Stream stream)
    {
        LibraryId = stream.ReadString();
        ExprName = stream.ReadString();
        if (stream.ReadBoolean())
            Args = BaseExpr.Deserialize<ArrayExpr<object>>(stream);
    }

    public override object? BaseEval(EvalContext ctx)
    {
        if (Args == null)
            return ExprLibrary.ExecuteFromLibrary(LibraryId, ExprName, ctx);

        var argValues = Args.Eval(ctx);
        var newCtx = ctx.WithVariables(argValues);
        return ExprLibrary.ExecuteFromLibrary(LibraryId, ExprName, newCtx);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(LibraryId);
        stream.WriteString(ExprName);
        stream.WriteBoolean(Args != null);
        if (Args != null)
            Args.ToBytes(stream);
    }
}
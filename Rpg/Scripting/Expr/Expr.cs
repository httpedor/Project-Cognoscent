using Rpg.Entities;

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
}
public abstract class Expr<T> : BaseExpr
{
    public abstract T Eval(EvalContext ctx);

    public T Eval(Entity? target, Entity? source, Entity? bpTarget = null)
    {
        return Eval(new EvalContext()
        {
            Target = target,
            Caller = source,
            TargetPart = bpTarget,
            Board = target?.Board
        });
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
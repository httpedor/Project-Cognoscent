using System.Collections;
using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;

public abstract class ArrayExpr<T> : Expr<IEnumerable<T>>
{
    public override object? BaseEval(EvalContext ctx)
    {
        return Eval(ctx);
    }

    public static implicit operator ArrayExpr<T>(Expr<T>[] array)
    {
        return new ConstArrayExpr<T>(array);
    }

    public ArrayExpr<U> Cast<U>()
    {
        if (typeof(T).IsAssignableTo(typeof(U)))
        {
            return new CastArrayExpr<U>(this);
        }
        throw new InvalidCastException($"Cannot cast array of {typeof(T).Name} to array of {typeof(U).Name}");
    }
}

public class ConstArrayExpr<T> : ArrayExpr<T>
{
    public Expr<T>[] Values { get; set; }

    public ConstArrayExpr(params Expr<T>[] values)
    {
        Values = values;
    }
    public ConstArrayExpr(IEnumerable<Expr<T>> values)
    {
        Values = values.ToArray();
    }
    public ConstArrayExpr(Stream stream)
    {
        var count = stream.ReadInt32();
        Values = new Expr<T>[count];
        for (int i = 0; i < count; i++)
        {
            Values[i] = BaseExpr.Deserialize<Expr<T>>(stream);
        }
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        return Values.Select(v => v.Eval(ctx));
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Values.Length);
        foreach (var val in Values)
        {
            stream.WriteObject(val);
        }
    }
}

public class CastArrayExpr<T> : ArrayExpr<T>
{
    public readonly BaseExpr Source;

    public CastArrayExpr(BaseExpr source)
    {
        Source = source;
        var type = Source.GetType();
        while (type != null)
        {
            if (type.Name.StartsWith("ArrayExpr") || type is IEnumerable)
                return;
            type = type.BaseType;
        }
        throw new InvalidOperationException($"Source expression of type {Source.GetType().Name} is not an array expression");
    }
    public CastArrayExpr(Stream stream)
    {
        Source = BaseExpr.Deserialize(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var result = Source.BaseEval(ctx);
        if (result is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is T tItem)
                    yield return tItem;
                else
                    throw new InvalidCastException($"Cannot cast item of type {item?.GetType().Name ?? "null"} to {typeof(T).Name}");
            }
        }
        else
        {
            throw new InvalidCastException($"Cannot cast value of type {result?.GetType().Name ?? "null"} to IEnumerable");
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
    }
}

public class MapExpr<T> : Expr<T>
{
    public readonly BaseExpr Source;
    public readonly Expr<T> ResultExpr;
    public MapExpr(BaseExpr source, Expr<T> resultExpr)
    {
        Source = source;
        ResultExpr = resultExpr;
        var type = Source.GetType();
        while (type != null)
        {
            if (type.Name.StartsWith("ArrayExpr") || type is IEnumerable)
                return;
            type = type.BaseType;
        }
        throw new InvalidOperationException($"Source expression of type {Source.GetType().Name} is not an array expression");
    }
    public MapExpr(Stream stream)
    {
        Source = BaseExpr.Deserialize(stream);
        ResultExpr = BaseExpr.Deserialize<Expr<T>>(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        ResultExpr.ToBytes(stream);
    }

    public override T Eval(EvalContext ctx)
    {
        object? result = Source.BaseEval(ctx);
        while (result is BaseExpr expr)
            result = expr.BaseEval(ctx);

        var newVariables = new object[ctx.Variables.Length + 1];
        for (int i = 0; i < ctx.Variables.Length; i++)
        {
            newVariables[i + 1] = ctx.Variables[i];
        }
        var newCtx = ctx.WithVariables(newVariables);
        var results = new List<T>();
        if (result is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                newVariables[0] = item;
                results.Add(ResultExpr.Eval(newCtx));
            }
        }
        else
        {
            results.Add(ResultExpr.Eval(newCtx));
        }
        if (results.Count == 1)
            return results[0];
        return (T)(object)results;
    }

}
public class MapManyExpr<T> : ArrayExpr<T>
{
    public readonly BaseExpr Source;
    public readonly ArrayExpr<T> ResultExpr;
    public MapManyExpr(BaseExpr source, ArrayExpr<T> resultExpr)
    {
        Source = source;
        ResultExpr = resultExpr;
        var type = Source.GetType();
        while (type != null)
        {
            if (type.Name.StartsWith("ArrayExpr"))
                return;
            type = type.BaseType;
        }
        throw new InvalidOperationException($"Source expression of type {Source.GetType().Name} is not an array expression");
    }
    public MapManyExpr(Stream stream)
    {
        Source = BaseExpr.Deserialize(stream);
        ResultExpr = BaseExpr.Deserialize<ArrayExpr<T>>(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        ResultExpr.ToBytes(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var result = Source.BaseEval(ctx);
        while (result is BaseExpr expr)
            result = expr.BaseEval(ctx);
        var newVariables = new object[ctx.Variables.Length + 1];
        for (int i = 0; i < ctx.Variables.Length; i++)
        {
            newVariables[i + 1] = ctx.Variables[i];
        }
        var newCtx = ctx.WithVariables(newVariables);
        Stack<IEnumerable> stack = new Stack<IEnumerable>();
        if (result is IEnumerable enumerable)
            stack.Push(enumerable);
        else
            throw new InvalidCastException($"Cannot cast value of type {result?.GetType().Name ?? "null"} to IEnumerable");
        
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var item in current)
            {
                if (item is IEnumerable inner)
                    stack.Push(inner);
                else
                {
                    newVariables[0] = item;
                    foreach (var mapped in ResultExpr.Eval(newCtx))
                    {
                        yield return mapped;
                    }
                }
            }
        }
    }
}
public class ForEachEffectExpr : EffectExpr
{
    public readonly EffectExpr Effect;
    public readonly ArrayExpr<object> Values;
    public ForEachEffectExpr(ArrayExpr<object> values, EffectExpr effect)
    {
        Values = values;
        Effect = effect;
    }
    public ForEachEffectExpr(Stream stream)
    {
        Effect = (EffectExpr)BaseExpr.Deserialize(stream);
        Values = (ArrayExpr<object>)BaseExpr.Deserialize(stream);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        var newVariables = new object[ctx.Variables.Length + 1];
        for (int i = 0; i < ctx.Variables.Length; i++)
            newVariables[i+1] = ctx.Variables[i];
        foreach (var value in Values.Eval(ctx))
        {
            if (value == null)
                continue;
            newVariables[0] = value;
            Effect.Eval(ctx.WithVariables(newVariables));
        }
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Effect.ToBytes(stream);
        Values.ToBytes(stream);
    }

    [ExprOp(ExprCategory.Effect, "foreach")]
    [ExprParam("values", typeof(List<object>), Required = true, Description = "The values to iterate over")]
    [ExprParam("effect", typeof(EffectExpr), Required = true, Description = "The effect to apply for each value. The current value will be available as variable 0 in each effect context.")]
    public static ForEachEffectExpr ForEachEffect(JsonElement json)
    {
        return new ForEachEffectExpr(ExpressionCompiler.CompileArray<object>(json.GetProperty("values")), ExpressionCompiler.CompileEffect(json.GetProperty("effects")));
    }
}

public sealed class ArrayAppendExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T>[] Arrays;

    public ArrayAppendExpr(params ArrayExpr<T>[] arrays)
    {
        Arrays = arrays;
    }
    public ArrayAppendExpr(Stream stream)
    {
        Arrays = new ArrayExpr<T>[stream.ReadInt32()];
        for (int i = 0; i < Arrays.Length; i++)
        {
            Arrays[i] = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        }
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        foreach (var array in Arrays)
        {
            foreach (var item in array.Eval(ctx))
            {
                yield return item;
            }
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Arrays.Length);
        foreach (var array in Arrays)
            array.ToBytes(stream);
    }
}
public sealed class ArrayWithVarsExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Array;
    public readonly ArrayExpr<object> Variables;
    public ArrayWithVarsExpr(ArrayExpr<T> array, ArrayExpr<object> variables)
    {
        Array = array;
        Variables = variables;
    }

    public ArrayWithVarsExpr(Stream stream)
    {
        Array = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        Variables = (ArrayExpr<object>)BaseExpr.Deserialize(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Array.ToBytes(stream);
        Variables.ToBytes(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var newVariables = new object[ctx.Variables.Length + 1];
        for (int i = 0; i < ctx.Variables.Length; i++)
            newVariables[i + 1] = ctx.Variables[i];
        var newCtx = ctx.WithVariables(newVariables);
        int index = 0;
        var varsEnum = Variables.Eval(ctx);
        foreach (var variable in varsEnum)
        {
            newVariables[index] = variable;
            index++;
            if (index >= newVariables.Length)
                break;
        }
        var arrayVals = Array.Eval(newCtx);
        foreach (var item in arrayVals)
        {
            yield return item;
        }
    }
}
public sealed class VarArrayExpr<T> : ArrayExpr<T>
{
    public readonly int VariableID;

    public VarArrayExpr(int variableID)
    {
        VariableID = variableID;
    }
    public VarArrayExpr(Stream stream)
    {
        VariableID = stream.ReadInt32();
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        object? value = ctx.Variables[VariableID];
        while (value is BaseExpr expr)
            value = expr.BaseEval(ctx);

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return CastExpr<T>.CastOp(item, ctx);
            }
        }
        else
        {
            yield return CastExpr<T>.CastOp(value, ctx);
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(VariableID);
    }
}
public sealed class SubArrayExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Source;
    public readonly Expr<float> Start;
    public readonly Expr<float> Count;

    public SubArrayExpr(ArrayExpr<T> source, Expr<float> start, Expr<float> count)
    {
        Source = source;
        Start = start;
        Count = count;
    }
    public SubArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        Start = BaseExpr.Deserialize<Expr<float>>(stream);
        Count = BaseExpr.Deserialize<Expr<float>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        return Source.Eval(ctx).Skip((int)Start.Eval(ctx)).Take((int)Count.Eval(ctx));
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        Start.ToBytes(stream);
        Count.ToBytes(stream);
    }
}
public sealed class FilterArrayExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Source;
    public readonly Expr<bool> Condition;

    public FilterArrayExpr(ArrayExpr<T> source, Expr<bool> condition)
    {
        Source = source;
        Condition = condition;
    }
    public FilterArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        Condition = BaseExpr.Deserialize<Expr<bool>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var newCtx = ctx.WithShiftedVariables(1);
        foreach (var item in Source.Eval(ctx))
        {
            newCtx.Variables[0] = item!;
            if (Condition.Eval(newCtx))
                yield return item;
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        Condition.ToBytes(stream);
    }
}
public sealed class OrderArrayExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Source;
    public readonly Expr<bool> Comparison;

    public OrderArrayExpr(ArrayExpr<T> source, Expr<bool> comparison)
    {
        Source = source;
        Comparison = comparison;
    }
    public OrderArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        Comparison = BaseExpr.Deserialize<Expr<bool>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var list = Source.Eval(ctx).ToList();
        list.Sort((a, b) =>
        {
            var newCtx = ctx.WithShiftedVariables(2);
            newCtx.Variables[0] = a!;
            newCtx.Variables[1] = b!;
            var result = Comparison.Eval(newCtx);
            return result ? -1 : 1;
        });
        return list;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        Comparison.ToBytes(stream);
    }
}
public sealed class MaxElementsArrayExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Source;
    public readonly Expr<float> ValueExpr;
    public readonly Expr<float> CountExpr;

    public MaxElementsArrayExpr(ArrayExpr<T> source, Expr<float> valueExpr, Expr<float> countExpr)
    {
        Source = source;
        ValueExpr = valueExpr;
        CountExpr = countExpr;

    }
    public MaxElementsArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        ValueExpr = BaseExpr.Deserialize<Expr<float>>(stream);
        CountExpr = BaseExpr.Deserialize<Expr<float>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var sourceVals = Source.Eval(ctx);
        var value = ValueExpr.Eval(ctx);
        var count = (int)CountExpr.Eval(ctx);
        return sourceVals.Select(item =>
        {
            var newCtx = ctx.WithShiftedVariables(1);
            newCtx.Variables[0] = item!;
            return (Item: item, Value: ValueExpr.Eval(newCtx));
        }).Where(x => x.Value <= value).OrderBy(x => x.Value).Take(count).Select(x => x.Item);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        ValueExpr.ToBytes(stream);
        CountExpr.ToBytes(stream);
    }
}
public sealed class MinElementsArrayExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Source;
    public readonly Expr<float> ValueExpr;
    public readonly Expr<float> CountExpr;

    public MinElementsArrayExpr(ArrayExpr<T> source, Expr<float> valueExpr, Expr<float> countExpr)
    {
        Source = source;
        ValueExpr = valueExpr;
        CountExpr = countExpr;

    }
    public MinElementsArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        ValueExpr = BaseExpr.Deserialize<Expr<float>>(stream);
        CountExpr = BaseExpr.Deserialize<Expr<float>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var sourceVals = Source.Eval(ctx);
        var value = ValueExpr.Eval(ctx);
        var count = (int)CountExpr.Eval(ctx);
        return sourceVals.Select(item =>
        {
            var newCtx = ctx.WithShiftedVariables(1);
            newCtx.Variables[0] = item!;
            return (Item: item, Value: ValueExpr.Eval(newCtx));
        }).Where(x => x.Value >= value).OrderByDescending(x => x.Value).Take(count).Select(x => x.Item);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        ValueExpr.ToBytes(stream);
        CountExpr.ToBytes(stream);
    }
}
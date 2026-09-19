using System.Collections;
using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;

/// <summary>
/// Shared helpers for expressions that consume another expression's sequence output.
/// </summary>
internal static class SequenceExpr
{
    /// <summary>
    /// True if <paramref name="expr"/> statically produces a sequence — either it derives from
    /// <see cref="ArrayExpr{T}"/>, or it is an <c>Expr&lt;T&gt;</c> whose T is enumerable (and not
    /// a string, which is enumerable but scalar as far as the language is concerned).
    /// </summary>
    public static bool ProducesSequence(BaseExpr expr)
    {
        for (var type = expr.GetType(); type != null; type = type.BaseType)
        {
            if (!type.IsGenericType) continue;
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ArrayExpr<>))
                return true;
            if (definition == typeof(Expr<>))
            {
                var produced = type.GetGenericArguments()[0];
                if (produced != typeof(string) && typeof(IEnumerable).IsAssignableFrom(produced))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Throws if <paramref name="source"/> is not a sequence-producing expression.</summary>
    public static BaseExpr RequireSequence(BaseExpr source, string consumer)
        => ProducesSequence(source)
            ? source
            : throw new InvalidOperationException(
                $"{consumer} requires an array source, but {source.GetType().Name} produces a single value.");
}

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

/// <summary>
/// Lets an array literal's items be read back without knowing their element type — what
/// <c>concat</c> needs to unpack <c>[a, b]</c> into the parts it appends.
/// </summary>
public interface IConstArrayExpr
{
    IReadOnlyList<BaseExpr> Items { get; }
}

public class ConstArrayExpr<T> : ArrayExpr<T>, IConstArrayExpr
{
    public Expr<T>[] Values { get; set; }

    IReadOnlyList<BaseExpr> IConstArrayExpr.Items => Values;

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
        Source = SequenceExpr.RequireSequence(source, "CastArrayExpr");
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

/// <summary>
/// Maps every element of a source array through a per-element expression, producing a new array.
/// The current element is exposed as variable 0 (existing variables shift up by one), matching
/// <see cref="FilterArrayExpr{T}"/>. The result is one scalar per element (not flattened), so it is
/// usable as the source of e.g. <c>sum</c>.
/// </summary>
public sealed class MapArrayExpr<T> : ArrayExpr<T>
{
    public readonly BaseExpr Source;
    public readonly LambdaExpr<T> ResultExpr;

    public MapArrayExpr(BaseExpr source, LambdaExpr<T> resultExpr)
    {
        Source = source;
        ResultExpr = resultExpr;
    }
    public MapArrayExpr(Stream stream)
    {
        Source = BaseExpr.Deserialize(stream);
        ResultExpr = BaseExpr.Deserialize<LambdaExpr<T>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        object? result = Source.BaseEval(ctx);
        while (result is BaseExpr expr)
            result = expr.BaseEval(ctx);
        if (result is not IEnumerable enumerable)
            throw new InvalidCastException($"Cannot map over non-enumerable value of type {result?.GetType().Name ?? "null"}");

        foreach (var item in enumerable)
            yield return ResultExpr.Invoke(ctx, item);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        ResultExpr.ToBytes(stream);
    }
}
public class ForEachEffectExpr : EffectExpr
{
    public readonly LambdaExpr<NoReturn> Effect;
    public readonly ArrayExpr<object> Values;
    public ForEachEffectExpr(ArrayExpr<object> values, LambdaExpr<NoReturn> effect)
    {
        Values = values;
        Effect = effect;
    }
    public ForEachEffectExpr(Stream stream)
    {
        Effect = BaseExpr.Deserialize<LambdaExpr<NoReturn>>(stream);
        Values = (ArrayExpr<object>)BaseExpr.Deserialize(stream);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        foreach (var value in Values.Eval(ctx))
        {
            if (value == null)
                continue;
            Effect.Invoke(ctx, value);
        }
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Effect.ToBytes(stream);
        Values.ToBytes(stream);
    }

    // The parameter was documented as "effect" but read as "effects"; the name that actually
    // worked is the one kept here.
    [ExprOp("foreach", Description = "Runs an effect once per element of an array.")]
    public static ForEachEffectExpr Op(
        [Doc("Values to iterate over")] ArrayExpr<object> values,
        [Doc("Effect to run per value; the current value is its parameter")] LambdaExpr<NoReturn> effects)
        => new ForEachEffectExpr(values, effects);
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
public sealed class VarArrayExpr<T> : ArrayExpr<T>, Types.IVariableReference
{
    public readonly int VariableID;

    int Types.IVariableReference.ReferencedVariable => VariableID;

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
        object? value = ctx.GetVariable(VariableID);
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
    public readonly LambdaExpr<bool> Condition;

    public FilterArrayExpr(ArrayExpr<T> source, LambdaExpr<bool> condition)
    {
        Source = source;
        Condition = condition;
    }
    public FilterArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        Condition = BaseExpr.Deserialize<LambdaExpr<bool>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        foreach (var item in Source.Eval(ctx))
        {
            if (Condition.Invoke(ctx, item))
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
    public readonly LambdaExpr<bool> Comparison;

    public OrderArrayExpr(ArrayExpr<T> source, LambdaExpr<bool> comparison)
    {
        Source = source;
        Comparison = comparison;
    }
    public OrderArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        Comparison = BaseExpr.Deserialize<LambdaExpr<bool>>(stream);
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        var list = Source.Eval(ctx).ToList();
        list.Sort((a, b) => Comparison.Invoke(ctx, a, b) ? -1 : 1);
        return list;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        Comparison.ToBytes(stream);
    }
}
public sealed class RankedElementsArrayExpr<T> : ArrayExpr<T>
{
    public readonly ArrayExpr<T> Source;
    public readonly LambdaExpr<float> ScoreExpr;
    public readonly Expr<float> CountExpr;
    /// <summary>True for <c>top</c>/<c>max</c> (highest scores first), false for <c>bottom</c>/<c>min</c>.</summary>
    public readonly bool Descending;

    public RankedElementsArrayExpr(ArrayExpr<T> source, LambdaExpr<float> scoreExpr, Expr<float> countExpr, bool descending)
    {
        Source = source;
        ScoreExpr = scoreExpr;
        CountExpr = countExpr;
        Descending = descending;
    }
    public RankedElementsArrayExpr(Stream stream)
    {
        Source = (ArrayExpr<T>)BaseExpr.Deserialize(stream);
        ScoreExpr = BaseExpr.Deserialize<LambdaExpr<float>>(stream);
        CountExpr = BaseExpr.Deserialize<Expr<float>>(stream);
        Descending = stream.ReadBoolean();
    }

    public override IEnumerable<T> Eval(EvalContext ctx)
    {
        // The score is a per-element expression: the element is bound as variable 0 before each
        // evaluation. Evaluating it once against the outer context (as this used to) read whatever
        // happened to occupy slot 0 and treated the result as a threshold, which was never intended.
        var scored = Source.Eval(ctx)
            .Select(item => (Item: item, Score: ScoreExpr.Invoke(ctx, item)));

        var ordered = Descending
            ? scored.OrderByDescending(x => x.Score)
            : scored.OrderBy(x => x.Score);

        return ordered.Take(ClampCount(CountExpr.Eval(ctx))).Select(x => x.Item);
    }

    /// <summary>
    /// Converts the count expression to a usable element count. The "no limit" default is
    /// <see cref="float.MaxValue"/>, and casting that straight to int overflows to a negative
    /// number, which would make Take() yield nothing.
    /// </summary>
    private static int ClampCount(float count)
        => count >= int.MaxValue ? int.MaxValue
         : count <= 0 ? 0
         : (int)count;

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Source.ToBytes(stream);
        ScoreExpr.ToBytes(stream);
        CountExpr.ToBytes(stream);
        stream.WriteBoolean(Descending);
    }
}
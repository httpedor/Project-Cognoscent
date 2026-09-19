using System.Text.Json;

namespace Rpg.Scripting;

public sealed class ConstNumberExpr : Expr<float>
{
    public readonly float Value;
    public ConstNumberExpr(float value) => Value = value;
    public ConstNumberExpr(Stream stream)
    {
        Value = stream.ReadFloat();
    }
    public override float Eval(EvalContext ctx) => Value;
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteFloat(Value);
    }

    [ExprOp("max_value", "max_float", "float_max_value", Description = "The largest representable number.")]
    public static Expr<float> MaxValue() => new ConstNumberExpr(float.MaxValue);

    [ExprOp("min_value", "min_float", "float_min_value", Description = "The smallest representable number.")]
    public static Expr<float> MinValue() => new ConstNumberExpr(float.MinValue);
}

public sealed class RangeNumberExpr : ArrayExpr<float>
{
    public readonly Expr<float> End;
    public readonly Expr<float> Start;
    public readonly Expr<float> Step;

    public RangeNumberExpr(Expr<float> start, Expr<float> end, Expr<float> step)
    {
        Start = start;
        End = end;
        Step = step;
    }
    public RangeNumberExpr(Stream stream)
    {
        Start = BaseExpr.Deserialize<Expr<float>>(stream);
        End = BaseExpr.Deserialize<Expr<float>>(stream);
        Step = BaseExpr.Deserialize<Expr<float>>(stream);
    }

    public override IEnumerable<float> Eval(EvalContext ctx)
    {
        float start = Start.Eval(ctx);
        float end = End.Eval(ctx);
        float step = Step.Eval(ctx);
        if (step == 0f)
            throw new Exception("Range step cannot be zero.");
        if (step > 0f)
        {
            for (float value = start; value < end; value += step)
                yield return value;
        }
        else
        {
            for (float value = start; value > end; value += step)
                yield return value;
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Start.ToBytes(stream);
        End.ToBytes(stream);
        Step.ToBytes(stream);
    }

    [ExprOp("range", "rangeNumber", "numberRange",
            Description = "The numbers from start (inclusive) to end (exclusive), spaced by step.")]
    public static RangeNumberExpr Op(
        [Doc("First value")] Expr<float> start,
        [Doc("Exclusive upper bound")] Expr<float> end,
        [Doc("Increment between values; may be negative")] Expr<float> step)
        => new RangeNumberExpr(start, end, step);
}
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

    [ExprOp(ExprCategory.Number, "max_value", "max_float", "float_max_value")]
    public static Expr<float> CompileMaxFloat(JsonElement obj)
    {
        return new ConstNumberExpr(float.MaxValue);
    }
    [ExprOp(ExprCategory.Number, "min_value", "min_float", "float_min_value")]
    public static Expr<float> CompileMinFloat(JsonElement obj)
    {
        return new ConstNumberExpr(float.MinValue);
    }
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

    [ExprOp(ExprCategory.Number, "range", "rangeNumber", "numberRange", IsArray = true)]
    [ExprParam("start", typeof(float), Required = true)]
    [ExprParam("end", typeof(float), Required = true)]
    [ExprParam("step", typeof(float), Required = true)]
    public static RangeNumberExpr Compile(JsonElement obj)
    {
        var start = ExpressionCompiler.Compile<float>(obj.GetProperty("start"));
        var end = ExpressionCompiler.Compile<float>(obj.GetProperty("end"));
        var step = ExpressionCompiler.Compile<float>(obj.GetProperty("step"));
        return new RangeNumberExpr(start, end, step);
    }
}
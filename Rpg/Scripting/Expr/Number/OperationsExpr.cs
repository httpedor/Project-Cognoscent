using System.Text.Json;

namespace Rpg.Scripting;

public sealed class LerpExpr : Expr<float>
{
    public readonly Expr<float> Min;
    public readonly Expr<float> Max;
    public readonly Expr<float> T;

    public LerpExpr(Expr<float> min, Expr<float> max, Expr<float> t)
    {
        Min = min;
        Max = max;
        T = t;
    }

    [ExprOp(ExprCategory.Number, "lerp")]
    [ExprParam("min", typeof(float), Required = true)]
    [ExprParam("max", typeof(float), Required = true)]
    [ExprParam("t", typeof(float), Required = true, Description = "Interpolation factor (0-1)")]
    public static Expr<float> CompileOp(JsonElement obj)
        => new LerpExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("min")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("max")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("t")));
    public LerpExpr(Stream stream)
    {
        Min = (Expr<float>)BaseExpr.Deserialize(stream);
        Max = (Expr<float>)BaseExpr.Deserialize(stream);
        T = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        return RpgMath.Lerp(
            Min.Eval(ctx),
            Max.Eval(ctx),
            T.Eval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Min.ToBytes(stream);
        Max.ToBytes(stream);
        T.ToBytes(stream);
    }
}
public sealed class RandomExpr : Expr<float>
{
    public readonly Expr<float> Min;
    public readonly Expr<float> Max;

    public RandomExpr(Expr<float> min, Expr<float> max)
    {
        Min = min;
        Max = max;
    }

    [ExprOp(ExprCategory.Number, "rand", "random", "distribution")]
    [ExprParam("min", typeof(float), Required = true)]
    [ExprParam("max", typeof(float), Required = true)]
    public static Expr<float> CompileOp(JsonElement obj)
        => new RandomExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("min")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("max")));
    public RandomExpr(Stream stream)
    {
        Min = (Expr<float>)BaseExpr.Deserialize(stream);
        Max = (Expr<float>)BaseExpr.Deserialize(stream);
    }
    /// <summary>
    /// Parses a range expression from a string, like 1d20, 3-10, 5:15.
    /// </summary>
    public RandomExpr(string range)
    {
        if (range.Contains('d') || range.Contains('D'))
        {
            var parts = range.Split(new char[] { 'd', 'D' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && float.TryParse(parts[0], out float count) && float.TryParse(parts[1], out float sides))
            {
                Min = new ConstNumberExpr(count);
                Max = new ConstNumberExpr(count * sides);
                return;
            }
        }
        else if (range.Contains('-') || range.Contains(':') || range.Contains(','))
        {
            var parts = range.Split(new char[] { '-', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && float.TryParse(parts[0], out float min) && float.TryParse(parts[1], out float max))
            {
                Min = new ConstNumberExpr(min);
                Max = new ConstNumberExpr(max);
                return;
            }
        }

        // Fallback to constant value
        if (float.TryParse(range, out float value))
        {
            Min = new ConstNumberExpr(value);
            Max = new ConstNumberExpr(value);
            return;
        }

        throw new ArgumentException($"Invalid range expression: {range}");
    }

    public override float Eval(EvalContext ctx)
    {
        return RpgMath.RandomFloat(
            Min.Eval(ctx),
            Max.Eval(ctx));
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Min.ToBytes(stream);
        Max.ToBytes(stream);
    }
}

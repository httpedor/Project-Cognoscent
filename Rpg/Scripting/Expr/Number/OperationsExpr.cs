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
public sealed class RangeExpr : Expr<float>
{
    public readonly Expr<float> Min;
    public readonly Expr<float> Max;

    public RangeExpr(Expr<float> min, Expr<float> max)
    {
        Min = min;
        Max = max;
    }
    public RangeExpr(Stream stream)
    {
        Min = (Expr<float>)BaseExpr.Deserialize(stream);
        Max = (Expr<float>)BaseExpr.Deserialize(stream);
    }
    /// <summary>
    /// Parses a range expression from a string, like 1d20, 3-10, 5:15.
    /// </summary>
    public RangeExpr(string range)
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
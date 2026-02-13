namespace Rpg.Scripting;

public sealed class LerpExpr : NumberExpr
{
    public readonly NumberExpr Min;
    public readonly NumberExpr Max;
    public readonly NumberExpr T;

    public LerpExpr(NumberExpr min, NumberExpr max, NumberExpr t)
    {
        Min = min;
        Max = max;
        T = t;
    }
    public LerpExpr(Stream stream)
    {
        Min = (NumberExpr)BaseExpr.Deserialize(stream);
        Max = (NumberExpr)BaseExpr.Deserialize(stream);
        T = (NumberExpr)BaseExpr.Deserialize(stream);
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
public sealed class RangeExpr : NumberExpr
{
    public readonly NumberExpr Min;
    public readonly NumberExpr Max;

    public RangeExpr(NumberExpr min, NumberExpr max)
    {
        Min = min;
        Max = max;
    }
    public RangeExpr(Stream stream)
    {
        Min = (NumberExpr)BaseExpr.Deserialize(stream);
        Max = (NumberExpr)BaseExpr.Deserialize(stream);
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
                Min = new ConstExpr(count);
                Max = new ConstExpr(count * sides);
                return;
            }
        }
        else if (range.Contains('-') || range.Contains(':') || range.Contains(','))
        {
            var parts = range.Split(new char[] { '-', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && float.TryParse(parts[0], out float min) && float.TryParse(parts[1], out float max))
            {
                Min = new ConstExpr(min);
                Max = new ConstExpr(max);
                return;
            }
        }

        // Fallback to constant value
        if (float.TryParse(range, out float value))
        {
            Min = new ConstExpr(value);
            Max = new ConstExpr(value);
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
namespace Rpg.Scripting;

public sealed class TicksToSecondsExpr : NumberExpr
{
    public const float TickRate = 50f;
    public readonly NumberExpr TicksExpr;

    public TicksToSecondsExpr(NumberExpr ticksExpr)
    {
        TicksExpr = ticksExpr;
    }
    public TicksToSecondsExpr(Stream stream)
    {
        TicksExpr = (NumberExpr)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var ticks = TicksExpr.Eval(ctx);
        return ticks / TickRate;
    }
}
public sealed class SecondsToTicksExpr : NumberExpr
{
    public readonly NumberExpr SecondsExpr;

    public SecondsToTicksExpr(NumberExpr secondsExpr)
    {
        SecondsExpr = secondsExpr;
    }
    public SecondsToTicksExpr(Stream stream)
    {
        SecondsExpr = (NumberExpr)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var seconds = SecondsExpr.Eval(ctx);
        return seconds * TicksToSecondsExpr.TickRate;
    }
}
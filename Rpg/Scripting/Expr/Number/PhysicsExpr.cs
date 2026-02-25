namespace Rpg.Scripting;

public sealed class TicksToSecondsExpr : Expr<float>
{
    public const float TickRate = 50f;
    public readonly Expr<float> TicksExpr;

    public TicksToSecondsExpr(Expr<float> ticksExpr)
    {
        TicksExpr = ticksExpr;
    }
    public TicksToSecondsExpr(Stream stream)
    {
        TicksExpr = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var ticks = TicksExpr.Eval(ctx);
        return ticks / TickRate;
    }
}
public sealed class SecondsToTicksExpr : Expr<float>
{
    public readonly Expr<float> SecondsExpr;

    public SecondsToTicksExpr(Expr<float> secondsExpr)
    {
        SecondsExpr = secondsExpr;
    }
    public SecondsToTicksExpr(Stream stream)
    {
        SecondsExpr = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var seconds = SecondsExpr.Eval(ctx);
        return seconds * TicksToSecondsExpr.TickRate;
    }
}
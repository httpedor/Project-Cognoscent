namespace Rpg.Scripting;

public sealed class GreaterThanConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public GreaterThanConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) > Right.Eval(ctx);
    }
}
public sealed class LessThanConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public LessThanConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) < Right.Eval(ctx);
    }
}
public sealed class EqualConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public EqualConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) == Right.Eval(ctx);
    }
}
public sealed class NotEqualConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public NotEqualConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) != Right.Eval(ctx);
    }
}
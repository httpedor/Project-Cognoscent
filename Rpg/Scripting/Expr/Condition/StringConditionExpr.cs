namespace Rpg.Scripting;

public class StringEqualsConditionExpr : ConditionExpr
{
    public readonly StringExpr Left;
    public readonly StringExpr Right;

    public StringEqualsConditionExpr(StringExpr left, StringExpr right)
    {
        Left = left;
        Right = right;
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) == Right.Eval(ctx);
    }
}
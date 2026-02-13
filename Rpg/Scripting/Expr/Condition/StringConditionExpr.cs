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
    public StringEqualsConditionExpr(Stream stream)
    {
        Left = (StringExpr)BaseExpr.Deserialize(stream);
        Right = (StringExpr)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) == Right.Eval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
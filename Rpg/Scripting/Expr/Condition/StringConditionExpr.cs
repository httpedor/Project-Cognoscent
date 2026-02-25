namespace Rpg.Scripting;

public class StringEqualsConditionExpr : Expr<bool>
{
    public readonly Expr<string> Left;
    public readonly Expr<string> Right;

    public StringEqualsConditionExpr(Expr<string> left, Expr<string> right)
    {
        Left = left;
        Right = right;
    }
    public StringEqualsConditionExpr(Stream stream)
    {
        Left = (Expr<string>)BaseExpr.Deserialize(stream);
        Right = (Expr<string>)BaseExpr.Deserialize(stream);
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

public class StringContainsConditionExpr : Expr<bool>
{
    public readonly Expr<string> Text;
    public readonly Expr<string> Substring;

    public StringContainsConditionExpr(Expr<string> text, Expr<string> substring)
    {
        Text = text;
        Substring = substring;
    }
    public StringContainsConditionExpr(Stream stream)
    {
        Text = (Expr<string>)BaseExpr.Deserialize(stream);
        Substring = (Expr<string>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Text.Eval(ctx).Contains(Substring.Eval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Text.ToBytes(stream);
        Substring.ToBytes(stream);
    }
}
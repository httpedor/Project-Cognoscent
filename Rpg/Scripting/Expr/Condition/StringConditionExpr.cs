namespace Rpg.Scripting;

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

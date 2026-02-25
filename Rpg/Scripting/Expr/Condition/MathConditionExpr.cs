namespace Rpg.Scripting;

public sealed class GreaterThanConditionExpr : Expr<bool>
{
    public readonly Expr<float> Left;
    public readonly Expr<float> Right;

    public GreaterThanConditionExpr(Expr<float> left, Expr<float> right)
    {
        Left = left;
        Right = right;
    }
    public GreaterThanConditionExpr(Stream stream)
    {
        Left = (Expr<float>)BaseExpr.Deserialize(stream);
        Right = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) > Right.Eval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
public sealed class LessThanConditionExpr : Expr<bool>
{
    public readonly Expr<float> Left;
    public readonly Expr<float> Right;

    public LessThanConditionExpr(Expr<float> left, Expr<float> right)
    {
        Left = left;
        Right = right;
    }
    public LessThanConditionExpr(Stream stream)
    {
        Left = (Expr<float>)BaseExpr.Deserialize(stream);
        Right = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) < Right.Eval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
public sealed class EqualConditionExpr : Expr<bool>
{
    public readonly Expr<float> Left;
    public readonly Expr<float> Right;

    public EqualConditionExpr(Expr<float> left, Expr<float> right)
    {
        Left = left;
        Right = right;
    }
    public EqualConditionExpr(Stream stream)
    {
        Left = (Expr<float>)BaseExpr.Deserialize(stream);
        Right = (Expr<float>)BaseExpr.Deserialize(stream);
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
public sealed class NotEqualConditionExpr : Expr<bool>
{
    public readonly Expr<float> Left;
    public readonly Expr<float> Right;

    public NotEqualConditionExpr(Expr<float> left, Expr<float> right)
    {
        Left = left;
        Right = right;
    }
    public NotEqualConditionExpr(Stream stream)
    {
        Left = (Expr<float>)BaseExpr.Deserialize(stream);
        Right = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.Eval(ctx) != Right.Eval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
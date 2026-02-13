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
    public GreaterThanConditionExpr(Stream stream)
    {
        Left = (NumberExpr)BaseExpr.Deserialize(stream);
        Right = (NumberExpr)BaseExpr.Deserialize(stream);
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
public sealed class LessThanConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public LessThanConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }
    public LessThanConditionExpr(Stream stream)
    {
        Left = (NumberExpr)BaseExpr.Deserialize(stream);
        Right = (NumberExpr)BaseExpr.Deserialize(stream);
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
public sealed class EqualConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public EqualConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }
    public EqualConditionExpr(Stream stream)
    {
        Left = (NumberExpr)BaseExpr.Deserialize(stream);
        Right = (NumberExpr)BaseExpr.Deserialize(stream);
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
public sealed class NotEqualConditionExpr : ConditionExpr
{
    public readonly NumberExpr Left;
    public readonly NumberExpr Right;

    public NotEqualConditionExpr(NumberExpr left, NumberExpr right)
    {
        Left = left;
        Right = right;
    }
    public NotEqualConditionExpr(Stream stream)
    {
        Left = (NumberExpr)BaseExpr.Deserialize(stream);
        Right = (NumberExpr)BaseExpr.Deserialize(stream);
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
using System.Text.Json;

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

    [ExprOp(">", Description = "True when left is greater than right.")]
    public static Expr<bool> Op(Expr<float> left, Expr<float> right)
        => new GreaterThanConditionExpr(left, right);

    [ExprOp("<=", Description = "True when left is less than or equal to right.")]
    public static Expr<bool> LessOrEqual(Expr<float> left, Expr<float> right)
        => new NotConditionExpr(new GreaterThanConditionExpr(left, right));
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

    [ExprOp("<", Description = "True when left is less than right.")]
    public static Expr<bool> Op(Expr<float> left, Expr<float> right)
        => new LessThanConditionExpr(left, right);

    [ExprOp(">=", Description = "True when left is greater than or equal to right.")]
    public static Expr<bool> GreaterOrEqual(Expr<float> left, Expr<float> right)
        => new NotConditionExpr(new LessThanConditionExpr(left, right));
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
public sealed class InRangeExpr : Expr<bool>
{
    public readonly Expr<float> Value;
    public readonly Expr<float> Min;
    public readonly Expr<float> Max;

    public InRangeExpr(Expr<float> value, Expr<float> min, Expr<float> max)
    {
        Value = value;
        Min = min;
        Max = max;
    }

    [ExprOp("in_range", Description = "True when min <= value <= max.")]
    public static Expr<bool> Op(
        [Doc("Value to test")] Expr<float> value,
        [Doc("Inclusive lower bound")] Expr<float> min,
        [Doc("Inclusive upper bound")] Expr<float> max)
        => new InRangeExpr(value, min, max);
    public InRangeExpr(Stream stream)
    {
        Value = (Expr<float>)BaseExpr.Deserialize(stream);
        Min = (Expr<float>)BaseExpr.Deserialize(stream);
        Max = (Expr<float>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        var value = Value.Eval(ctx);
        return value >= Min.Eval(ctx) && value <= Max.Eval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Value.ToBytes(stream);
        Min.ToBytes(stream);
        Max.ToBytes(stream);
    }
}
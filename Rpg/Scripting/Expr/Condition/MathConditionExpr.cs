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

    [ExprOp(ExprCategory.Condition, ">")]
    [ExprParam("left", typeof(float), Required = true)]
    [ExprParam("right", typeof(float), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new GreaterThanConditionExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("left")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("right")));

    [ExprOp(ExprCategory.Condition, "<=")]
    [ExprParam("left", typeof(float), Required = true)]
    [ExprParam("right", typeof(float), Required = true)]
    public static Expr<bool> CompileLte(JsonElement obj)
        => new NotConditionExpr(new GreaterThanConditionExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("left")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("right"))));
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

    [ExprOp(ExprCategory.Condition, "<")]
    [ExprParam("left", typeof(float), Required = true)]
    [ExprParam("right", typeof(float), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new LessThanConditionExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("left")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("right")));

    [ExprOp(ExprCategory.Condition, ">=")]
    [ExprParam("left", typeof(float), Required = true)]
    [ExprParam("right", typeof(float), Required = true)]
    public static Expr<bool> CompileGte(JsonElement obj)
        => new NotConditionExpr(new LessThanConditionExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("left")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("right"))));
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

    [ExprOp(ExprCategory.Condition, "in_range")]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("min", typeof(float), Required = true)]
    [ExprParam("max", typeof(float), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new InRangeExpr(
            ExpressionCompiler.Compile<float>(obj.GetProperty("value")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("min")),
            ExpressionCompiler.Compile<float>(obj.GetProperty("max")));
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
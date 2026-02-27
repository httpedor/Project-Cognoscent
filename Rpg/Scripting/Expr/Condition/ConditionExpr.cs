using System.Text.Json;

namespace Rpg.Scripting;

public sealed class ConstConditionExpr : Expr<bool>
{
    public readonly bool Value;
    public ConstConditionExpr(bool value) => Value = value;

    [ExprOp(ExprCategory.Condition, "true")]
    public static Expr<bool> CompileTrue(JsonElement obj) => new ConstConditionExpr(true);

    [ExprOp(ExprCategory.Condition, "false")]
    public static Expr<bool> CompileFalse(JsonElement obj) => new ConstConditionExpr(false);
    public ConstConditionExpr(Stream stream)
    {
        Value = stream.ReadBoolean();
    }
    public override bool Eval(EvalContext ctx) => Value;
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteBoolean(Value);
    }
}
public sealed class VarConditionExpr : Expr<bool>
{
    public readonly int SymbolId;

    public VarConditionExpr(int symbolId)
    {
        SymbolId = symbolId;
    }
    public VarConditionExpr(Stream stream)
    {
        SymbolId = stream.ReadInt32();
    }

    public override bool Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Condition variable symbol ID {SymbolId} is out of range.");
        }
        return (bool)ctx.Variables[SymbolId];
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(SymbolId);
    }
}
public sealed class RandomConditionExpr : Expr<bool>
{
    public readonly Expr<float> Probability; // 0.0 to 1.0

    public RandomConditionExpr(Expr<float> probability)
    {
        Probability = probability;
    }

    [ExprOp(ExprCategory.Condition, "random", "rand")]
    [ExprParam("probability", "numberExpr", Description = "Probability value 0-1 (defaults to 0.5)")]
    public static Expr<bool> CompileOp(JsonElement obj)
    {
        Expr<float> probability = obj.TryGetProperty("probability", out var probElem)
            ? ExpressionCompiler.CompileNumber(probElem)
            : new ConstNumberExpr(0.5f);
        return new RandomConditionExpr(probability);
    }
    public RandomConditionExpr(Stream stream)
    {
        Probability = BaseExpr.Deserialize<Expr<float>>(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Probability.ToBytes(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return new Random().NextDouble() < Probability.Eval(ctx);
    }
}
public sealed class ConditionalExpr<T> : Expr<T>
{
    public readonly Expr<bool> Condition;
    public readonly Expr<T> TrueExpr;
    public readonly Expr<T> FalseExpr;

    public ConditionalExpr(Expr<bool> condition, Expr<T> trueExpr, Expr<T> falseExpr)
    {
        Condition = condition;
        TrueExpr = trueExpr;
        FalseExpr = falseExpr;
    }
    public ConditionalExpr(Stream stream)
    {
        Condition = BaseExpr.Deserialize<Expr<bool>>(stream);
        TrueExpr = BaseExpr.Deserialize<Expr<T>>(stream);
        FalseExpr = BaseExpr.Deserialize<Expr<T>>(stream);
    }

    public override T Eval(EvalContext ctx)
    {
        if (Condition.Eval(ctx))
            return TrueExpr.Eval(ctx);
        else
            return FalseExpr.Eval(ctx);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Condition.ToBytes(stream);
        TrueExpr.ToBytes(stream);
        FalseExpr.ToBytes(stream);
    }
}
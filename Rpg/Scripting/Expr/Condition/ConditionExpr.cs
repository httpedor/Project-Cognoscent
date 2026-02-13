namespace Rpg.Scripting;

public abstract class ConditionExpr : Expr<bool>
{
    
}
public sealed class ConstConditionExpr : ConditionExpr
{
    public readonly bool Value;
    public ConstConditionExpr(bool value) => Value = value;
    public override bool Eval(EvalContext ctx) => Value;
}
public sealed class VarConditionExpr : ConditionExpr
{
    public readonly int SymbolId;

    public VarConditionExpr(int symbolId)
    {
        SymbolId = symbolId;
    }

    public override bool Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Condition variable symbol ID {SymbolId} is out of range.");
        }
        return (bool)ctx.Variables[SymbolId];
    }
}
public sealed class RandomConditionExpr : ConditionExpr
{
    public readonly float Probability; // 0.0 to 1.0

    public RandomConditionExpr(float probability = 0.5f)
    {
        Probability = probability;
    }

    public override bool Eval(EvalContext ctx)
    {
        return new Random().NextDouble() < Probability;
    }
}
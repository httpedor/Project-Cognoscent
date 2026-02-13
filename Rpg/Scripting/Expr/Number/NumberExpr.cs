namespace Rpg.Scripting;

public abstract class NumberExpr : Expr<float>
{
}
public sealed class ConstExpr : NumberExpr
{
    public readonly float Value;
    public ConstExpr(float value) => Value = value;
    public override float Eval(EvalContext ctx) => Value;
}

public sealed class VarExpr : NumberExpr
{
    public readonly int SymbolId;

    public VarExpr(int symbolId)
    {
        SymbolId = symbolId;
    }

    public override float Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Variable symbol ID {SymbolId} is out of range.");
        }
        return (float)ctx.Variables[SymbolId];
    }
}
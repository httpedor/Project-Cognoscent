using Rpg.Entities;

namespace Rpg.Scripting;

public abstract class SelectorExpr : Expr<Entity?>
{

}
public sealed class CallerSelectorExpr : SelectorExpr
{
    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Caller;
    }
}
public sealed class TargetSelectorExpr : SelectorExpr
{
    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Target;
    }
}
public sealed class TargetPartSelectorExpr : SelectorExpr
{
    public override Entity? Eval(EvalContext ctx)
    {
            return ctx.TargetPart;
    }
}
public sealed class VarEntitySelectorExpr : SelectorExpr
{
    public readonly int SymbolId;

    public VarEntitySelectorExpr(int symbolId)
    {
        SymbolId = symbolId;
    }

    public override Entity? Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Entity variable symbol ID {SymbolId} is out of range.");
        }
        return ctx.Variables[SymbolId] as Entity;
    }
}
public sealed class NoEntitySelectorExpr : SelectorExpr
{
    public override Entity? Eval(EvalContext ctx)
    {
        return null;
    }
}
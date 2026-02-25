using Rpg.Entities;

namespace Rpg.Scripting;

public sealed class CallerSelectorExpr : Expr<Entity?>
{
    public CallerSelectorExpr(){}
    public CallerSelectorExpr(Stream stream){}
    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Caller;
    }
}
public sealed class TargetSelectorExpr : Expr<Entity?>
{
    public TargetSelectorExpr(){}
    public TargetSelectorExpr(Stream stream){}
    public override Entity? Eval(EvalContext ctx)
    {
        return ctx.Target;
    }
}
public sealed class TargetPartSelectorExpr : Expr<Entity?>
{
    public TargetPartSelectorExpr(){}
    public TargetPartSelectorExpr(Stream stream){}
    public override Entity? Eval(EvalContext ctx)
    {
            return ctx.TargetPart;
    }
}
public sealed class VarEntitySelectorExpr : Expr<Entity?>
{
    public readonly int SymbolId;

    public VarEntitySelectorExpr(int symbolId)
    {
        SymbolId = symbolId;
    }
    public VarEntitySelectorExpr(Stream stream)
    {
        SymbolId = stream.ReadInt32();
    }

    public override Entity? Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Entity variable symbol ID {SymbolId} is out of range.");
        }
        return ctx.Variables[SymbolId] as Entity;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(SymbolId);
    }
}
public sealed class NoEntitySelectorExpr : Expr<Entity?>
{
    public NoEntitySelectorExpr(){}
    public NoEntitySelectorExpr(Stream stream){}
    public override Entity? Eval(EvalContext ctx)
    {
        return null;
    }
}
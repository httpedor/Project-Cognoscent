namespace Rpg.Scripting;

public sealed class AndConditionExpr : ConditionExpr
{
    public readonly ConditionExpr[] Args;
    public AndConditionExpr(ConditionExpr[] args) => Args = args;

    public override bool Eval(EvalContext ctx)
    {
        foreach (var e in Args)
            if (!e.Eval(ctx))
                return false;
        return true;
    }
}
public sealed class OrConditionExpr : ConditionExpr
{
    public readonly ConditionExpr[] Args;
    public OrConditionExpr(ConditionExpr[] args) => Args = args;

    public override bool Eval(EvalContext ctx)
    {
        foreach (var e in Args)
            if (e.Eval(ctx))
                return true;
        return false;
    }
}
public sealed class NotConditionExpr : ConditionExpr
{
    public readonly ConditionExpr Arg;
    public NotConditionExpr(ConditionExpr arg) => Arg = arg;

    public override bool Eval(EvalContext ctx)
    {
        return !Arg.Eval(ctx);
    }
}
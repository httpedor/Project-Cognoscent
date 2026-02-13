namespace Rpg.Scripting;

public class CompendiumEntryExpr<T> : Expr<T?> where T : class
{
    public readonly StringExpr IdExpr;
    public CompendiumEntryExpr(StringExpr idExpr)
    {
        IdExpr = idExpr;
    }
    public override T? Eval(EvalContext ctx)
    {
        string id = IdExpr.Eval(ctx);
        return Compendium.GetEntry<T>(id);
    }
}
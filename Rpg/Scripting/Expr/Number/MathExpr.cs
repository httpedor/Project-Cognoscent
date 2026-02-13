namespace Rpg.Scripting;

public sealed class AddExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public AddExpr(NumberExpr[] args) => Args = args;

    public override float Eval(EvalContext ctx)
    {
        float sum = 0;
        foreach (var e in Args)
            sum += e.Eval(ctx);
        return sum;
    }
}
public sealed class SubExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public SubExpr(NumberExpr[] args) => Args = args;

    public override float Eval(EvalContext ctx)
    {
        float result = Args[0].Eval(ctx);
        for (int i = 1; i < Args.Length; i++)
            result -= Args[i].Eval(ctx);
        return result;
    }
}
public sealed class MulExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public MulExpr(NumberExpr[] args) => Args = args;

    public override float Eval(EvalContext ctx)
    {
        float product = 1f;
        foreach (var e in Args)
            product *= e.Eval(ctx);
        return product;
    }
}
public sealed class DivExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public DivExpr(NumberExpr[] args) => Args = args;

    public override float Eval(EvalContext ctx)
    {
        float result = Args[0].Eval(ctx);
        for (int i = 1; i < Args.Length; i++)
            result /= Args[i].Eval(ctx);
        return result;
    }
}

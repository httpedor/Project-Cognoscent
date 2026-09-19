using System.Text.Json;

namespace Rpg.Scripting;

public sealed class AndConditionExpr : Expr<bool>
{
    public readonly ArrayExpr<bool> Args;
    public AndConditionExpr(ArrayExpr<bool> args) => Args = args;

    [ExprOp("and", Description = "True when every condition is true.")]
    public static Expr<bool> Op([Doc("Conditions that must all hold")] ArrayExpr<bool> conditions)
        => new AndConditionExpr(conditions);
    public AndConditionExpr(Stream stream)
    {
        Args = (ArrayExpr<bool>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        foreach (var e in Args.Eval(ctx))
            if (!e)
                return false;
        return true;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Args.ToBytes(stream);
    }
}
public sealed class OrConditionExpr : Expr<bool>
{
    public readonly ArrayExpr<bool> Args;
    public OrConditionExpr(ArrayExpr<bool> args) => Args = args;

    [ExprOp("or", Description = "True when at least one condition is true.")]
    public static Expr<bool> Op([Doc("Conditions, any of which may hold")] ArrayExpr<bool> conditions)
        => new OrConditionExpr(conditions);
    public OrConditionExpr(Stream stream)
    {
        Args = (ArrayExpr<bool>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        foreach (var e in Args.Eval(ctx))
            if (e)
                return true;
        return false;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Args.ToBytes(stream);
    }
}
public sealed class NotConditionExpr : Expr<bool>
{
    public readonly Expr<bool> Arg;
    public NotConditionExpr(Expr<bool> arg) => Arg = arg;

    [ExprOp("not", Description = "Negates a condition.")]
    public static Expr<bool> Op([Doc("Condition to negate")] Expr<bool> condition)
        => new NotConditionExpr(condition);
    public NotConditionExpr(Stream stream)
    {
        Arg = (Expr<bool>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return !Arg.Eval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Arg.ToBytes(stream);
    }
}

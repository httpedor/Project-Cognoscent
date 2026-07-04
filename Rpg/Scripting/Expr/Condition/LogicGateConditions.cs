using System.Text.Json;

namespace Rpg.Scripting;

public sealed class AndConditionExpr : Expr<bool>
{
    public readonly ArrayExpr<bool> Args;
    public AndConditionExpr(ArrayExpr<bool> args) => Args = args;

    [ExprOp(ExprCategory.Condition, "and")]
    [ExprParam("conditions", typeof(bool[]), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new AndConditionExpr(ExpressionCompiler.CompileArray<bool>(obj.GetProperty("conditions")));
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

    [ExprOp(ExprCategory.Condition, "or")]
    [ExprParam("conditions", typeof(bool[]), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new OrConditionExpr(ExpressionCompiler.CompileArray<bool>(obj.GetProperty("conditions")));
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

    [ExprOp(ExprCategory.Condition, "not")]
    [ExprParam("condition", typeof(bool), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new NotConditionExpr(ExpressionCompiler.Compile<bool>(obj.GetProperty("condition")));
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

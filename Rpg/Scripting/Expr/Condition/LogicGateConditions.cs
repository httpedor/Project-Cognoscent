using System.Text.Json;

namespace Rpg.Scripting;

public sealed class AndConditionExpr : Expr<bool>
{
    public readonly Expr<bool>[] Args;
    public AndConditionExpr(Expr<bool>[] args) => Args = args;

    [ExprOp(ExprCategory.Condition, "and")]
    [ExprParam("conditions", "conditionExpr[]", Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new AndConditionExpr(ExpressionCompiler.CompileArgsAs<Expr<bool>>(obj.GetProperty("conditions")));
    public AndConditionExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new Expr<bool>[length];
        for (int i = 0; i < length; i++)
            Args[i] = (Expr<bool>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        foreach (var e in Args)
            if (!e.Eval(ctx))
                return false;
        return true;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Args.Length);
        foreach (var e in Args)
            e.ToBytes(stream);
    }
}
public sealed class OrConditionExpr : Expr<bool>
{
    public readonly Expr<bool>[] Args;
    public OrConditionExpr(Expr<bool>[] args) => Args = args;

    [ExprOp(ExprCategory.Condition, "or")]
    [ExprParam("conditions", "conditionExpr[]", Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new OrConditionExpr(ExpressionCompiler.CompileArgsAs<Expr<bool>>(obj.GetProperty("conditions")));
    public OrConditionExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new Expr<bool>[length];
        for (int i = 0; i < length; i++)
            Args[i] = (Expr<bool>)BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        foreach (var e in Args)
            if (e.Eval(ctx))
                return true;
        return false;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Args.Length);
        foreach (var e in Args)
            e.ToBytes(stream);
    }
}
public sealed class NotConditionExpr : Expr<bool>
{
    public readonly Expr<bool> Arg;
    public NotConditionExpr(Expr<bool> arg) => Arg = arg;

    [ExprOp(ExprCategory.Condition, "not")]
    [ExprParam("condition", "conditionExpr", Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new NotConditionExpr(ExpressionCompiler.CompileCondition(obj.GetProperty("condition")));
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
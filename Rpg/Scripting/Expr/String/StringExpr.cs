using Rpg.Skills;

namespace Rpg.Scripting;

public abstract class StringExpr : Expr<string>
{
}

public sealed class StringLiteralExpr : StringExpr
{
    private readonly string value;

    public StringLiteralExpr(string value)
    {
        this.value = value;
    }

    public override string Eval(EvalContext ctx)
    {
        return value;
    }

}
public sealed class VarStringExpr : StringExpr
{
    public readonly int SymbolId;

    public VarStringExpr(int SymbolId)
    {
        this.SymbolId = SymbolId;
    }

    public override string Eval(EvalContext ctx)
    {
        return ctx.GetVariable<string>(SymbolId);
    }
}
public sealed class ArgumentTypeNameExpr : StringExpr
{
    public readonly int ArgumentIndex;

    public ArgumentTypeNameExpr(int argumentIndex)
    {
        ArgumentIndex = argumentIndex;
    }

    public override string Eval(EvalContext ctx)
    {
        var arg = ctx.Variables[ArgumentIndex];
        if (arg is SkillArgument skillArg)
            return SkillArgument.ArgumentTypeToString(skillArg.GetType());
        return arg?.GetType().Name ?? "null";
    }
}
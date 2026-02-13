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
    public StringLiteralExpr(Stream stream)
    {
        value = stream.ReadString();
    }

    public override string Eval(EvalContext ctx)
    {
        return value;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(value);
    }
}
public sealed class ArgumentTypeNameExpr : StringExpr
{
    public readonly int ArgumentIndex;

    public ArgumentTypeNameExpr(int argumentIndex)
    {
        ArgumentIndex = argumentIndex;
    }
    public ArgumentTypeNameExpr(Stream stream)
    {
        ArgumentIndex = stream.ReadInt32();
    }

    public override string Eval(EvalContext ctx)
    {
        var arg = ctx.Variables[ArgumentIndex];
        if (arg is SkillArgument skillArg)
            return SkillArgument.ArgumentTypeToString(skillArg.GetType());
        return arg?.GetType().Name ?? "null";
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(ArgumentIndex);
    }
}
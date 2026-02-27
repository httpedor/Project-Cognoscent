using System.Text.Json;
using Rpg.Entities;
using Rpg.Health;
using Rpg.Skills;

namespace Rpg.Scripting;

public sealed class StringLiteralExpr : Expr<string>
{
    public readonly string Value;

    public StringLiteralExpr(string value)
    {
        this.Value = value;
    }
    public StringLiteralExpr(Stream stream)
    {
        Value = stream.ReadString();
    }

    public override string Eval(EvalContext ctx)
    {
        return Value;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Value);
    }
}
public sealed class VarStringExpr : Expr<string>
{
    public readonly int SymbolId;

    public VarStringExpr(int symbolId)
    {
        SymbolId = symbolId;
    }
    public VarStringExpr(Stream stream)
    {
        SymbolId = stream.ReadInt32();
    }

    public override string Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"String variable symbol ID {SymbolId} is out of range.");
        }
        var value = ctx.Variables[SymbolId];
        if (value == null)
            return "null";
        if (value is Entity entity)
            return entity.Name;
        if (value is Skill skill)
            return skill.GetName();
        if (value is SkillArgument skillArg)
            return SkillArgument.ArgumentTypeToString(skillArg.GetType());
        if (value is DamageType damageType)
            return damageType.Name;
        return value.ToString() ?? "null";
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(SymbolId);
    }
}

public sealed class StringConcatExpr : Expr<string>
{
    public readonly Expr<string>[] Parts;

    public StringConcatExpr(Expr<string>[] parts)
    {
        Parts = parts;
    }

    [ExprOp(ExprCategory.String, "concat", "add", "join")]
    [ExprParam("strings", "stringExpr[]", Required = true)]
    public static Expr<string> CompileOp(JsonElement obj)
        => new StringConcatExpr(ExpressionCompiler.CompileArgsAs<Expr<string>>(obj.GetProperty("strings")));
    public StringConcatExpr(Stream stream)
    {
        int partCount = stream.ReadByte();
        Parts = new Expr<string>[partCount];
        for (int i = 0; i < partCount; i++)
        {
            Parts[i] = (Expr<string>)BaseExpr.Deserialize(stream);
        }
    }

    public override string Eval(EvalContext ctx)
    {
        return string.Concat(Parts.Select(p => p.Eval(ctx)));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteByte((byte)Parts.Length);
        foreach (var part in Parts)
        {
            part.ToBytes(stream);
        }
    }
}
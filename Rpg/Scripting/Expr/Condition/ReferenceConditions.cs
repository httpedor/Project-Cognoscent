using System.Text.Json;

namespace Rpg.Scripting;

public sealed class VarHasTagConditionExpr : Expr<bool>
{
    public readonly int VariableSymbolId;
    public readonly string Tag;

    public VarHasTagConditionExpr(int variableSymbolId, string tag)
    {
        VariableSymbolId = variableSymbolId;
        Tag = tag;
    }
    public VarHasTagConditionExpr(Stream stream)
    {
        VariableSymbolId = stream.ReadInt32();
        Tag = stream.ReadString();
    }

    public override bool Eval(EvalContext ctx)
    {
        var obj = ctx.GetVariable(VariableSymbolId);
        if (obj is ITaggable taggable)
        {
            return taggable.HasTag(Tag);
        }
        throw new InvalidOperationException($"Variable with symbol ID {VariableSymbolId} is not taggable.");
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(VariableSymbolId);
        stream.WriteString(Tag);
    }

    [ExprOp("var_has_tag", Description = "True when the referenced variable carries a tag.")]
    public static Expr<bool> Op(
        [Doc("Variable to check, by index or context name")] Types.VariableRef var,
        [Doc("Tag to look for")] string tag)
        => new VarHasTagConditionExpr(var, tag);
}

public sealed class VarIsEntryConditionExpr : Expr<bool>
{
    public Expr<string> EntryName;
    public int VariableID;
    public VarIsEntryConditionExpr(Expr<string> name, int id)
    {
        EntryName = name;
        VariableID = id;
    }
    public override bool Eval(EvalContext ctx)
    {
        var obj = ctx.GetVariable(VariableID);
        if (obj == null)
            return false;
        var entryName = Compendium.GetEntryName(obj);
        if (entryName == null)
            Logger.Log("Object of type " + obj.GetType() + " is not an CompendiumEntry!", LogLevel.Warning);

        return entryName == EntryName.Eval(ctx);
    }

    [ExprOp("var_name_is", "var_name_equals",
            Description = "True when the referenced variable is the named compendium entry.")]
    public static Expr<bool> Op(
        [Doc("Compendium entry name to compare against")] Expr<string> name,
        [Doc("Variable to check, by index or context name")] Types.VariableRef var)
        => new VarIsEntryConditionExpr(name, var);
}

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

    [ExprOp(ExprCategory.Condition, "var_has_tag")]
    [ExprParam("var", typeof(float), Required = true, Description = "Which variable to check")]
    [ExprParam("tag", typeof(string), Required = true, Description = "Tag to check for")]
    public static Expr<bool> CompileOp(JsonElement obj)
    {
        var varIdProp = obj.GetProperty("var");
        int varId;
        if (varIdProp.ValueKind == JsonValueKind.Number)
        {
            varId = varIdProp.GetInt32();
        }
        else if (varIdProp.ValueKind == JsonValueKind.String)
        {
            // Try to parse the variable symbol name to an ID
            string varSymbolName = varIdProp.GetString()!;
            varId = ExpressionCompiler.GetVariableSymbolId(varSymbolName);
        }
        else
        {
            throw new JsonException("The 'var' property must be either a number (variable ID) or a string (variable symbol name).");
        }
        string tag = obj.GetProperty("tag").GetString()!;
        return new VarHasTagConditionExpr(varId, tag);
    }
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

    [ExprOp(ExprCategory.Condition, "var_name_is", "var_name_equals")]
    [ExprParam("name", typeof(string), Required = true, Description = "The name to check")]
    [ExprParam("var", typeof(float), Required = true, Description = "Which variable to check")]
    public static Expr<bool> CompileOp(JsonElement obj)
    {
        return new VarIsEntryConditionExpr(ExpressionCompiler.Compile<string>(obj.GetProperty("name")), obj.GetProperty("var").GetInt32());
    }
}

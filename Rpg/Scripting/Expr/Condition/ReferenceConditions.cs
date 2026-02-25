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
        if (VariableSymbolId < 0 || VariableSymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Condition variable symbol ID {VariableSymbolId} is out of range.");
        }
        var obj = ctx.Variables[VariableSymbolId];
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
}
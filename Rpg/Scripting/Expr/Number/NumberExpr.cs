namespace Rpg.Scripting;

public abstract class NumberExpr : Expr<float>
{
}
public sealed class ConstExpr : NumberExpr
{
    public readonly float Value;
    public ConstExpr(float value) => Value = value;
    public ConstExpr(Stream stream)
    {
        Value = stream.ReadFloat();
    }
    public override float Eval(EvalContext ctx) => Value;
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteFloat(Value);
    }
}

public sealed class VarExpr : NumberExpr
{
    public readonly int SymbolId;

    public VarExpr(int symbolId)
    {
        SymbolId = symbolId;
    }
    public VarExpr(Stream stream)
    {
        SymbolId = stream.ReadInt32();
    }

    public override float Eval(EvalContext ctx)
    {
        if (SymbolId < 0 || SymbolId >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Variable symbol ID {SymbolId} is out of range.");
        }
        return (float)ctx.Variables[SymbolId];
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(SymbolId);
    }
}
namespace Rpg.Scripting;

public sealed class ConstNumberExpr : Expr<float>
{
    public readonly float Value;
    public ConstNumberExpr(float value) => Value = value;
    public ConstNumberExpr(Stream stream)
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

public sealed class VarNumberExpr : Expr<float>
{
    public readonly int Index;

    public VarNumberExpr(int symbolId)
    {
        Index = symbolId;
    }
    public VarNumberExpr(Stream stream)
    {
        Index = stream.ReadInt32();
    }

    public override float Eval(EvalContext ctx)
    {
        if (Index < 0 || Index >= ctx.Variables.Length)
        {
            throw new IndexOutOfRangeException($"Variable symbol ID {Index} is out of range.");
        }
        return (float)ctx.Variables[Index];
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Index);
    }
}
namespace Rpg.Scripting;

public sealed class AndConditionExpr : ConditionExpr
{
    public readonly ConditionExpr[] Args;
    public AndConditionExpr(ConditionExpr[] args) => Args = args;
    public AndConditionExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new ConditionExpr[length];
        for (int i = 0; i < length; i++)
            Args[i] = (ConditionExpr)BaseExpr.Deserialize(stream);
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
public sealed class OrConditionExpr : ConditionExpr
{
    public readonly ConditionExpr[] Args;
    public OrConditionExpr(ConditionExpr[] args) => Args = args;
    public OrConditionExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new ConditionExpr[length];
        for (int i = 0; i < length; i++)
            Args[i] = (ConditionExpr)BaseExpr.Deserialize(stream);
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
public sealed class NotConditionExpr : ConditionExpr
{
    public readonly ConditionExpr Arg;
    public NotConditionExpr(ConditionExpr arg) => Arg = arg;
    public NotConditionExpr(Stream stream)
    {
        Arg = (ConditionExpr)BaseExpr.Deserialize(stream);
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
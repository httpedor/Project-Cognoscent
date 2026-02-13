namespace Rpg.Scripting;

public sealed class AddExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public AddExpr(NumberExpr[] args) => Args = args;
    public AddExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new NumberExpr[length];
        for (int i = 0; i < length; i++)
            Args[i] = (NumberExpr)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        float sum = 0;
        foreach (var e in Args)
            sum += e.Eval(ctx);
        return sum;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Args.Length);
        foreach (var e in Args)
            e.ToBytes(stream);
    }
}
public sealed class SubExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public SubExpr(NumberExpr[] args) => Args = args;
    public SubExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new NumberExpr[length];
        for (int i = 0; i < length; i++)
            Args[i] = (NumberExpr)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        float result = Args[0].Eval(ctx);
        for (int i = 1; i < Args.Length; i++)
            result -= Args[i].Eval(ctx);
        return result;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Args.Length);
        foreach (var e in Args)
            e.ToBytes(stream);
    }
}
public sealed class MulExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public MulExpr(NumberExpr[] args) => Args = args;
    public MulExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new NumberExpr[length];
        for (int i = 0; i < length; i++)
            Args[i] = (NumberExpr)BaseExpr.Deserialize(stream);
    }
    public override float Eval(EvalContext ctx)
    {
        float product = 1f;
        foreach (var e in Args)
            product *= e.Eval(ctx);
        return product;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Args.Length);
        foreach (var e in Args)
            e.ToBytes(stream);
    }
}
public sealed class DivExpr : NumberExpr
{
    public readonly NumberExpr[] Args;
    public DivExpr(NumberExpr[] args) => Args = args;
    public DivExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new NumberExpr[length];
        for (int i = 0; i < length; i++)
            Args[i] = (NumberExpr)BaseExpr.Deserialize(stream);
    }
    public override float Eval(EvalContext ctx)
    {
        float result = Args[0].Eval(ctx);
        for (int i = 1; i < Args.Length; i++)
            result /= Args[i].Eval(ctx);
        return result;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Args.Length);
        foreach (var e in Args)
            e.ToBytes(stream);
    }
}

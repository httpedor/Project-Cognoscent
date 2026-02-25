namespace Rpg.Scripting;

public sealed class AddExpr : Expr<float>
{
    public readonly Expr<float>[] Args;
    public AddExpr(Expr<float>[] args) => Args = args;
    public AddExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new Expr<float>[length];
        for (int i = 0; i < length; i++)
            Args[i] = (Expr<float>)BaseExpr.Deserialize(stream);
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
public sealed class SubExpr : Expr<float>
{
    public readonly Expr<float>[] Args;
    public SubExpr(Expr<float>[] args) => Args = args;
    public SubExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new Expr<float>[length];
        for (int i = 0; i < length; i++)
            Args[i] = (Expr<float>)BaseExpr.Deserialize(stream);
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
public sealed class MulExpr : Expr<float>
{
    public readonly Expr<float>[] Args;
    public MulExpr(Expr<float>[] args) => Args = args;
    public MulExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new Expr<float>[length];
        for (int i = 0; i < length; i++)
            Args[i] = (Expr<float>)BaseExpr.Deserialize(stream);
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
public sealed class DivExpr : Expr<float>
{
    public readonly Expr<float>[] Args;
    public DivExpr(Expr<float>[] args) => Args = args;
    public DivExpr(Stream stream)
    {
        int length = stream.ReadInt32();
        Args = new Expr<float>[length];
        for (int i = 0; i < length; i++)
            Args[i] = (Expr<float>)BaseExpr.Deserialize(stream);
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

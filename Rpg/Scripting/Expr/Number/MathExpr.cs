using System.Text.Json;

namespace Rpg.Scripting;

public sealed class AddExpr : Expr<float>
{
    public readonly ArrayExpr<float> Args;
    public AddExpr(ArrayExpr<float> args) => Args = args;
    public AddExpr(params Expr<float>[] args) => Args = new ConstArrayExpr<float>(args);

    [ExprOp("sum", "plus", "add", "addition", "+", Description = "Adds every number in the array.")]
    public static Expr<float> Op([Doc("Numbers to sum")] ArrayExpr<float> numbers)
        => new AddExpr(numbers);
    public AddExpr(Stream stream)
    {
        Args = (ArrayExpr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        float sum = 0;
        var result = Args.Eval(ctx);
        foreach (var e in result)
            sum += e;
        return sum;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Args.ToBytes(stream);
    }
}
public sealed class SubExpr : Expr<float>
{
    public readonly ArrayExpr<float> Args;
    public SubExpr(ArrayExpr<float> args) => Args = args;
    public SubExpr(params Expr<float>[] args) => Args = new ConstArrayExpr<float>(args);

    [ExprOp("sub", "subtract", "minus", "subtraction", "-",
            Description = "Subtracts each subsequent number from the first.")]
    public static Expr<float> Op([Doc("Numbers to subtract sequentially")] ArrayExpr<float> numbers)
        => new SubExpr(numbers);
    public SubExpr(Stream stream)
    {
        Args = (ArrayExpr<float>)BaseExpr.Deserialize(stream);
    }

    public override float Eval(EvalContext ctx)
    {
        var args = Args.Eval(ctx).ToArray();
        float result = args[0];
        for (int i = 1; i < args.Length; i++)
            result -= args[i];
        return result;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Args.ToBytes(stream);
    }
}
public sealed class MulExpr : Expr<float>
{
    public readonly ArrayExpr<float> Args;
    public MulExpr(ArrayExpr<float> args) => Args = args;
    public MulExpr(params Expr<float>[] args) => Args = new ConstArrayExpr<float>(args);

    [ExprOp("mul", "multiply", "times", "multiplication", "*",
            Description = "Multiplies every number in the array.")]
    public static Expr<float> Op([Doc("Numbers to multiply")] ArrayExpr<float> numbers)
        => new MulExpr(numbers);
    public MulExpr(Stream stream)
    {
        Args = (ArrayExpr<float>)BaseExpr.Deserialize(stream);
    }
    public override float Eval(EvalContext ctx)
    {
        float product = 1f;
        foreach (var e in Args.Eval(ctx))
            product *= e;
        return product;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Args.ToBytes(stream);
    }
}
public sealed class DivExpr : Expr<float>
{
    public readonly ArrayExpr<float> Args;
    public DivExpr(ArrayExpr<float> args) => Args = args;
    public DivExpr(params Expr<float>[] args) => Args = new ConstArrayExpr<float>(args);

    [ExprOp("div", "divide", "division", "/",
            Description = "Divides the first number by each subsequent one.")]
    public static Expr<float> Op([Doc("Numbers to divide sequentially")] ArrayExpr<float> numbers)
        => new DivExpr(numbers);
    public DivExpr(Stream stream)
    {
        Args = (ArrayExpr<float>)BaseExpr.Deserialize(stream);
    }
    public override float Eval(EvalContext ctx)
    {
        var args = Args.Eval(ctx).ToArray();
        float result = args[0];
        for (int i = 1; i < args.Length; i++)
            result /= args[i];
        return result;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Args.ToBytes(stream);
    }
}

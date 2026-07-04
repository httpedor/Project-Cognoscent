using System.Text.Json;

namespace Rpg.Scripting;

public sealed class AddExpr : Expr<float>
{
    public readonly ArrayExpr<float> Args;
    public AddExpr(ArrayExpr<float> args) => Args = args;
    public AddExpr(params Expr<float>[] args) => Args = new ConstArrayExpr<float>(args);

    [ExprOp(ExprCategory.Number, "sum", "plus", "add", "addition", "+")]
    [ExprParam("numbers", typeof(global::System.Collections.Generic.List<float>), Required = true, Description = "Array of numbers to sum")]
    public static Expr<float> CompileOp(JsonElement obj)
        => new AddExpr(ExpressionCompiler.CompileArray<float>(obj.GetProperty("numbers")));
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

    [ExprOp(ExprCategory.Number, "sub", "subtract", "minus", "subtraction", "-")]
    [ExprParam("numbers", typeof(float[]), Required = true, Description = "Array of numbers to subtract sequentially")]
    public static Expr<float> CompileOp(JsonElement obj)
        => new SubExpr(ExpressionCompiler.CompileArray<float>(obj.GetProperty("numbers")));
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

    [ExprOp(ExprCategory.Number, "mul", "multiply", "times", "multiplication", "*")]
    [ExprParam("numbers", typeof(float[]), Required = true, Description = "Array of numbers to multiply")]
    public static Expr<float> CompileOp(JsonElement obj)
        => new MulExpr(ExpressionCompiler.CompileArray<float>(obj.GetProperty("numbers")));
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

    [ExprOp(ExprCategory.Number, "div", "divide", "division", "/")]
    [ExprParam("numbers", typeof(float[]), Required = true, Description = "Array of numbers to divide sequentially")]
    public static Expr<float> CompileOp(JsonElement obj)
        => new DivExpr(ExpressionCompiler.CompileArray<float>(obj.GetProperty("numbers")));
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

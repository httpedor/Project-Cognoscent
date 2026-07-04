using System.Text.Json;

namespace Rpg.Scripting;

public sealed class ConstConditionExpr : Expr<bool>
{
    public readonly bool Value;
    public ConstConditionExpr(bool value) => Value = value;

    [ExprOp(ExprCategory.Condition, "true")]
    public static Expr<bool> CompileTrue(JsonElement obj) => new ConstConditionExpr(true);

    [ExprOp(ExprCategory.Condition, "false")]
    public static Expr<bool> CompileFalse(JsonElement obj) => new ConstConditionExpr(false);
    public ConstConditionExpr(Stream stream)
    {
        Value = stream.ReadBoolean();
    }
    public override bool Eval(EvalContext ctx) => Value;
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteBoolean(Value);
    }
}
public sealed class RandomConditionExpr : Expr<bool>
{
    public readonly Expr<float> Probability; // 0.0 to 1.0

    public RandomConditionExpr(Expr<float> probability)
    {
        Probability = probability;
    }

    [ExprOp(ExprCategory.Condition, "random", "rand")]
    [ExprParam("probability", typeof(float), Description = "Probability value 0-1 (defaults to 0.5)")]
    public static Expr<bool> CompileOp(JsonElement obj)
    {
        Expr<float> probability = obj.TryGetProperty("probability", out var probElem)
            ? ExpressionCompiler.Compile<float>(probElem)
            : new ConstNumberExpr(0.5f);
        return new RandomConditionExpr(probability);
    }
    public RandomConditionExpr(Stream stream)
    {
        Probability = BaseExpr.Deserialize<Expr<float>>(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Probability.ToBytes(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return new Random().NextDouble() < Probability.Eval(ctx);
    }
}
public sealed class ConditionalExpr<T> : Expr<T>
{
    public readonly Expr<bool> Condition;
    public readonly Expr<T> TrueExpr;
    public readonly Expr<T> FalseExpr;

    public ConditionalExpr(Expr<bool> condition, Expr<T> trueExpr, Expr<T> falseExpr)
    {
        Condition = condition;
        TrueExpr = trueExpr;
        FalseExpr = falseExpr;
    }
    public ConditionalExpr(Stream stream)
    {
        Condition = BaseExpr.Deserialize<Expr<bool>>(stream);
        TrueExpr = BaseExpr.Deserialize<Expr<T>>(stream);
        FalseExpr = BaseExpr.Deserialize<Expr<T>>(stream);
    }

    public override T Eval(EvalContext ctx)
    {
        if (Condition.Eval(ctx))
            return TrueExpr.Eval(ctx);
        else
            return FalseExpr.Eval(ctx);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Condition.ToBytes(stream);
        TrueExpr.ToBytes(stream);
        FalseExpr.ToBytes(stream);
    }
}

public class IfElseExpr<T> : Expr<T>
{
    public readonly List<(Expr<bool> Condition, Expr<T> Result)> Cases;
    public readonly Expr<T>? DefaultCase;

    public IfElseExpr(List<(Expr<bool>, Expr<T>)> cases, Expr<T>? defaultCase = null)
    {
        Cases = cases;
        DefaultCase = defaultCase;
    }

    public IfElseExpr(Stream stream)
    {
        int caseCount = stream.ReadInt32();
        Cases = new List<(Expr<bool>, Expr<T>)>();
        for (int i = 0; i < caseCount; i++)
        {
            var condition = BaseExpr.Deserialize<Expr<bool>>(stream);
            var result = BaseExpr.Deserialize<Expr<T>>(stream);
            Cases.Add((condition, result));
        }
        if (stream.ReadBoolean())
        {
            DefaultCase = BaseExpr.Deserialize<Expr<T>>(stream);
        }
    }

    public override T Eval(EvalContext ctx)
    {
        foreach (var (condition, result) in Cases)
        {
            if (condition.Eval(ctx))
                return result.Eval(ctx);
        }
        if (DefaultCase != null)
            return DefaultCase.Eval(ctx);
        throw new Exception("No conditions matched and no default case provided in SwitchConditionExpr");
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Cases.Count);
        foreach (var (condition, result) in Cases)
        {
            condition.ToBytes(stream);
            result.ToBytes(stream);
        }
        stream.WriteBoolean(DefaultCase != null);
        if (DefaultCase != null)
            DefaultCase.ToBytes(stream);
    }
}

public class SwitchNumberExpr<T> : Expr<T>
{
    public readonly Expr<float> Value;
    public readonly Dictionary<float, Expr<T>> Cases;
    public readonly Expr<T>? DefaultCase;

    public SwitchNumberExpr(Expr<float> value, Dictionary<float, Expr<T>> cases, Expr<T>? defaultCase = null)
    {
        Value = value;
        Cases = cases;
        DefaultCase = defaultCase;
    }

    public SwitchNumberExpr(Stream stream)
    {
        Value = BaseExpr.Deserialize<Expr<float>>(stream);
        int caseCount = stream.ReadInt32();
        Cases = new Dictionary<float, Expr<T>>();
        for (int i = 0; i < caseCount; i++)
        {
            var key = stream.ReadFloat();
            var result = BaseExpr.Deserialize<Expr<T>>(stream);
            Cases[key] = result;
        }
        if (stream.ReadBoolean())
        {
            DefaultCase = BaseExpr.Deserialize<Expr<T>>(stream);
        }
    }

    public override T Eval(EvalContext ctx)
    {
        float value = Value.Eval(ctx);
        if (Cases.TryGetValue(value, out var caseExpr))
        {
            return caseExpr.Eval(ctx);
        }
        if (DefaultCase != null)
            return DefaultCase.Eval(ctx);
        throw new Exception("No cases matched and no default case provided in SwitchNumberExpr");
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Value.ToBytes(stream);
        stream.WriteInt32(Cases.Count);
        foreach (var (condition, result) in Cases)
        {
            stream.WriteFloat(condition);
            result.ToBytes(stream);
        }
        stream.WriteBoolean(DefaultCase != null);
        if (DefaultCase != null)
            DefaultCase.ToBytes(stream);
    }
}

public sealed class SwitchRangeExpr<T> : Expr<T>
{
    public readonly Expr<float> Value;
    public readonly List<(float Min, float Max, Expr<T> Result)> Cases;
    public readonly Expr<T>? DefaultCase;

    public SwitchRangeExpr(Expr<float> value, List<(float Min, float Max, Expr<T> Result)> cases, Expr<T>? defaultCase = null)
    {
        Value = value;
        Cases = cases;
        DefaultCase = defaultCase;
    }

    public SwitchRangeExpr(Stream stream)
    {
        Value = BaseExpr.Deserialize<Expr<float>>(stream);
        int caseCount = stream.ReadInt32();
        Cases = new List<(float, float, Expr<T>)>();
        for (int i = 0; i < caseCount; i++)
        {
            var min = stream.ReadFloat();
            var max = stream.ReadFloat();
            var result = BaseExpr.Deserialize<Expr<T>>(stream);
            Cases.Add((min, max, result));
        }
        if (stream.ReadBoolean())
        {
            DefaultCase = BaseExpr.Deserialize<Expr<T>>(stream);
        }
    }

    public override T Eval(EvalContext ctx)
    {
        float value = Value.Eval(ctx);
        foreach (var (min, max, result) in Cases)
        {
            if (value >= min && value <= max)
                return result.Eval(ctx);
        }
        if (DefaultCase != null)
            return DefaultCase.Eval(ctx);
        throw new Exception("No cases matched and no default case provided in SwitchRangeExpr");
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Value.ToBytes(stream);
        stream.WriteInt32(Cases.Count);
        foreach (var (min, max, result) in Cases)
        {
            stream.WriteFloat(min);
            stream.WriteFloat(max);
            result.ToBytes(stream);
        }
        stream.WriteBoolean(DefaultCase != null);
        if (DefaultCase != null)
            DefaultCase.ToBytes(stream);
    }
}

public class AllConditionExpr : Expr<bool>
{

    public readonly Expr<bool> Condition;
    public readonly ArrayExpr<object> Variables;

    public AllConditionExpr(Expr<bool> condition, ArrayExpr<object> variables)
    {
        Condition = condition;
        Variables = variables;
    }
    public AllConditionExpr(Stream stream)
    {
        Condition = BaseExpr.Deserialize<Expr<bool>>(stream);
        Variables = BaseExpr.Deserialize<ArrayExpr<object>>(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        foreach (var element in Variables.Eval(ctx))
        {
            var newVariables = new object[ctx.Variables.Length+1];
            for (int i = 0; i < ctx.Variables.Length; i++)
                newVariables[i+1] = ctx.Variables[i];
            newVariables[0] = element!;
            if (!Condition.Eval(ctx.WithVariables(newVariables)))
                return false;
        }
        return true;
    }

    [ExprOp(ExprCategory.Condition, "all")]
    [ExprParam("condition", typeof(bool), Required = true, Description = "The condition to check for all elements. The current element will be available as variable 0 in the condition context.")]
    [ExprParam("variables", typeof(object[]), Required = true, Description = "The array of elements to check.")]
    public static Expr<bool> CompileAll(JsonElement obj)
    {
        var condition = ExpressionCompiler.Compile<bool>(obj.GetProperty("condition"));
        var variables = ExpressionCompiler.CompileArray<object>(obj.GetProperty("variables"));
        return new AllConditionExpr(condition, variables);
    }
}
public class AnyConditionExpr : Expr<bool>
{

    public readonly Expr<bool> Condition;
    public readonly ArrayExpr<object> Variables;

    public AnyConditionExpr(Expr<bool> condition, ArrayExpr<object> variables)
    {
        Condition = condition;
        Variables = variables;
    }
    public AnyConditionExpr(Stream stream)
    {
        Condition = BaseExpr.Deserialize<Expr<bool>>(stream);
        Variables = BaseExpr.Deserialize<ArrayExpr<object>>(stream);
    }


    public override bool Eval(EvalContext ctx)
    {
        foreach (var element in Variables.Eval(ctx))
        {
            var newVariables = new object[ctx.Variables.Length+1];
            for (int i = 0; i < ctx.Variables.Length; i++)
                newVariables[i+1] = ctx.Variables[i];
            newVariables[0] = element;
            if (Condition.Eval(ctx.WithVariables(newVariables)))
                return true;
        }
        return false;
    }

    [ExprOp(ExprCategory.Condition, "any")]
    [ExprParam("condition", typeof(bool), Required = true, Description = "The condition to check for any element. The current element will be available as variable 0 in the condition context.")]
    [ExprParam("variables", typeof(object[]), Required = true, Description = "The array of elements to check.")]
    public static Expr<bool> CompileAny(JsonElement obj)
    {
        var condition = ExpressionCompiler.Compile<bool>(obj.GetProperty("condition"));
        var variables = ExpressionCompiler.CompileArray<object>(obj.GetProperty("variables"));
        return new AnyConditionExpr(condition, variables);
    }
}
public sealed class EqualConditionExpr : Expr<bool>
{
    public readonly BaseExpr Left;
    public readonly BaseExpr Right;

    public EqualConditionExpr(BaseExpr left, BaseExpr right)
    {
        Left = left;
        Right = right;
    }

    [ExprOp(ExprCategory.Condition, "=", "==")]
    [ExprParam("left", typeof(object), Required = true)]
    [ExprParam("right", typeof(object), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new EqualConditionExpr(
            ExpressionCompiler.Compile<object>(obj.GetProperty("left")),
            ExpressionCompiler.Compile<object>(obj.GetProperty("right")));
    public EqualConditionExpr(Stream stream)
    {
        Left = BaseExpr.Deserialize(stream);
        Right = BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Object.Equals(Left.BaseEval(ctx), Right.BaseEval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
public sealed class NotEqualConditionExpr : Expr<bool>
{
    public readonly BaseExpr Left;
    public readonly BaseExpr Right;

    public NotEqualConditionExpr(BaseExpr left, BaseExpr right)
    {
        Left = left;
        Right = right;
    }

    [ExprOp(ExprCategory.Condition, "!=")]
    [ExprParam("left", typeof(object), Required = true)]
    [ExprParam("right", typeof(object), Required = true)]
    public static Expr<bool> CompileOp(JsonElement obj)
        => new NotEqualConditionExpr(
            ExpressionCompiler.Compile<object>(obj.GetProperty("left")),
            ExpressionCompiler.Compile<object>(obj.GetProperty("right")));
    public NotEqualConditionExpr(Stream stream)
    {
        Left = BaseExpr.Deserialize(stream);
        Right = BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return Left.BaseEval(ctx) != Right.BaseEval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
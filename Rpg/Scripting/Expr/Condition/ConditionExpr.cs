using System.Text.Json;

namespace Rpg.Scripting;

public sealed class ConstConditionExpr : Expr<bool>
{
    public readonly bool Value;
    public ConstConditionExpr(bool value) => Value = value;

    [ExprOp("true", Description = "The constant true.")]
    public static Expr<bool> True() => new ConstConditionExpr(true);

    [ExprOp("false", Description = "The constant false.")]
    public static Expr<bool> False() => new ConstConditionExpr(false);
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

    [ExprOp("random", "rand", Description = "True with the given probability.")]
    public static Expr<bool> Op(
        [Doc("Probability between 0 and 1; defaults to 0.5")] Expr<float>? probability = null)
        => new RandomConditionExpr(probability ?? new ConstNumberExpr(0.5f));
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

    public readonly LambdaExpr<bool> Condition;
    public readonly ArrayExpr<object> Variables;

    public AllConditionExpr(LambdaExpr<bool> condition, ArrayExpr<object> variables)
    {
        Condition = condition;
        Variables = variables;
    }
    public AllConditionExpr(Stream stream)
    {
        Condition = BaseExpr.Deserialize<LambdaExpr<bool>>(stream);
        Variables = BaseExpr.Deserialize<ArrayExpr<object>>(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        foreach (var element in Variables.Eval(ctx))
        {
            if (!Condition.Invoke(ctx, element))
                return false;
        }
        return true;
    }

    [ExprOp("all", Description = "True when the condition holds for every element.")]
    public static Expr<bool> Op(
        [Doc("Elements to test")] ArrayExpr<object> variables,
        [Doc("Test applied to each element")] LambdaExpr<bool> condition)
        => new AllConditionExpr(condition, variables);
}
public class AnyConditionExpr : Expr<bool>
{

    public readonly LambdaExpr<bool> Condition;
    public readonly ArrayExpr<object> Variables;

    public AnyConditionExpr(LambdaExpr<bool> condition, ArrayExpr<object> variables)
    {
        Condition = condition;
        Variables = variables;
    }
    public AnyConditionExpr(Stream stream)
    {
        Condition = BaseExpr.Deserialize<LambdaExpr<bool>>(stream);
        Variables = BaseExpr.Deserialize<ArrayExpr<object>>(stream);
    }


    public override bool Eval(EvalContext ctx)
    {
        foreach (var element in Variables.Eval(ctx))
        {
            if (Condition.Invoke(ctx, element))
                return true;
        }
        return false;
    }

    [ExprOp("any", Description = "True when the condition holds for at least one element.")]
    public static Expr<bool> Op(
        [Doc("Elements to test")] ArrayExpr<object> variables,
        [Doc("Test applied to each element")] LambdaExpr<bool> condition)
        => new AnyConditionExpr(condition, variables);
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

    [ExprOp("=", "==", Description = "True when both sides are equal.")]
    public static Expr<bool> Op(Expr<object> left, Expr<object> right)
        => new EqualConditionExpr(left, right);
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

    [ExprOp("!=", Description = "True when the two sides differ.")]
    public static Expr<bool> Op(Expr<object> left, Expr<object> right)
        => new NotEqualConditionExpr(left, right);
    public NotEqualConditionExpr(Stream stream)
    {
        Left = BaseExpr.Deserialize(stream);
        Right = BaseExpr.Deserialize(stream);
    }

    public override bool Eval(EvalContext ctx)
    {
        return !Object.Equals(Left.BaseEval(ctx), Right.BaseEval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Left.ToBytes(stream);
        Right.ToBytes(stream);
    }
}
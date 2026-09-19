using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace Rpg.Scripting;

public class NoReturn {}
public abstract class EffectExpr : Expr<NoReturn>
{
    public abstract void EvalEffect(EvalContext ctx);
    public override NoReturn Eval(EvalContext ctx)
    {
        EvalEffect(ctx);
        return null!;
    }
}
/// <summary>
/// A list of effects, yielded <em>unevaluated</em>.
/// <para>
/// This is why an array of effects is not a <see cref="ConstArrayExpr{T}"/>: that one evaluates its
/// items to produce the sequence, which for effects means running them all and handing the consumer
/// a list of nulls. <see cref="CompositeEffectExpr"/> wants the effects themselves, to run in order.
/// </para>
/// </summary>
public class EffectArray : ArrayExpr<EffectExpr>
{
    public EffectExpr[] Effects { get; set; }
    public EffectArray(params EffectExpr[] effects)
    {
        Effects = effects;
    }
    public EffectArray(Stream stream)
    {
        int length = stream.ReadInt32();
        Effects = new EffectExpr[length];
        for (int i = 0; i < length; i++)
            Effects[i] = (EffectExpr)BaseExpr.Deserialize(stream);
    }

    public override IEnumerable<EffectExpr> Eval(EvalContext ctx)
    {
        return Effects;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(Effects.Length);
        foreach (var effect in Effects)
            effect.ToBytes(stream);
    }
}
public sealed class NoEffectExpr : EffectExpr
{
    public NoEffectExpr() {}
    public NoEffectExpr(Stream stream) {}

    [ExprOp("null", "nop", "noeffect", "no_effect", Description = "Does nothing.")]
    public static EffectExpr Op() => new NoEffectExpr();

    public override void EvalEffect(EvalContext ctx)
    {
    }
}
// Basically CastExpr but for effects
public sealed class CallExprEffect : EffectExpr
{
    public readonly BaseExpr Expr;

    public CallExprEffect(BaseExpr expr)
    {
        Expr = expr;
    }
    public CallExprEffect(Stream stream)
    {
        Expr = BaseExpr.Deserialize(stream);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        Expr.BaseEval(ctx);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Expr.ToBytes(stream);
    }
}
public sealed class CompositeEffectExpr : EffectExpr
{
    public readonly ArrayExpr<EffectExpr> Effects;
    public CompositeEffectExpr(ArrayExpr<EffectExpr> effects) => Effects = effects;

    [ExprOp("composite", Description = "Runs several effects in order.")]
    public static EffectExpr Op([Doc("Effects to run in sequence")] ArrayExpr<EffectExpr> effects)
        => new CompositeEffectExpr(effects);
    public CompositeEffectExpr(Stream stream)
    {
        Effects = (ArrayExpr<EffectExpr>)BaseExpr.Deserialize(stream);
    }
    public override void EvalEffect(EvalContext ctx)
    {
        foreach (var effect in Effects.Eval(ctx))
        {
            effect.EvalEffect(ctx);
        }
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Effects.ToBytes(stream);
    }
}

public sealed class SetVariableEffectExpr : EffectExpr
{
    public readonly int VariableID;
    public readonly BaseExpr ValueExpr;

    public SetVariableEffectExpr(int variableID, BaseExpr valueExpr)
    {
        VariableID = variableID;
        ValueExpr = valueExpr;
    }
    public SetVariableEffectExpr(Stream stream)
    {
        VariableID = stream.ReadInt32();
        ValueExpr = (Expr<JsonElement>)BaseExpr.Deserialize(stream);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        var value = ValueExpr.BaseEval(ctx);
        ctx.Variables[VariableID] = value!;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteInt32(VariableID);
        ValueExpr.ToBytes(stream);
    }

    // These four differed only in the declared type of the value they store; each now says so in
    // its signature instead of repeating the same property reads. The variable index was always
    // evaluated at compile time, so it is declared as a literal rather than an expression.
    [ExprOp("set_const", "const_set", "setconst", "constset",
            Description = "Stores a literal value in a variable slot.")]
    public static SetVariableEffectExpr SetConst(
        [Doc("Index of the variable slot to write")] int variable,
        [Doc("Value to store")] Expr<object> value)
        => new SetVariableEffectExpr(variable, value);

    [ExprOp("set_number", "number_set", "setnumber", "numberset",
            Description = "Stores a number in a variable slot.")]
    public static SetVariableEffectExpr SetNumber(
        [Doc("Index of the variable slot to write")] int variable,
        [Doc("Number to store")] Expr<float> value)
        => new SetVariableEffectExpr(variable, value);

    [ExprOp("set_string", "string_set", "setstring", "stringset",
            Description = "Stores a string in a variable slot.")]
    public static SetVariableEffectExpr SetString(
        [Doc("Index of the variable slot to write")] int variable,
        [Doc("String to store")] Expr<string> value)
        => new SetVariableEffectExpr(variable, value);

    [ExprOp("set_condition", "condition_set", "setcondition", "conditionset",
            Description = "Stores a condition result in a variable slot.")]
    public static SetVariableEffectExpr SetCondition(
        [Doc("Index of the variable slot to write")] int variable,
        [Doc("Condition to store")] Expr<bool> value)
        => new SetVariableEffectExpr(variable, value);
}

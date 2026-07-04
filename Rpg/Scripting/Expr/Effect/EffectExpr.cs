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
}
public sealed class NoEffectExpr : EffectExpr
{
    public NoEffectExpr() {}
    public NoEffectExpr(Stream stream) {}

    [ExprOp(ExprCategory.Effect, "null", "nop", "noeffect", "no_effect")]
    public static EffectExpr CompileOp(JsonElement obj) => new NoEffectExpr();

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

    [ExprOp(ExprCategory.Effect, "composite")]
    [ExprParam("effects", typeof(List<EffectExpr>), Required = true)]
    public static EffectExpr CompileOp(JsonElement obj)
        => new CompositeEffectExpr(ExpressionCompiler.CompileArray<EffectExpr>(obj.GetProperty("effects")));
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

    [ExprOp(ExprCategory.Effect, "set_const", "const_set", "setconst", "constset")]
    [ExprParam("variable", typeof(float), Required = true, Description = "The ID of the variable")]
    [ExprParam("value", typeof(object), Required = true)]
    public static SetVariableEffectExpr Compile(JsonElement json)
    {
        int variableID = (int)ExpressionCompiler.Compile<float>(json.GetProperty("variable")).Eval();
        var valEl = json.GetProperty("value");
        if (valEl.ValueKind == JsonValueKind.True || valEl.ValueKind == JsonValueKind.False)
        {
            // Special case for boolean literals since we want to support them directly as "value"
            return new SetVariableEffectExpr(variableID, new ConstConditionExpr(valEl.GetBoolean()));
        }
        if (valEl.ValueKind == JsonValueKind.Number)
        {
            // Special case for number literals since we want to support them directly as "value"
            return new SetVariableEffectExpr(variableID, new ConstNumberExpr(valEl.GetSingle()));
        }
        if (valEl.ValueKind == JsonValueKind.String)
        {
            // Special case for string literals since we want to support them directly as "value"
            return new SetVariableEffectExpr(variableID, new StringLiteralExpr(valEl.GetString()!));
        }
        
        throw new Exception("Invalid value for SetVariableEffectExpr: " + valEl);
    }

    [ExprOp(ExprCategory.Effect, "set_number", "number_set", "setnumber", "numberset")]
    [ExprParam("variable", typeof(float), Required = true, Description = "The ID of the variable")]
    [ExprParam("value", typeof(float), Required = true, Description = "The number expression to evaluate and set the variable to")]
    public static SetVariableEffectExpr CompileNumber(JsonElement json)
    {
        int variableID = (int)ExpressionCompiler.Compile<float>(json.GetProperty("variable")).Eval();
        var valueExpr = ExpressionCompiler.Compile<float>(json.GetProperty("value"));
        return new SetVariableEffectExpr(variableID, valueExpr);
    }

    [ExprOp(ExprCategory.Effect, "set_string", "string_set", "setstring", "stringset")]
    [ExprParam("variable", typeof(float), Required = true, Description = "The ID of the variable")]
    [ExprParam("value", typeof(string), Required = true, Description = "The string expression to evaluate and set the variable to")]
    public static SetVariableEffectExpr CompileString(JsonElement json)
    {
        int variableID = (int)ExpressionCompiler.Compile<float>(json.GetProperty("variable")).Eval();
        var valueExpr = ExpressionCompiler.Compile<string>(json.GetProperty("value"));
        return new SetVariableEffectExpr(variableID, valueExpr);
    }

    [ExprOp(ExprCategory.Effect, "set_condition", "condition_set", "setcondition", "conditionset")]
    [ExprParam("variable", typeof(float), Required = true, Description = "The ID of the variable")]
    [ExprParam("value", typeof(bool), Required = true, Description = "The condition expression to evaluate and set the variable to")]
    public static SetVariableEffectExpr CompileCondition(JsonElement json)
    {
        int variableID = (int)ExpressionCompiler.Compile<float>(json.GetProperty("variable")).Eval();
        var valueExpr = ExpressionCompiler.Compile<bool>(json.GetProperty("value"));
        return new SetVariableEffectExpr(variableID, valueExpr);
    }
}
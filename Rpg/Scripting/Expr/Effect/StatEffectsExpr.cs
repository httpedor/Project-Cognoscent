using System.Text.Json;
using Rpg.Entities;

namespace Rpg.Scripting;

public class SetStatEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly string StatName;
    public readonly Expr<float> Value;
    public SetStatEffect(string statName, Expr<Entity?> target, Expr<float> value)
    {
        StatName = statName;
        Target = target;
        Value = value;
    }

    [ExprOp(ExprCategory.Effect, "setstat", "set_stat")]
    [ExprParam("stat", "string", Required = true, Description = "Name of the stat to set")]
    [ExprParam("value", "numberExpr", Required = true)]
    [ExprParam("target", "selectorExpr", Required = true)]
    public static EffectExpr CompileSetStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.CompileNumber(obj.GetProperty("value"));
        var target = ExpressionCompiler.CompileSelector(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, value);
    }

    [ExprOp(ExprCategory.Effect, "addstat", "add_stat")]
    [ExprParam("stat", "string", Required = true)]
    [ExprParam("value", "numberExpr", Required = true)]
    [ExprParam("target", "selectorExpr", Required = true)]
    public static EffectExpr CompileAddStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.CompileNumber(obj.GetProperty("value"));
        var target = ExpressionCompiler.CompileSelector(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new AddExpr([new StatExpr(statName, target, new ConstNumberExpr(0)), value]));
    }

    [ExprOp(ExprCategory.Effect, "substat", "sub_stat", "remove_stat", "removestat")]
    [ExprParam("stat", "string", Required = true)]
    [ExprParam("value", "numberExpr", Required = true)]
    [ExprParam("target", "selectorExpr", Required = true)]
    public static EffectExpr CompileSubStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.CompileNumber(obj.GetProperty("value"));
        var target = ExpressionCompiler.CompileSelector(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new SubExpr([new StatExpr(statName, target, new ConstNumberExpr(0)), value]));
    }

    [ExprOp(ExprCategory.Effect, "mulstat", "mul_stat")]
    [ExprParam("stat", "string", Required = true)]
    [ExprParam("value", "numberExpr", Required = true)]
    [ExprParam("target", "selectorExpr", Required = true)]
    public static EffectExpr CompileMulStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.CompileNumber(obj.GetProperty("value"));
        var target = ExpressionCompiler.CompileSelector(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new MulExpr([new StatExpr(statName, target, new ConstNumberExpr(1)), value]));
    }

    [ExprOp(ExprCategory.Effect, "divstat", "div_stat")]
    [ExprParam("stat", "string", Required = true)]
    [ExprParam("value", "numberExpr", Required = true)]
    [ExprParam("target", "selectorExpr", Required = true)]
    public static EffectExpr CompileDivStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.CompileNumber(obj.GetProperty("value"));
        var target = ExpressionCompiler.CompileSelector(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new DivExpr([new StatExpr(statName, target, new ConstNumberExpr(1)), value]));
    }
    public override void Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var stats = entity.Stats;
        if (stats == null)
            return;
        var stat = stats.GetStat(StatName);
        if (stat == null)
            throw new Exception($"Stat '{StatName}' not found on entity '{entity.Name}'.");
        var val = Value.Eval(ctx);
        stat.BaseValue = val;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(StatName);
        Target.ToBytes(stream);
        Value.ToBytes(stream);
    }
}
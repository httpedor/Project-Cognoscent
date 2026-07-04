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
    [ExprParam("stat", typeof(string), Required = true, Description = "Name of the stat to set")]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr CompileSetStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.Compile<float>(obj.GetProperty("value"));
        var target = ExpressionCompiler.Compile<Entity>(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, value);
    }

    [ExprOp(ExprCategory.Effect, "addstat", "add_stat")]
    [ExprParam("stat", typeof(string), Required = true)]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr CompileAddStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.Compile<float>(obj.GetProperty("value"));
        var target = ExpressionCompiler.Compile<Entity>(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new AddExpr([new StatExpr(statName, target, new ConstNumberExpr(0)), value]));
    }

    [ExprOp(ExprCategory.Effect, "substat", "sub_stat", "remove_stat", "removestat")]
    [ExprParam("stat", typeof(string), Required = true)]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr CompileSubStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.Compile<float>(obj.GetProperty("value"));
        var target = ExpressionCompiler.Compile<Entity>(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new SubExpr([new StatExpr(statName, target, new ConstNumberExpr(0)), value]));
    }

    [ExprOp(ExprCategory.Effect, "mulstat", "mul_stat")]
    [ExprParam("stat", typeof(string), Required = true)]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr CompileMulStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.Compile<float>(obj.GetProperty("value"));
        var target = ExpressionCompiler.Compile<Entity>(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new MulExpr([new StatExpr(statName, target, new ConstNumberExpr(1)), value]));
    }

    [ExprOp(ExprCategory.Effect, "divstat", "div_stat")]
    [ExprParam("stat", typeof(string), Required = true)]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr CompileDivStat(JsonElement obj)
    {
        string statName = obj.GetProperty("stat").GetString()!;
        var value = ExpressionCompiler.Compile<float>(obj.GetProperty("value"));
        var target = ExpressionCompiler.Compile<Entity>(obj.GetProperty("target"));
        return new SetStatEffect(statName, target, new DivExpr([new StatExpr(statName, target, new ConstNumberExpr(1)), value]));
    }
    public override void EvalEffect(EvalContext ctx)
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

public sealed class StatModifierAddEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<string> StatName;
    public readonly Expr<string> Id; // Optional ID for the modifier, used to remove it later if needed
    public readonly Expr<float> Value;
    public readonly Expr<StatModifierType> ModifierType; // "Flat", "Percent", etc.

    public StatModifierAddEffect(Expr<string> statName, Expr<StatModifierType> modifierType, Expr<Entity?> target, Expr<float> value, Expr<string>? id = null)
    {
        StatName = statName;
        ModifierType = modifierType;
        Target = target;
        Value = value;
        Id = id ?? new StringLiteralExpr(Guid.NewGuid().ToString()); // Generate a random ID if not provided
    }

    [ExprOp(ExprCategory.Effect, "modifystat", "modify_stat")]
    [ExprParam("stat", typeof(string), Required = true)]
    [ExprParam("modifier_type", typeof(StatModifierType), Required = true, Description = "How the modifier value is applied to the stat. E.g. \"Flat\", \"Percent\", etc.")]
    [ExprParam("value", typeof(float), Required = true)]
    [ExprParam("target", typeof(Entity), Required = true)]
    [ExprParam("id", typeof(string), Required = false, Description = "Optional ID for the modifier, used to remove it later if needed")]
    public static EffectExpr Compile(JsonElement obj)
    {
        var statName = ExpressionCompiler.Compile<string>(obj.GetProperty("stat"));
        var modifierType = ExpressionCompiler.Compile<StatModifierType>(obj.GetProperty("modifier_type"));
        var value = ExpressionCompiler.Compile<float>(obj.GetProperty("value"));
        var target = ExpressionCompiler.Compile<Entity?>(obj.GetProperty("target"));
        var id = obj.TryGetProperty("id", out var idElement) ? ExpressionCompiler.Compile<string>(idElement) : null;
        return new StatModifierAddEffect(statName, modifierType, target, value, id);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var stats = entity.Stats;
        if (stats == null)
            return;
        var statName = StatName.Eval(ctx);
        var statId = Id.Eval(ctx);
        var stat = stats.GetStat(statName);
        if (stat == null)
            throw new Exception($"Stat '{statName}' not found on entity '{entity.Name}'.");
        var modType = ModifierType.Eval(ctx);
        var val = Value.Eval(ctx);
        stat.AddModifier(new StatModifier(statId, val, modType));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        StatName.ToBytes(stream);
        Target.ToBytes(stream);

        Id.ToBytes(stream);
        ModifierType.ToBytes(stream);
        Value.ToBytes(stream);
    }
}
public sealed class StatModifierRemoveEffect : EffectExpr
{
    public readonly Expr<Entity?> Target;
    public readonly Expr<string> StatName;
    public readonly Expr<string> Id;

    public StatModifierRemoveEffect(Expr<string> statName, Expr<string> id, Expr<Entity?> target)
    {
        StatName = statName;
        Id = id;
        Target = target;
    }

    [ExprOp(ExprCategory.Effect, "removestatmodifier", "remove_stat_modifier")]
    [ExprParam("stat", typeof(string), Required = true)]
    [ExprParam("id", typeof(string), Required = true, Description = "ID of the modifier to remove")]
    [ExprParam("target", typeof(Entity), Required = true)]
    public static EffectExpr Compile(JsonElement obj)
    {
        var statName = ExpressionCompiler.Compile<string>(obj.GetProperty("stat"));
        var id = ExpressionCompiler.Compile<string>(obj.GetProperty("id"));
        var target = ExpressionCompiler.Compile<Entity?>(obj.GetProperty("target"));
        return new StatModifierRemoveEffect(statName, id, target);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);
        if (entity == null)
            return;
        var stats = entity.Stats;
        if (stats == null)
            return;
        var statName = StatName.Eval(ctx);
        var statId = Id.Eval(ctx);
        var stat = stats.GetStat(statName);
        if (stat == null)
            throw new Exception($"Stat '{statName}' not found on entity '{entity.Name}'.");
        stat.RemoveModifier(statId);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        StatName.ToBytes(stream);
        Id.ToBytes(stream);
        Target.ToBytes(stream);
    }
}
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

    [ExprOp("setstat", "set_stat", Description = "Sets a stat's base value.")]
    public static EffectExpr SetStat(
        [Doc("Name of the stat to set")] string stat,
        [Doc("New base value")] Expr<float> value,
        [Doc("Entity to modify")] Expr<Entity?> target)
        => new SetStatEffect(stat, target, value);

    [ExprOp("addstat", "add_stat", Description = "Adds to a stat's base value.")]
    public static EffectExpr AddStat(
        [Doc("Name of the stat to change")] string stat,
        [Doc("Amount to add")] Expr<float> value,
        [Doc("Entity to modify")] Expr<Entity?> target)
        => new SetStatEffect(stat, target,
            new AddExpr([new StatExpr(stat, target, new ConstNumberExpr(0)), value]));

    [ExprOp("substat", "sub_stat", "remove_stat", "removestat",
            Description = "Subtracts from a stat's base value.")]
    public static EffectExpr SubStat(
        [Doc("Name of the stat to change")] string stat,
        [Doc("Amount to subtract")] Expr<float> value,
        [Doc("Entity to modify")] Expr<Entity?> target)
        => new SetStatEffect(stat, target,
            new SubExpr([new StatExpr(stat, target, new ConstNumberExpr(0)), value]));

    [ExprOp("mulstat", "mul_stat", Description = "Multiplies a stat's base value.")]
    public static EffectExpr MulStat(
        [Doc("Name of the stat to change")] string stat,
        [Doc("Factor to multiply by")] Expr<float> value,
        [Doc("Entity to modify")] Expr<Entity?> target)
        => new SetStatEffect(stat, target,
            new MulExpr([new StatExpr(stat, target, new ConstNumberExpr(1)), value]));

    [ExprOp("divstat", "div_stat", Description = "Divides a stat's base value.")]
    public static EffectExpr DivStat(
        [Doc("Name of the stat to change")] string stat,
        [Doc("Divisor")] Expr<float> value,
        [Doc("Entity to modify")] Expr<Entity?> target)
        => new SetStatEffect(stat, target,
            new DivExpr([new StatExpr(stat, target, new ConstNumberExpr(1)), value]));
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

    [ExprOp("modifystat", "modify_stat", Description = "Attaches a modifier to a stat.")]
    public static EffectExpr Op(
        [Doc("Name of the stat to modify")] Expr<string> stat,
        [Doc("How the value applies, e.g. Flat or Percent")] Expr<StatModifierType> modifier_type,
        [Doc("Modifier value")] Expr<float> value,
        [Doc("Entity to modify")] Expr<Entity?> target,
        [Doc("Id used to remove the modifier later; generated when omitted")] Expr<string>? id = null)
        => new StatModifierAddEffect(stat, modifier_type, target, value, id);

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

    [ExprOp("removestatmodifier", "remove_stat_modifier",
            Description = "Removes a previously attached stat modifier.")]
    public static EffectExpr Op(
        [Doc("Name of the modified stat")] Expr<string> stat,
        [Doc("Id of the modifier to remove")] Expr<string> id,
        [Doc("Entity to modify")] Expr<Entity?> target)
        => new StatModifierRemoveEffect(stat, id, target);

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
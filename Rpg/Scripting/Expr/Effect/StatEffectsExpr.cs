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
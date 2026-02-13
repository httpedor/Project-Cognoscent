using Rpg.Entities;

namespace Rpg.Scripting;

public sealed class StatExpr : NumberExpr
{
    public readonly string StatName;
    public readonly NumberExpr DefaultValue;
    public readonly SelectorExpr Target;

    public StatExpr(string statName, SelectorExpr target, NumberExpr defaultValue)
    {
        StatName = statName;
        DefaultValue = defaultValue;
        Target = target;
    }

    public override float Eval(EvalContext ctx)
    {
        var entity = Target.Eval(ctx);

        var defaultValue = DefaultValue.Eval(ctx);
        if (entity == null)
            return defaultValue;
        var stats = entity.Stats;
        if (stats == null)
            return defaultValue;
        return stats.GetStatValue(StatName, defaultValue);
    }
}
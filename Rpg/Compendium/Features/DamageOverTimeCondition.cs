using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Health;
using Rpg.Scripting;

namespace Rpg.Features;

public class DamageOverTimeCondition : ConditionFeature
{
    private readonly Expr<float> damage;
    private readonly Expr<float> interval; // In seconds
    private readonly Expr<DamageType?> damageType;
    public DamageOverTimeCondition(string id, string name, string description, string icon,
                                    Expr<DamageType?> dt, Expr<float> damage, Expr<float> interval) : base(id, name, description, icon)
    {
        this.damage = damage;
        this.interval = interval;
        damageType = dt;
    }

    public override void OnTick(FeaturesContainer source)
    {
        base.OnTick(source);
        var ctx = new EvalContext(source.Entity);
        var intervalValue = interval.Eval(ctx) * Physics.TicksPerSecond;
        if (intervalValue == 0 || GetTicksSinceStart(source) % intervalValue == 0)
        {
            var dt = damageType.Eval(ctx);
            if (dt == null)
            {
                Logger.LogError($"DamageOverTimeCondition {Id} could not find damage type: {damageType}. Skipping damage application.");
                return;
            }
            var dmg = damage.Eval(ctx);
            foreach (var component in source.Entity.Components)
            {
                if (component is IDamageable damageable)
                    damageable.Damage(new DamageInstance(new DamageSource(dt), dmg));
            }
        }
    }
}
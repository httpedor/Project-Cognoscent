using Rpg.Entities;

namespace Rpg.Health;

public class DamageEvent : CancellableComponentEvent
{
    public IDamageable Damaged;
    public DamageInstance DamageInstance;
    public List<StatModifier> DamageModifiers = new();
    public string Formula = "";
    public DamageEvent(Component component, DamageInstance damageInstance) : base(component)
    {
        if (component is IDamageable damageable)
            Damaged = damageable;
        else
            throw new ArgumentException($"Component {component} is not damageable");
        DamageInstance = damageInstance;
    }
}
public interface IDamageable
{
    public double Damage(DamageInstance damageInstance);
}
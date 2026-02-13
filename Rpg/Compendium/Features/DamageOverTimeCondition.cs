using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Health;

namespace Rpg.Features;

public class DamageOverTimeCondition : ConditionFeature
{
    private readonly string id;
    private readonly string description;
    private readonly float damage;
    private readonly uint interval;
    private readonly DamageType damageType;
    public DamageOverTimeCondition(string id, string name, string description, DamageType dt, float damage = 0, uint interval = 0, uint ticks = 0) : base(ticks)
    {
        this.id = id;
        CustomName = name;
        this.description = description;
        this.damage = damage;
        this.interval = interval;
        damageType = dt;
    }

    public DamageOverTimeCondition(Stream stream) : base(stream)
    {
        id = stream.ReadString();
        description = stream.ReadLongString();
        damage = stream.ReadFloat();
        interval = stream.ReadUInt32();
        damageType = DamageType.FromBytes(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(id);
        stream.WriteLongString(description);
        stream.WriteFloat(damage);
        stream.WriteUInt32(interval);
        damageType.ToBytes(stream);
    }

    public DamageOverTimeCondition WithDamage(float damage)
    {
        return new DamageOverTimeCondition(id, CustomName!, description, damageType, damage, interval, ticks);
    }

    public DamageOverTimeCondition WithInterval(uint ticks)
    {
        return new DamageOverTimeCondition(id, CustomName!, description, damageType, damage, interval, ticks);
    }

    public override void OnTick(FeaturesContainer source)
    {
        base.OnTick(source);

        if (interval == 0 || GetTicksSinceStart(source.Entity) % interval == 0)
        {
            foreach (var component in source.Entity.Components)
            {
                if (component is IDamageable damageable)
                    damageable.Damage(new DamageInstance(new DamageSource(damageType), damage));
            }
        }
    }

    public override string GetId()
    {
        return id;
    }
    public override string GetDescription()
    {
        return description;
    }
}
namespace Rpg;

public class DamageOverTimeCondition : ConditionFeature
{
    private readonly string id;
    private readonly string description;
    private readonly double damage;
    private readonly uint interval;
    private readonly DamageType damageType;
    public DamageOverTimeCondition(string id, string name, string description, DamageType dt, double damage = 0, uint interval = 0, uint ticks = 0) : base(ticks)
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
        damage = stream.ReadDouble();
        interval = stream.ReadUInt32();
        damageType = DamageType.FromBytes(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(id);
        stream.WriteLongString(description);
        stream.WriteDouble(damage);
        stream.WriteUInt32(interval);
        damageType.ToBytes(stream);
    }

    public DamageOverTimeCondition WithDamage(double damage)
    {
        return new DamageOverTimeCondition(id, CustomName!, description, damageType, damage, interval, ticks);
    }
    public DamageOverTimeCondition WithDuration(uint ticks)
    {
        return new DamageOverTimeCondition(id, CustomName!, description, damageType, damage, interval, ticks);
    }

    public DamageOverTimeCondition WithInterval(uint ticks)
    {
        return new DamageOverTimeCondition(id, CustomName!, description, damageType, damage, interval, ticks);
    }

    public override void OnTick(IFeatureContainer entity)
    {
        base.OnTick(entity);

        if (entity is IDamageable damageable && (interval == 0 || GetTicksSinceStart(entity) % interval == 0))
        {
            damageable.Damage(new DamageSource(damageType), damage);
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
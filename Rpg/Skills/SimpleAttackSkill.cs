using Rpg;

namespace Rpg;

public class SimpleAttackSkill : AttackSkill
{
    public readonly DamageType DamageType;
    public readonly string Stat;
    public readonly string? Group;
    public SimpleAttackSkill(string stat, DamageType type, float delay = 100, float cooldown = 0, float staminaUse = 10)
    {
        Stat = stat;
        Group = null;
    }
    public SimpleAttackSkill(string stat, string group, DamageType type, float delay = 100, float cooldown = 0, float staminaUse = 10)
    {
        Stat = stat;
        Group = group;
    }
    public SimpleAttackSkill(Stream data) : base(data)
    {
        Stat = data.ReadString();
        Group = data.ReadByte() == 0 ? null : data.ReadString();
    }

    public override (bool, string?) DoesHit(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target)
    {
        Creature creature;
        switch (target)
        {
            case BodyPart { Owner: null }:
                return (true, null);
            case BodyPart bp:
                creature = bp.Owner;
                break;
            case Creature c:
                creature = c;
                break;
            default:
                return (true, null);
        }
        //100% Hit Chance, only miss if dodge

        float stat = executor.GetStatValue(Stat);
        if (Group != null)
            stat = executor.Body.GetStatByGroup(Group, Stat);
        
        return (false, null);
    }
    
    public override (float damage, DamageType type) GetDamage(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target)
    {
        if (Group != null)
            return (executor.Body.GetStatByGroup(Group, Stat), DamageType);
        return (executor.GetStatValue(Stat), DamageType);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(Stat);
        stream.WriteByte((byte)(Group == null ? 0 : 1));
        if (Group != null)
            stream.WriteString(Group);
    }


}

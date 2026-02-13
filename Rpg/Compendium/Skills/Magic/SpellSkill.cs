namespace Rpg.Magic;

public class SpellSkill : AttackSkill
{
    public override (float damage, DamageType type, string? formula) GetDamage(Creature executor, List<SkillArgument> arguments, ISkillSource source,
        IDamageable target)
    {
        throw new NotImplementedException();
    }
}
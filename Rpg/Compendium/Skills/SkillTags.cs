namespace Rpg.Skills;

public static class SkillTags
{
    public static readonly Tag<Skill> Unparryable = new("unparryable");
    public static readonly Tag<Skill> Melee = new("melee");
    public static readonly Tag<Skill> Projectile = new("projectile");
    public static readonly Tag<Skill> Magic = new("magic");
}
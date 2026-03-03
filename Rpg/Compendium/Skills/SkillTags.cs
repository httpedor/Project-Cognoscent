namespace Rpg.Skills;

public static class SkillTags
{
    public static readonly Tag<Skill> Unparryable = new("unparryable"); // Skills that cannot be parried
    public static readonly Tag<Skill> Melee = new("melee");
    public static readonly Tag<Skill> Projectile = new("projectile"); // Projectile skills create projectiles
    public static readonly Tag<Skill> Magic = new("magic"); // Magic skills are affected by silence
}
namespace Rpg;

public static class Skills
{
    private static Dictionary<string, Skill> skills = new();
    public static IEnumerable<(string id, Skill skill)> All => skills.Select(t => (t.Key, t.Value));

    private static T register<T>(string id, T skill) where T : Skill
    {
        skills[id] = skill;
        return skill;
    }
    public static bool Exists(string id) => skills.ContainsKey(id);
    public static Skill? Get(string id) => skills.GetValueOrDefault(id);

    public static TeleportSkill Teleport = register("teleport", new TeleportSkill());
}
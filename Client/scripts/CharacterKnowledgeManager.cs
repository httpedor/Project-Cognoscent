using Rpg;

namespace TTRpgClient.scripts;

public static class CharacterKnowledgeManager
{
    public static bool KnowsStat(Entity target, Stat stat)
    {
        if (GameManager.IsGm)
            return true;
        if (target is not Creature creature)
            return false;
        return GameManager.OwnsEntity(target);
    }

    public static bool KnowsFeature(Entity target, Feature feature)
    {
        if (GameManager.IsGm)
            return true;
        if (target is not Creature creature)
            return false;
        return GameManager.OwnsEntity(target);
    }
}
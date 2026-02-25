using Rpg;
using Rpg.Entities;
using Rpg.Features;

namespace TTRpgClient.scripts;

public static class CharacterKnowledgeManager
{
    public static bool KnowsStat(Entity target, Stat stat)
    {
        if (GameManager.IsGm)
            return true;
        return GameManager.OwnsEntity(target);
    }

    public static bool KnowsFeature(Entity target, Feature feature)
    {
        if (GameManager.IsGm)
            return true;
        if (GameManager.OwnsEntity(target))
            return feature.CanBeSeenBy(target);
        return false;
    }
}
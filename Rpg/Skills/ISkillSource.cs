using System.ComponentModel;
using Rpg.Entities;

namespace Rpg;

public interface ISkillSource
{
    Creature? Creature {
        get;
    }
    IEnumerable<Skill> Skills {
        get;
    }

    string Name {
        get;
    }

    public float GetStat(string id, float defaultValue = 0);

    public float GetStatCreature(string id, float def = 0)
    {
        if (Creature == null)
            return def;
        return Creature.GetStatValueOrDefault(id, def);
    }
}
using System.ComponentModel;
using Rpg.Entities;

namespace Rpg;

public interface ISkillSource
{
    public abstract IEnumerable<Skill> Skills {
        get;
    }

    public abstract string Name {
        get;
    }
}
using Rpg.Entities.Components;
using Rpg.Skills;

namespace Rpg.Entities.Interfaces;

public interface ISkillProvider
{
    public IEnumerable<Skill> GetSkillsFor(SkillExecutor executor);
}
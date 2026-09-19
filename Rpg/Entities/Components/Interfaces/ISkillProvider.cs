using Rpg.Entities.Components;
using Rpg.Skills;

namespace Rpg.Entities.Interfaces;

[RegisterComponent]
public interface ISkillProvider
{
    public IEnumerable<Skill> GetSkillsFor(SkillExecutor executor);
}
using System.Numerics;
using System.Text.Json.Nodes;

namespace Rpg.Animation;

public class MeleeAnimation : SkillAnimation
{
    public Vector3 Target;
    public MeleeAnimation(SkillData skillData) : base(skillData)
    {
        Target = skillData.Arguments[0] switch
        {
            BodyPartSkillArgument bpsa => bpsa.Part?.Owner?.Position ?? Vector3.Zero,
            PositionSkillArgument psa  => psa.Position,
            EntitySkillArgument esa => esa.Entity?.Position ?? Vector3.Zero,
            _ => Target
        };
    }
    public MeleeAnimation(SkillData skillData, JsonObject json) : base(skillData, json)
    {
        
    }
}
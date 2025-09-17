using System.Text.Json.Nodes;

namespace Rpg.Animation;

public class SkillAnimation
{
    protected SkillData _skillData;
    protected SkillAnimation(SkillData skillData)
    {
        _skillData = skillData;
    }
    protected SkillAnimation(SkillData skillData, JsonObject json) : this(skillData)
    {
        
    }
    public static SkillAnimation? FromJson(SkillData skill, JsonObject json)
    {
        if (!json.ContainsKey("type"))
            return null;

        return json["type"]!.ToString() switch
        {
            "melee" => new MeleeAnimation(skill, json),
            _ => null
        };
    }
}
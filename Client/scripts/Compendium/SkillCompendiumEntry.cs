using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg;
using Rpg.Skills;

namespace TTRpgClient.scripts.ui;

public partial class SkillCompendiumEntry : CodeCompendiumEntry
{
    public SkillCompendiumEntry(string entryId, JsonElement json) : base(Compendium.GetFolderName<Skill>(), entryId, json)
    {
    }
}
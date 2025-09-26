using System.Text.Json.Nodes;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class SkillCompendiumEntry : CodeCompendiumEntry
{
    public SkillCompendiumEntry(string entryId, JsonObject json) : base(Compendium.GetFolderName<Skill>(), entryId, json)
    {
        switch (json["type"]!.GetValue<string>())
        {
            
        }
    }
}
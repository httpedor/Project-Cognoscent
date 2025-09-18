using System.Text.Json.Nodes;
using Rpg;
using TTRpgClient.scripts.ui;

public partial class FeatureCompendiumEntry : CodeCompendiumEntry
{
    private static TabInfo[] tabs =
    [
        new TabInfo()
        {
            JsonKey = ""
        }
    ];
    public FeatureCompendiumEntry(string entryId, JsonObject json) : base(Compendium.GetFolderName<Feature>(), entryId, json, tabs)
    {
    }
}
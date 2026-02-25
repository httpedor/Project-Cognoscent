using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class NoteCompendiumEntry(string entryId, JsonElement json)
    : CompendiumEntry(Compendium.GetFolderName<string>(), entryId, json)
{
    protected override void OnClick()
    {
        Modal.OpenMultiline("Nota " + entryId, text =>
        {
            JsonObject mutJson = json.ToNode()!.AsObject();
            mutJson["text"] = text;
            json = mutJson.ToElement();
            NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, json));
        }, json.GetProperty("text").GetString()!);
    }
}
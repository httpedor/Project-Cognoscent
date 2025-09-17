using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class NoteCompendiumEntry(string entryId, JsonObject json)
    : CompendiumEntry(Compendium.GetFolderName<string>(), entryId, json)
{
    protected override void OnClick()
    {
        Modal.OpenMultiline("Nota " + entryId, text =>
        {
            json["text"] = text;
            NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, json));
        }, json["text"]!.GetValue<string>());
    }
}
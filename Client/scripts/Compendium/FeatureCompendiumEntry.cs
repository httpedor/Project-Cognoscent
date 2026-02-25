using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg;
using Rpg.Features;
using Rpg.Health;
using TTRpgClient.scripts;
using TTRpgClient.scripts.ui;

public partial class FeatureCompendiumEntry : CompendiumEntry
{
    public FeatureCompendiumEntry(string entryId, JsonElement json) : base(Compendium.GetFolderName<Feature>(), entryId, json)
    {
    }

    private JsonObject CopyObjBase()
    {
        JsonObject newObj = new JsonObject
        {
            ["type"] = json.GetProperty("type").GetString(),
            ["name"] = json.GetProperty("name")!.GetString(),
            ["description"] = json.GetProperty("description")!.GetString(),
            ["icon"] = json.GetProperty("icon")!.GetString()
        };
        return newObj;
    }

    protected override void OnClick()
    {
        switch (json.GetProperty("type").ToString())
        {
            case "arbitrary":
            {
                base.OnClick();
                break;
            }
            case "damage_over_time":
            {
                Modal.OpenFormDialog("DOTFeature " + entryId, (result) =>
                {
                    var newObj = CopyObjBase();
                    newObj["damageType"] = result.TipoDeDano.Name;
                    newObj["damage"] = result.Dano;
                    newObj["interval"] = result.Intervalo;
                    
                    NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, newObj.ToElement()));
                }, (TipoDeDano: Compendium.GetDefaultEntry<DamageType>(), Dano: 1f, Intervalo: json.GetPropertyOrNull("interval")?.GetInt32() ?? 0));
                break;
            }
        }
    }

    public override void AddGMContextMenuOptions()
    {
        base.AddGMContextMenuOptions();
        ContextMenu.AddOption("Mudar Tipo", (_) =>
        {
            Modal.OpenOptionsDialog("Mudar tipo de " + entryId, "Selecione o tipo de Feat",
            ["simple", "condition", "damage_over_time", "arbitrary"],
            (type) =>
            {
                if (type == null)
                    return;

                var newObj = CopyObjBase();
                if (json.TryGetProperty("toggleable", out var toggleable))
                    newObj["toggleable"] = toggleable.GetBoolean();
                
                switch (type)
                {
                    case "damage_over_time":
                    {
                        newObj["damage_type"] = Compendium.GetDefaultEntry<DamageType>().Name;
                        newObj["damage"] = 1;
                        break;
                    }
                }
                
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, newObj.ToElement()));
            });
        });
        ContextMenu.AddOption("Mudar Nome", (_) =>
        {
            Modal.OpenStringDialog("Mudar nome de " + entryId, (name) =>
            {
                if (string.IsNullOrWhiteSpace(name) || name == entryId)
                    return;
                if (Compendium.GetEntryJsonOrNull<Feature>(name) != null)
                {
                    Modal.OpenAcceptDialog("Erro", "Uma feature com esse id já existe.");
                    return;
                }
                var mutJson = json.ToNode()!.AsObject();
                mutJson["name"] = name;
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.UpdateEntry(folder, entryId, mutJson.ToElement()));
            }, true);
        });
        ContextMenu.AddOption("Mudar Descrição", (_) =>
        {
            Modal.OpenStringDialog("Mudar descrição de " + entryId, (desc) =>
            {
                var mutJson = json.ToNode()!.AsObject();
                if (desc == null || desc == mutJson["description"]!.ToString())
                    return;
                mutJson["description"] = desc;
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.UpdateEntry(folder, entryId, mutJson.ToElement()));
            }, false, json.GetProperty("description").ToString());
        });
        
    }
}
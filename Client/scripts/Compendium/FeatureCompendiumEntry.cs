using System.Text.Json.Nodes;
using Rpg;
using TTRpgClient.scripts;
using TTRpgClient.scripts.ui;

public partial class FeatureCompendiumEntry : CodeCompendiumEntry
{
    private static TabInfo[] arbitraryTabs =
    [
        new()
        {
            JsonKey = "tick",
            TabTitle = "OnTick"
        },
        new()
        {
            JsonKey = "enable",
            TabTitle = "OnEnable"
        },
        new()
        {
            JsonKey = "disable",
            TabTitle = "OnDisable"
        },
        new ()
        {
            JsonKey = "doesGetAttacked",
            TabTitle = "DoesGetAttacked"
        },
        new ()
        {
            JsonKey = "doesAttack",
            TabTitle = "DoesAttack"
        },
        new ()
        {
            JsonKey = "doesExecuteSkill",
            TabTitle = "DoesExecuteSkill"
        },
        new ()
        {
            JsonKey = "attacked",
            TabTitle = "OnAttacked"
        },
        new ()
        {
            JsonKey = "attack",
            TabTitle = "OnAttack"
        },
        new ()
        {
            JsonKey = "executeSkill",
            TabTitle = "OnExecuteSkill"
        },
        new ()
        {
            JsonKey = "injured",
            TabTitle = "OnInjured"
        },
        new()
        {
            JsonKey = "receivingDamage",
            TabTitle = "ModifyReceivingDamage"
        },
        new()
        {
            JsonKey = "attackingDamage",
            TabTitle = "ModifyAttackingDamage"
        }
    ];
    public FeatureCompendiumEntry(string entryId, JsonObject json) : base(Compendium.GetFolderName<Feature>(), entryId, json)
    {
        tabs = json["type"]!.ToString() switch
        {
            "arbitrary" => arbitraryTabs,
            _ => tabs
        };
    }

    private JsonObject CopyObjBase()
    {
        JsonObject newObj = new JsonObject
        {
            ["type"] = json["type"],
            ["name"] = json["name"],
            ["description"] = json["description"],
            ["icon"] = json["icon"]
        };
        return newObj;
    }

    protected override void OnClick()
    {
        switch (json["type"]!.ToString())
        {
            case "arbitrary":
            {
                base.OnClick();
                break;
            }
            case "damage_over_time":
            {
                //TODO: Dmg type and actually updating
                Modal.OpenFormDialog("DOTFeature " + entryId, (result) =>
                {
                    var newObj = CopyObjBase();
                    newObj["damageType"] = result.TipoDeDano.Name;
                    newObj["damage"] = result.Dano;
                    newObj["interval"] = result.Intervalo;
                    
                    NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, newObj));
                }, (TipoDeDano: DamageType.Physical, Dano: 1f, Intervalo: json["interval"]?.GetValue<int>() ?? 0));
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
                if (json.ContainsKey("toggleable"))
                    newObj["toggleable"] = json["toggleable"];
                
                switch (type)
                {
                    case "damage_over_time":
                    {
                        newObj["damage_type"] = DamageType.Physical.Name;
                        newObj["damage"] = 1;
                        break;
                    }
                }
                
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, newObj));
            });
        });
    }
}
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
                Modal.OpenFormDialog("Feature " + entryId, (results) =>
                {
                    
                }, ("Tipo de Dano", DamageType.Physical, null), ("Dano", json["damage"]!.GetValue<float>(), d => ((float)d) > 0), ("Intervalo", json["interval"]?.GetValue<uint>() ?? 0, null));
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
                
                JsonObject newObj = new JsonObject
                {
                    ["type"] = type,
                    ["name"] = json["name"],
                    ["description"] = json["description"],
                    ["icon"] = json["icon"]
                };
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
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

    protected override void OnClick()
    {
        var fields = new ExpressionEditorWindow.ExprFieldInfo[]
        {
            new() { JsonKey = "execute", DisplayName = "On Execute", ExprType = "effect" },
            new() { JsonKey = "start", DisplayName = "On Start", ExprType = "effect" },
            new() { JsonKey = "cancel", DisplayName = "On Cancel", ExprType = "effect" },
            new() { JsonKey = "delay", DisplayName = "Delay (ticks)", ExprType = "number" },
            new() { JsonKey = "cooldown", DisplayName = "Cooldown (ticks)", ExprType = "number" },
            new() { JsonKey = "duration", DisplayName = "Duration (ticks)", ExprType = "number" },
            new() { JsonKey = "canCancel", DisplayName = "Can Cancel", ExprType = "bool" },
            new() { JsonKey = "canExecute", DisplayName = "Can Execute", ExprType = "bool" },
            new() { JsonKey = "condition", DisplayName = "Condition", ExprType = "bool" },
            new() { JsonKey = "canTarget", DisplayName = "Can Target", ExprType = "bool" },
        };

        // For attack skills, add damage-related fields
        if (json.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "attack")
        {
            var attackFields = new ExpressionEditorWindow.ExprFieldInfo[]
            {
                new() { JsonKey = "onHit", DisplayName = "On Hit", ExprType = "effect" },
                new() { JsonKey = "onAttack", DisplayName = "On Attack", ExprType = "effect" },
            };
            var allFields = new ExpressionEditorWindow.ExprFieldInfo[fields.Length + attackFields.Length];
            fields.CopyTo(allFields, 0);
            attackFields.CopyTo(allFields, fields.Length);
            fields = allFields;
        }

        Modal.OpenExpressionEditorMulti($"Skill: {entryId}", fields, json,
            (result) =>
            {
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, result.ToElement()));
            });
    }
}
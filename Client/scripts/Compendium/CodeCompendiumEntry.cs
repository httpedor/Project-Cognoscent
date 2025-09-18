using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace TTRpgClient.scripts.ui;

public partial class CodeCompendiumEntry : CompendiumEntry
{
    private string[] codeKeys;
    public CodeCompendiumEntry(string folder, string entryId, JsonObject json, string[] codeKeys) : base(folder, entryId, json)
    {
        this.codeKeys = codeKeys;
    }

    protected override void OnClick()
    {
        base.OnClick();

        var tabs = new List<(string, string?)>();
        foreach (string key in codeKeys)
        {
            if (json.ContainsKey(key))
            {
                tabs.Add((key, json[key]!.ToString()));
            }
        }
        Modal.OpenCode("Titulo", tabs.ToArray(), (results) =>
        {
            GD.Print(results.Length);
        });
    }
}
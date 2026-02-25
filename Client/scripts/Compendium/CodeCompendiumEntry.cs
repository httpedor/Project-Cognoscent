using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class CodeCompendiumEntry : CompendiumEntry
{
    public class TabInfo
    {
        public required string JsonKey;
        public string TabTitle = "";
        public Dictionary<string, Type> Globals = new();
    }
    protected TabInfo[] tabs;
    public CodeCompendiumEntry(string folder, string entryId, JsonElement json, TabInfo[] tabs) : base(folder, entryId, json)
    {
        this.tabs = tabs;
    }

    public CodeCompendiumEntry(string folder, string entryId, JsonElement json) : base(folder, entryId, json)
    {
        tabs = [];
    }

    protected override void OnClick()
    {
        base.OnClick();

        var displayToId = new Dictionary<string, string>();
        var tabsContent = new (string, Control)[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            var tabInfo = tabs[i];
            var code = new CSharpCodeEdit();
            if (json.TryGetProperty(tabInfo.JsonKey, out var prop))
                code.Text = prop.GetString()!.Replace("\\n", "\n");
            foreach (var global in tabInfo.Globals)
            {
                //TODO: This
                //code.AddCodeCompletionOption();
            }
            displayToId[tabInfo.TabTitle] = tabInfo.JsonKey;
            tabsContent[i] = (tabInfo.TabTitle, code);
        }
        Modal.OpenTabs(entryId, tabsContent, () =>
        {
            var mutJson = json.ToNode()!.AsObject();
            foreach (var tabContent in tabsContent)
            {
                var code = (tabContent.Item2 as CodeEdit)!;
                string jsonKey = displayToId[tabContent.Item1];
                
                if (code.Text == "")
                    mutJson.Remove(jsonKey);
                else
                    mutJson[jsonKey] = code.Text;
            }
            NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, mutJson.ToElement()));
        });
    }
}
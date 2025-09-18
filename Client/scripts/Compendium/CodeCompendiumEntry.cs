using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class CodeCompendiumEntry : CompendiumEntry
{
    public class TabInfo
    {
        public string JsonKey;
        public string TabTitle;
        public Dictionary<string, Type> Globals;
    }
    protected TabInfo[] tabs;
    public CodeCompendiumEntry(string folder, string entryId, JsonObject json, TabInfo[] tabs) : base(folder, entryId, json)
    {
        this.tabs = tabs;
    }

    public CodeCompendiumEntry(string folder, string entryId, JsonObject json) : base(folder, entryId, json)
    {
        tabs = [];
    }

    protected override void OnClick()
    {
        base.OnClick();

        var tabsContent = new (string, Control)[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            var tabInfo = tabs[i];
            var code = new CSharpCodeEdit();
            if (json.ContainsKey(tabInfo.JsonKey))
                code.Text = json[tabInfo.JsonKey]!.ToString();
            
            tabsContent[i] = (tabs[i].TabTitle, code);
        }
        Modal.OpenTabs(entryId, tabsContent, () =>
        {
            foreach (var tabContent in tabsContent)
            {
                var code = (tabContent.Item2 as CodeEdit)!;
                
                if (code.Text == "")
                    json.Remove(tabContent.Item1);
                else
                    json[tabContent.Item1] = code.Text;
            }
            NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, entryId, json));
        });
    }
}
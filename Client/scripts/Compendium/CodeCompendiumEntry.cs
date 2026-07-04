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
    }
}
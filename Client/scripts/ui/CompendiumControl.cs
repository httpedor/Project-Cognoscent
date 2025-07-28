using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class CompendiumControl : VBoxContainer
{
    public override void _Ready()
    {
        base._Ready();
        Compendium.OnFolderRegistered += (folder) =>
        {
            VBoxContainer main = new VBoxContainer
            {
                Name = folder
            };
            Label header = new Label()
            {
                Name = "Header",
                Text = folder,
                LabelSettings = new LabelSettings()
                {
                    FontSize = 24,
                }
            };
            HSeparator sep = new HSeparator();
            main.AddChild(header);
            main.AddChild(sep);
            AddChild(main);
        };
        Compendium.OnEntryRegistered += (folder, entry, json) =>
        {
            VBoxContainer main = GetNode<VBoxContainer>(folder);
            if (main == null)
            {
                GD.PrintErr("Compendium folder " + folder + " not found!");
                return;
            }
            Label placeholder = new Label()
            {
                Name = entry,
                Text = entry,
                LabelSettings = new LabelSettings()
                {
                    FontSize = 16,
                }
            };
            main.AddChild(placeholder);
        };
        Compendium.OnEntryRemoved += (folder, entry) =>
        {
            
        };

        Compendium.RegisterDefaults();
    }
}
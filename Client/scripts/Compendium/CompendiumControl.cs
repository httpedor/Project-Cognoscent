using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Rpg;
using FileAccess = Godot.FileAccess;

namespace TTRpgClient.scripts.ui;

public partial class CompendiumControl : ScrollContainer
{
    private static readonly Dictionary<string, Func<IEnumerable<(string name, JsonObject json)>>> addFunctions = new()
    {
        {"Midia", () => {
            string f = Modal.OpenFileDialogAsync().Result[0];
            byte[] data = File.ReadAllBytes(f);
            string fName = f[(f.LastIndexOf('/') + 1)..];
            JsonObject json = new JsonObject
            {
                ["fileName"] = fName,
                ["type"] = Midia.GetFilenameType(fName).ToString(),
                ["data"] = Convert.ToBase64String(data)
            };
            return [(fName[..fName.LastIndexOf('.')], json)];
        }},
        {"Notes", () =>
        {
            JsonObject json = new JsonObject();
            json["text"] = "";
            return [("New Note", json)];
        }}
    };

    private static readonly Dictionary<string, Func<string, JsonObject, (Action, string)[]>> contextMenuFunctions = new()
    {

    };

    private Control? dragging;
    private VBoxContainer folderContainer;
    
    public override void _Ready()
    {
        base._Ready();
        folderContainer = new VBoxContainer();
        AddChild(folderContainer);
        Compendium.OnFolderRegistered += (folder) =>
        {
            VBoxContainer main = new VBoxContainer
            {
                Name = folder
            };
            HBoxContainer header = new HBoxContainer()
            {
                Name = "Header",
                AnchorLeft = 0,
                AnchorRight = 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            Label text = new Label()
            {
                Text = folder,
                LabelSettings = new LabelSettings()
                {
                    FontSize = 24,
                },
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            header.AddChild(text);

            if (addFunctions.TryGetValue(folder, out var addFunc))
            {
                TextureRect add = new TextureRect()
                {
                    Texture = Icons.Plus,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(32, 32)
                };
                add.GuiInput += async (@event) =>
                {
                    if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) return;
                    add.AcceptEvent();
                    foreach (var info in addFunc())
                        NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, info.name, info.json));
                };
                header.AddChild(add);
            }
            HSeparator sep = new HSeparator();
            main.AddChild(header);
            main.AddChild(sep);
            folderContainer.AddChild(main);
        };
        Compendium.OnEntryRegistered += (folder, entry, json) =>
        {
            VBoxContainer main = folderContainer.GetNode<VBoxContainer>(folder);
            if (main == null)
            {
                GD.PrintErr("Compendium folder " + folder + " not found!");
                return;
            }

            main.AddChild(CompendiumEntry.GetEntryFor(folder, entry, json));
        };
        Compendium.OnEntryRemoved += (folder, entry) =>
        {
            VBoxContainer main = GetNodeOrNull<VBoxContainer>(folder);
            if (main == null)
            {
                GD.PrintErr("Compendium folder " + folder + " not found when trying to remove entry " + entry);
                return;
            }

            Node child = main.GetNodeOrNull(entry);
            if (child == null)
            {
                GD.PrintErr("Entry " + entry + " not found in folder " + folder);
                return;
            }

            main.RemoveChild(child);
            child.QueueFree();
        };

        Compendium.RegisterDefaults();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        folderContainer.Size = new Vector2(Size.X, folderContainer.Size.Y);
        if (dragging != null)
        {
            dragging.GlobalPosition = GetGlobalMousePosition() - (dragging.Size/2);
            dragging.Size = new Vector2(32, 32);
        }
    }
}
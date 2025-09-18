using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public partial class CompendiumEntry : Control, IContextMenuProvider
{
    public static CompendiumEntry GetEntryFor(string folder, string entryId, JsonObject entry)
    {
        return folder switch
        {
            "Midia" => new MidiaCompendiumEntry(entryId, entry),
            "Notes" => new NoteCompendiumEntry(entryId, entry),
            "Features" => new CodeCompendiumEntry(folder, entryId, entry, []),
            _ => new CompendiumEntry(folder, entryId, entry)
        };
    }
    
    private static Control? dragging;
    private bool clicking = false;
    
    protected TextureRect entryTexture;
    
    protected string entryId;
    protected string folder;
    protected JsonObject json;

    protected CompendiumEntry(string folder, string entryId, JsonObject json)
    {
        this.entryId = entryId;
        this.json = json;
        this.folder = folder;
        HBoxContainer entryContainer = new HBoxContainer()
        {
            Name = entryId
        };
        entryTexture = new TextureRect()
        {
            Texture = GetIcon(),
            Size = new Vector2I(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        Label entryLabel = new Label()
        {
            Text = entryId,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            LabelSettings = new LabelSettings()
            {
                FontSize = 16,
            }
        };
        TextureRect removeTexture = new TextureRect()
        {
            Texture = Icons.Trash,
            CustomMinimumSize = new Vector2(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        removeTexture.GuiInput += (@event) =>
        {
            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) return;

            if (Input.IsKeyPressed(Key.Ctrl))
            {
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.RemoveEntry(folder, entryId));
                return;
            }

            Modal.OpenConfirmationDialog("Confirmar Deletar " + json,
                $"Tem certeza que deseja deletar {folder}/{json}", (delete) =>
                {
                    if (delete)
                        NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.RemoveEntry(folder, entryId));
                }, "Sim", "Não");
        };

        entryContainer.AddChild(entryTexture);
        entryContainer.AddChild(entryLabel);
        entryContainer.AddChild(removeTexture);
        AddChild(entryContainer);


        MouseEntered += () => InputManager.RequestPriority(this);
        MouseExited += () => InputManager.ReleasePriority(this);
    }

    public override void _Ready()
    {
        base._Ready();
        CustomMinimumSize = new Vector2(0, 24);
    }

    public virtual Texture2D GetIcon()
    {
        return Icons.GetIcon(json["icon"]?.GetValue<string>());
    }

    protected virtual void OnDrag(Vector2 target)
    {
        
    }

    protected virtual void OnClick()
    {
        
    }

    protected virtual void OnAddFile()
    {
        
    }
    
    public virtual void AddGMContextMenuOptions()
    {
        ContextMenu.AddOption("Renomear", _ =>
        {
            Modal.OpenStringDialog("Renomear", name =>
            {
                if (name == null)
                    return;
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.RemoveEntry(folder, entryId));
                NetworkManager.Instance.SendPacket(CompendiumUpdatePacket.AddEntry(folder, name, json));
            }, true);
        });
    }

    public virtual void AddContextMenuOptions()
    {
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
        if (clicking && @event is InputEventMouseMotion iemm)
        {
            AcceptEvent();
            if (dragging == null)
            {
                TextureRect cloneTex = new TextureRect()
                {
                    Texture = entryTexture.Texture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = entryTexture.StretchMode,
                    Modulate = new Color(1, 1, 1, 0.5f)
                };
                cloneTex.GuiInput += (@event2) =>
                {
                    if (@event2 is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) return;
                    cloneTex.AcceptEvent();
                    OnDrag(InputManager.Instance.MousePosition);

                    clicking = false;
                    dragging = null;
                    cloneTex.QueueFree();
                };
                GameManager.Instance.AddChild(cloneTex);
                dragging = cloneTex;
            }
        }
        if (@event is not InputEventMouseButton iemb) return;
        
        AcceptEvent();
        if (iemb.ButtonIndex == MouseButton.Left && !iemb.Pressed)
        {
            clicking = false;
            OnClick();
        }

        if (iemb.ButtonIndex == MouseButton.Left && iemb.Pressed)
        {
            clicking = true;
        }
    }

}
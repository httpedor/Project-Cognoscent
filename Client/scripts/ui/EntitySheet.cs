// csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts.ui;
public partial class EntitySheetWindow : Window
{
    // Track open windows so we can offset them and avoid overwriting
    private static readonly System.Collections.Generic.List<EntitySheetWindow> OpenWindows = new();
    private static int nextOffset = 0;
    private const int OffsetStep = 24;
    private const int MaxOffsetWrap = 8;

    // Instance UI nodes
    private readonly VBoxContainer root;
    private readonly HBoxContainer header;
    private readonly TextureRect displayTex;
    private readonly Label nameLbl;
    private readonly Label subtitleLbl;
    private readonly Button inspectBodyBtn;
    private readonly ScrollContainer statsScroll;
    private readonly VBoxContainer statsVBox;
    private readonly Label featuresTitle;
    private readonly FlowContainer featuresFlow;

    private ClientBoard? currentBoard;
    private EntityNode? currentEntityNode;
    private Entity? currentEntity;

    public EntitySheetWindow()
    {
        Name = "EntitySheetWindow";
        // floating: use rect position and size
        Size = new Vector2I(360, 300);
        Visible = true;
        Unresizable = false;
        Title = "Ficha";
        
        CloseRequested += CloseWindow;
        
        root = new VBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);

        displayTex = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.FitWidth,
            CustomMinimumSize = new Vector2(72, 72),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ClipContents = true
        };
        header.AddChild(displayTex);

        var infoVBox = new VBoxContainer();
        infoVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        infoVBox.AddThemeConstantOverride("separation", 6);
        
        header.AddChild(infoVBox);

        nameLbl = new Label
        {
            Text = "Unknown",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        nameLbl.AddThemeColorOverride("font_color", Colors.LimeGreen);
        infoVBox.AddChild(nameLbl);

        subtitleLbl = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 4
        };
        subtitleLbl.AddThemeColorOverride("font_color", Colors.Silver);
        infoVBox.AddChild(subtitleLbl);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(btnRow);

        inspectBodyBtn = new Button { Text = "Inspect Body", Disabled = true };
        inspectBodyBtn.Pressed += () =>
        {
            if (currentEntity is Creature c)
            {
                BodyInspector.Instance.Show(c.Body, BodyInspector.BodyInspectorSettings.HEALTH);
            }
        };
        btnRow.AddChild(inspectBodyBtn);


        var sep1 = new HSeparator();
        root.AddChild(sep1);

        var statsTitle = new Label { Text = "Attributes", HorizontalAlignment = HorizontalAlignment.Left };
        statsTitle.AddThemeColorOverride("font_color", Colors.White);
        root.AddChild(statsTitle);

        statsScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 120)
        };
        statsVBox = new VBoxContainer();
        statsVBox.AddThemeConstantOverride("separation", 6);
        statsScroll.AddChild(statsVBox);
        root.AddChild(statsScroll);

        featuresTitle = new Label { Text = "Features", HorizontalAlignment = HorizontalAlignment.Left };
        root.AddChild(featuresTitle);

        featuresFlow = new FlowContainer
        {
            CustomMinimumSize = new Vector2(0, 60)
        };
        featuresFlow.AddThemeConstantOverride("separation", 6);
        featuresFlow.AddThemeConstantOverride("vertical_separation", 6);
        root.AddChild(featuresFlow);
    }

    // Factory: open a new floating window for given board/entity
    public static void Open(Entity entity)
    {
        var w = new EntitySheetWindow();
        // position windows with small offset so multiple windows are visible
        int offs = (nextOffset++ % MaxOffsetWrap) * OffsetStep;
        w.Position = new Vector2I(120 + offs, 80 + offs);
        nextOffset %= MaxOffsetWrap;

        OpenWindows.Add(w);
        // Add to UI layer; keep z-order towards top
        GameManager.UILayer.AddChild(w);
        w.SetData((ClientBoard)entity.Board, entity);
        w.Populate();
    }

    private void CloseWindow()
    {
        OpenWindows.Remove(this);
        QueueFree();
    }

    private void SetData(ClientBoard board, Entity entity)
    {
        currentBoard = board;
        currentEntity = entity;
        currentEntityNode = board.GetEntityNode(entity);
    }

    private double lastUpdate;
    public override void _Process(double delta)
    {
        base._Process(delta);
        if (lastUpdate > 1)
        {
            UpdateSheet();
            lastUpdate = 0;
        }
        lastUpdate += delta;
    }

    public void UpdateSheet()
    {
        if (Visible && currentEntity != null)
            Populate();
    }

    private void Populate()
    {
        if (currentEntity == null || currentEntityNode == null) return;

        if (currentEntity.Name == "" || !currentEntityNode.NameKnown)
            nameLbl.Text = "Desconhecido";
        else
            nameLbl.Text = currentEntity.Name;

        string typeFriendly = HumanFriendlyType(currentEntity.GetEntityType());
        string grounded = currentEntity.IsGrounded ? "No Chão" : "Queda Livre";
        string sizeClass = FriendlySize(currentEntity.Size);
        subtitleLbl.Text = $"{typeFriendly} • {grounded} • {sizeClass}";

        try
        {
            var node = currentBoard?.GetEntityNode(currentEntity);
            if (node != null)
                displayTex.Texture = node.Display.Texture;
            else
                displayTex.Texture = GetTextureOrNull(currentEntity.Display);
        }
        catch
        {
            displayTex.Texture = null;
        }

        ClearChildren(statsVBox);
        if (currentEntity is Creature c)
        {
            var healthBar = new HBoxContainer();
            healthBar.AddThemeConstantOverride("separation", 8);

            var healthLabel = new Label { Text = "Health", HorizontalAlignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(80, 0) };
            healthLabel.AddThemeColorOverride("font_color", Colors.OrangeRed);
            healthBar.AddChild(healthLabel);

            var pb = new ProgressBar
            {
                MinValue = 0,
                MaxValue = c.MaxHealth > 0 ? (float)c.MaxHealth : 1,
                Value = (float)Math.Max(0, c.Health),
                CustomMinimumSize = new Vector2(0, 18)
            };
            pb.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            healthBar.AddChild(pb);

            var percentLbl = new Label { Text = FriendlyPercent(c.Health, c.MaxHealth), HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(60, 0) };
            percentLbl.AddThemeColorOverride("font_color", Colors.White);
            healthBar.AddChild(percentLbl);

            statsVBox.AddChild(healthBar);
        }

        foreach (var stat in currentEntity.Stats.OrderBy(s => s.Id))
        {
            if (!CharacterKnowledgeManager.KnowsStat(currentEntity, stat))
                continue;
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 8);

            var lbl = new Label { Text = HumanizeStatId(stat.Id), CustomMinimumSize = new Vector2(100, 0), HorizontalAlignment = HorizontalAlignment.Left };
            lbl.AddThemeColorOverride("font_color", Colors.White);
            h.AddChild(lbl);

            float ratio = StatRatio(stat);
            var pb = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                Value = ratio,
                CustomMinimumSize = new Vector2(0, 14)
            };
            pb.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            h.AddChild(pb);

            var desc = new Label { Text = StatDescriptor(ratio), CustomMinimumSize = new Vector2(80, 0), HorizontalAlignment = HorizontalAlignment.Right };
            desc.AddThemeColorOverride("font_color", Colors.Silver);
            h.AddChild(desc);

            statsVBox.AddChild(h);
        }

        if (statsVBox.GetChildCount() == 0)
            statsVBox.AddChild(new Label { Text = "Sem atributos" });

        ClearChildren(featuresFlow);
        bool found = false;
        foreach (var kv in currentEntity.FeaturesDict)
        {
            var f = kv.Value.feature;
            if (!CharacterKnowledgeManager.KnowsFeature(currentEntity, f))
                continue;
            found = true;
            bool enabled = kv.Value.enabled;
            var tag = new Button
            {
                Text = f.GetName(),
                ToggleMode = false,
                Disabled = true,
                FocusMode = Control.FocusModeEnum.None
            };
            tag.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            tag.AddThemeColorOverride("font_color", enabled ? Colors.White : Colors.Gray);
            tag.AddThemeColorOverride("font_color_pressed", enabled ? Colors.White : Colors.Gray);
            featuresFlow.AddChild(tag);
        }
        if (!found)
            featuresFlow.AddChild(new Label { Text = "Nenhuma feature" });

        inspectBodyBtn.Disabled = !(currentEntity is Creature);
    }

    private static string HumanFriendlyType(EntityType t) => t switch
    {
        EntityType.Creature => "Criatura",
        EntityType.Item => "Item",
        EntityType.Projectile => "Projétil",
        EntityType.Door => "Porta",
        EntityType.Container => "Container",
        EntityType.Light => "Fonte de Luz",
        EntityType.Prop => "Prop",
        _ => "Desconhecido"
    };

    private static string FriendlySize(System.Numerics.Vector3 size)
    {
        float area = size.X * size.Y;
        if (area < 1f) return "Pequeno";
        if (area < 4f) return "Médio";
        return "Largo";
    }

    private static float StatRatio(Stat stat)
    {
        if (stat.BaseValue > 0.0001f)
            return Math.Clamp(stat.MaxValue / stat.FinalValue, 0f, 1f);
        return Math.Clamp(stat.MaxValue / (stat.MaxValue + 10f), 0f, 1f);
    }

    private static string StatDescriptor(float ratio)
    {
        if (ratio >= 0.85f) return "Very high";
        if (ratio >= 0.65f) return "High";
        if (ratio >= 0.4f) return "Average";
        if (ratio >= 0.15f) return "Low";
        return "Very low";
    }

    private static string FriendlyPercent(double value, double max)
    {
        if (max <= 0) return "—";
        int p = (int)Math.Round((value / max) * 100);
        return p + "%";
    }

    private static string HumanizeStatId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Unknown";
        id = id.Replace('_', ' ').Trim();
        return char.ToUpper(id[0]) + id.Substring(1);
    }

    private static void ClearChildren(Node parent)
    {
        var children = parent.GetChildren();
        // GetChildren returns Godot.Collections.Array
        for (int i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is Node node)
                node.QueueFree();
        }
    }

    private static Texture2D? GetTextureOrNull(Midia? m)
    {
        if (m == null) return null;
        try
        {
            if (m.Type != MidiaType.Image || m.Bytes == null || m.Bytes.Length == 0)
                return null;

            var img = new Image();
            img.LoadPngFromBuffer(m.Bytes);
            if (img.IsEmpty())
            {
                img.LoadJpgFromBuffer(m.Bytes);
                if (img.IsEmpty())
                {
                    img.LoadWebpFromBuffer(m.Bytes);
                    if (img.IsEmpty())
                        return null;
                }
            }

            return ImageTexture.CreateFromImage(img);
        }
        catch
        {
            return null;
        }
    }
}

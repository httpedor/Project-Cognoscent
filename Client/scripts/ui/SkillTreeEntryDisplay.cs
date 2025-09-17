using System;
using System.Linq;
using Godot;
using Rpg;
using TTRpgClient.scripts.ui;

public partial class SkillTreeEntryDisplay : Sprite2D
{
    private Node2D bg;
    private PanelContainer tooltipPanel;
    private Label tooltipLabel;
    private double existedTime = 0;
    private double xFreq = 1;
    private double yFreq = 1;
    private double xOffset = 0;
    private double yOffset = 0;
    private double mouseOverTime;
    public Vector2 StartPos;
    public SkillTreeEntry? Entry
    {
        get;
        set
        {
            field = value;
            if (field == null)
            {
                Visible = false;
                return;
            }

            Visible = true;
            Texture = Icons.GetIcon(field.Icon);
            Name = field.Name;
            if (tooltipLabel != null)
                tooltipLabel.Text = field.Name + "\n\n" + field.Description;
        }
    }

    public override void _Ready()
    {
        existedTime = GD.Randf() * 1000;
        xOffset = GD.Randf() * 1000;
        yOffset = GD.Randf() * 1000;
        xFreq = GD.Randf() * 2;
        yFreq = GD.Randf() * 2;
        StartPos = Position;
        Scale = new Vector2(0.1f, 0.1f);
        bg = new Node2D()
        {
            ShowBehindParent = true
        };
        bg.Draw += () => _DrawBehind(bg);
        AddChild(bg);

        tooltipLabel = new Label();
        if (Entry != null)
            tooltipLabel.Text = Entry.Name + "\n\n" + Entry.Description;
        tooltipPanel = new PanelContainer()
        {
            Scale = new Vector2(12, 12)
        };
        tooltipPanel.AddChild(tooltipLabel);
        AddChild(tooltipPanel);
    }

    public override void _Process(double delta)
    {
        existedTime += delta;
        if (GetRect().HasPoint(GetLocalMousePosition()))
        {
            mouseOverTime += delta;
            SkillTreeDisplay.Hovered = Entry;
        }
        else
        {
            mouseOverTime = 0;
            if (SkillTreeDisplay.Hovered == Entry)
                SkillTreeDisplay.Hovered = null;
        }

        if (mouseOverTime >= 1)
        {
            tooltipPanel.Visible = true;
            tooltipPanel.Position = Position + new Vector2(GetRect().Size.X/2, -GetRect().Size.Y/2);
        }
        else
            tooltipPanel.Visible = false;

        Position = StartPos + (new Vector2((float)Math.Sin((existedTime*xFreq)+xOffset), (float)Math.Cos((existedTime*yFreq)+yOffset)) * 5);
        
        bg.QueueRedraw();
    }

    private void _DrawBehind(Node2D node)
    {
        if (Entry == null)
            return;
        if (mouseOverTime == 0)
            node.DrawRect(GetRect(), new Color(0.2f, 0.2f, 0.2f, 0.5f));
        else
            node.DrawRect(GetRect(), new Color(0.6f, 0.6f, 0.6f, 0.5f));
        if (Entry.Enabled)
            node.DrawRect(GetRect(), Colors.Red, false, 32f);
        else if (Entry.CanEnable)
            node.DrawRect(GetRect(), Colors.White, false, 32f);
        else
            node.DrawRect(GetRect(), Colors.Black, false, 32f);
    }
}
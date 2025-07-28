using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public static class ActionBar
{
    private static HBoxContainer container;

    static ActionBar()
    {
        container = new HBoxContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1,
            Alignment = BoxContainer.AlignmentMode.Center,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetBottom = -8
        };
        GameManager.UILayer.AddChild(container);
        GameManager.UILayer.MoveChild(container, 2);
    }

    public static void AddButton(string id, string tooltip, Texture2D icon, Action onClick, bool clickable = true)
    {
        var btn = new Button
        {
            Name = id,
            Icon = icon,
            IconAlignment = HorizontalAlignment.Center,
            ExpandIcon = true,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(32, 32),
            Disabled = clickable
        };
        btn.ButtonUp += onClick;
        container.AddChild(btn);
    }
    public static void RemoveButton(string id)
    {
        var btn = GetButton(id);
        if (btn == null)
            return;
        container.RemoveChild(btn);
        btn.QueueFree();
    }

    public static Button? GetButton(string id)
    {
        var btn = container.FindChild(id);
        if (btn == null)
            return null;
        return (Button?)btn;
    }

    public static void Clear()
    {
        foreach (var child in container.GetChildren())
            child.QueueFree();
    }

    public static void Hide()
    {
        container.Hide();
    }
    public static void Show()
    {
        container.Show();
    }

    public static void PopulateWithSkills(Creature creature)
    {
        foreach (var (source, skill) in creature.AvailableSkills)
        {
            AddButton(source.Name+";"+skill.GetName(), skill.GetTooltip() + "\n\nFonte: " + source.Name, Icons.GetIcon(skill.GetIconName()), async () =>
            {
                var args = await InputManager.Instance.RequestSkillArguments(creature, source, skill);
                if (args == null)
                    return;
                NetworkManager.Instance.SendPacket(new CreatureSkillUpdatePacket(creature, new SkillData(skill, args, source, skill.GetLayers(creature, source))));
            }, !skill.CanBeUsed(creature, source));
        }
    }
}

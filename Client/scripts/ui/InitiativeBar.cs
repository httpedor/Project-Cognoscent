using System;
using System.Collections.Generic;
using Godot;
using Rpg;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts.ui;

public static class InitiativeBar
{
    private static HBoxContainer container;

    static InitiativeBar()
    {
        container = new HBoxContainer
        {
            Name = "InitiativeBar",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0,
            CustomMinimumSize = new Vector2(0, 32),
            Alignment = BoxContainer.AlignmentMode.Center,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.End,
            OffsetBottom = 40
        };
        GameManager.UILayer.AddChild(container);
        GameManager.UILayer.MoveChild(container, 2);
    }
    
    public static void AddButton(string id, string tooltip, Texture2D icon, Action? onClick, bool clickable = true)
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
        if (onClick != null)
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

    public static void PopulateWithBoard(ClientBoard board)
    {
        Clear();
        LinkedList<(Creature Executor, ActionLayer Layer)> actionQueue = new();
        foreach (var creature in board.GetEntities<Creature>())
        {
            foreach (string layerName in creature.ActiveActionLayers)
            {
                var layer = creature.GetActionLayer(layerName)!;
                var current = actionQueue.First;
                var prev = current;
                while (current != null)
                {
                    if (current.Value.Layer.EndTick > layer.EndTick)
                    {
                        break;
                    }
                    prev = current;
                    current = current.Next;
                }

                if (prev != null)
                    actionQueue.AddAfter(prev, (creature, layer));
                else
                    actionQueue.AddFirst((creature, layer));
            }
        }

        foreach (var action in actionQueue)
        {
            AddButton(action.Executor.Id.ToString(), action.Executor.Name + " acaba " + action.Layer.Name, board.GetEntityNode(action.Executor).Display.Texture, null, true);
        }
    }
}
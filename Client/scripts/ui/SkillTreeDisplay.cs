using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg;
using TTRpgClient.scripts.extensions;

namespace TTRpgClient.scripts.ui;

public static class SkillTreeDisplay
{
    private class SkillNode(SkillTreeEntry entry, SkillTreeEntryDisplay display)
    {
        public List<SkillNode> Dependencies = new();
        public List<SkillNode> Dependents = new();
        public SkillTreeEntry Entry = entry;
        public SkillTreeEntryDisplay Display = display;
    }

    private static Dictionary<string, SkillTreeEntryDisplay> displays = new();
    private static PanelContainer panel;
    private static Node2D container;
    
    private static List<SkillNode> TopologicalSort(IEnumerable<SkillNode> nodes) {
        var visited = new HashSet<SkillNode>();
        var result = new List<SkillNode>();

        void Visit(SkillNode node) {
            if (!visited.Add(node)) return;
            foreach (var dep in node.Dependencies) {
                Visit(dep);
            }
            result.Add(node);
        }

        foreach (var node in nodes) {
            Visit(node);
        }

        return result;
    }
    
    private static Dictionary<SkillNode, int> GetNodeLevels(IEnumerable<SkillNode> sortedNodes) {
        var levels = new Dictionary<SkillNode, int>();

        foreach (var node in sortedNodes) {
            int level = 0;
            foreach (var dep in node.Dependencies) {
                level = Math.Max(level, levels[dep] + 1);
            }
            levels[node] = level;
        }

        return levels;
    }
    
    private static Dictionary<SkillNode, Vector2> GetNodePositions(Dictionary<SkillNode, int> levels) {
        var positions = new Dictionary<SkillNode, Vector2>();
        var levelGroups = levels.GroupBy(kv => kv.Value).OrderBy(g => g.Key);

        float ySpacing = 150;
        float xSpacing = 100;

        foreach (var levelGroup in levelGroups) {
            int level = levelGroup.Key;
            var nodesInLevel = levelGroup.Select(kv => kv.Key).ToList();

            for (int i = 0; i < nodesInLevel.Count; i++) {
                float x = i * xSpacing;
                float y = level * ySpacing;
                positions[nodesInLevel[i]] = new Vector2(x, y);
            }
        }

        return positions;
    }

    public static SkillTree? Tree
    {
        get;
        set
        {
            if (value == field)
                return;
            
            foreach (var child in container.GetChildren())
                child.QueueFree();
            field = value;
            if (field != null)
            {
                Dictionary<string, SkillNode> nodes = new();
                foreach (var entry in field.Entries)
                {
                    var display = new SkillTreeEntryDisplay()
                    {
                        Entry = entry
                    };
                    var node = new SkillNode(entry, display);
                    nodes[entry.Name] = node;
                    displays[entry.Name] = display;
                    container.AddChild(display);
                }

                foreach (var node in nodes.Values)
                {
                    foreach (string dep in node.Entry.Dependencies)
                    {
                        var found = nodes.GetValueOrDefault(dep);
                        if (found != null)
                        {
                            node.Dependencies.Add(nodes[dep]);
                            nodes[dep].Dependents.Add(node);
                        }
                    }
                }
                
                var sorted = TopologicalSort(nodes.Values);
                var levels = GetNodeLevels(sorted);
                var positions = GetNodePositions(levels);

                foreach (var entry in positions)
                {
                    entry.Key.Display.StartPos = entry.Value;
                }
            }
            
            container.QueueRedraw();
        }
    }

    static SkillTreeDisplay()
    {
        panel = new PanelContainer()
        {
            Name = "SkillTreeDisplay",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            Visible = false
        };
        panel.GuiInput += _GuiInput;
        container = new Node2D()
        {
            Scale = new Vector2(0.5f, 0.5f)
        };
        container.Draw += _Draw;
        panel.AddChild(container);
        
        GameManager.UILayer.AddChild(panel);
        GameManager.UILayer.MoveChild(panel, 2);
    }

    public static SkillTreeEntry? Hovered;
    public static bool Visible
    {
        get => panel.Visible;
        set => panel.Visible = value;
    }

    private static void _Draw()
    {
        foreach (var display in displays.Values)
        {
            foreach (string depName in display.Entry?.Dependencies ?? Enumerable.Empty<string>())
            {
                var depDisplay  = displays[depName];
                container.DrawLine(display.StartPos, depDisplay.StartPos, Colors.White, 8, true);
            }
        }
    }

    private static void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motionEvent)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Middle))
            {
                container.Position += motionEvent.ScreenRelative;
            }
        }
        else if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
                container.Scale *= 1.1f;
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
                container.Scale /= 1.1f;
            container.Scale = new Vector2(Mathf.Clamp(container.Scale.X, 0.2f, 0.8f), Mathf.Clamp(container.Scale.Y, 0.2f, 0.8f));

            if (mouseEvent is { ButtonIndex: MouseButton.Left, Pressed: false } && Hovered is { CanEnable: true })
            {
                NetworkManager.Instance.SendPacket(new SkillTreeUpdatePacket(Hovered, !Hovered.Enabled));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

public static class SkillTreeDisplay
{
    private class SkillNode(SkillTreeEntry entry, SkillTreeEntryDisplay display)
    {
        public List<SkillNode> Dependencies = new();
        public SkillTreeEntry Entry = entry;
        public SkillTreeEntryDisplay Display = display;
    }

    private static PanelContainer panel;
    private static DiagramCanvas container;
    
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

                // Update the diagram: build connection map and refresh the diagram's internal display map
                container.UpdateDisplays();
                var conn = new Dictionary<string, IEnumerable<string>>();
                foreach (var kv in nodes)
                    conn[kv.Key] = kv.Value.Entry.Dependencies;
                // Make skill-tree nodes draggable by default so users can rearrange them; can be toggled per-node later.
                container.SetDraggableNodes(nodes.Keys);
                container.SetConnections(conn);
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
        container = new DiagramCanvas()
        {
            Scale = new Vector2(0.5f, 0.5f)
        };
        panel.AddChild(container);
        
        GameManager.UILayer.AddChild(panel);
        GameManager.UILayer.MoveChild(panel, 2);
    }

    public static SkillTreeEntry? Hovered;
    // Bridge Hovered to the DiagramCanvas so child displays can set it directly on the diagram
    public static SkillTreeEntry? HoveredEntry
    {
        get => container.Hovered as SkillTreeEntry;
        set => container.Hovered = value;
    }
    public static bool Visible
    {
        get => panel.Visible;
        set => panel.Visible = value;
    }

    // Diagram drawing/panning/zooming handled by DiagramCanvas. Keep GUI input handler to handle skill-specific actions.

    private static void _GuiInput(InputEvent @event)
    {
        // forward panning/zooming to the diagram
        container.HandleGuiInput(@event);

        if (@event is InputEventMouseButton mouseEvent)
        {
            // DiagramCanvas already applied zoom; handle skill-specific click action here
            if (mouseEvent is { ButtonIndex: MouseButton.Left, Pressed: false })
            {
                var hovered = HoveredEntry;
                if (hovered is { CanEnable: true })
                {
                    NetworkManager.Instance.SendPacket(new SkillTreeUpdatePacket(hovered, !hovered.Enabled));
                }
            }
        }
    }
}
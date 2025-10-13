// filepath: c:\Users\pedro.alvares\RiderProjects\Project-Cognoscent\Client\scripts\ui\DiagramCanvas.cs
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TTRpgClient.scripts.ui;

public partial class DiagramCanvas : Node2D
{
    // Map child.Name -> child Node2D (node displays)
    public Dictionary<string, Node2D> Displays { get; private set; } = new();

    // Map display name -> dependency names
    public Dictionary<string, IEnumerable<string>> Connections { get; private set; } = new();

    // A general-purpose hovered payload. Consumers can store any object here (e.g. a SkillTreeEntry).
    public object? Hovered { get; set; }

    // Drag/select state (declared here so methods below compile)
    private bool isDragging = false;
    private Node2D? draggedNode = null;
    private Vector2 dragOffset = Vector2.Zero;

    // Events for consumers
    public event System.Action<string>? NodeSelected;
    public event System.Action<string>? NodeDragStarted;
    public event System.Action<string, Vector2>? NodeDragged;
    public event System.Action<string>? NodeDragEnded;

    // Per-node draggable control
    private HashSet<string> draggableNodes = new();
    public void SetNodeDraggable(string name, bool draggable)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (draggable) draggableNodes.Add(name);
        else draggableNodes.Remove(name);
    }

    public void SetDraggableNodes(IEnumerable<string> names)
    {
        draggableNodes = new HashSet<string>(names ?? Enumerable.Empty<string>());
    }

    public bool IsNodeDraggable(string name) => !string.IsNullOrEmpty(name) && draggableNodes.Contains(name);

    public DiagramCanvas()
    {
        // sensible defaults (kept from previous code)
        Scale = new Vector2(0.5f, 0.5f);
    }

    // Scan children and build the Displays map. Call after adding/removing children.
    public void UpdateDisplays()
    {
        Displays.Clear();
        foreach (var c in GetChildren())
        {
            if (c is Node2D n && !string.IsNullOrEmpty(n.Name))
                Displays[n.Name] = n;
        }
        QueueRedraw();
    }

    public void SetConnections(Dictionary<string, IEnumerable<string>> connections)
    {
        // caller should provide a non-null map; assign directly
        Connections = connections;
        QueueRedraw();
    }

    // Handle panning/zooming. Skill-tree specific actions (like toggling a skill) should be handled by the caller.
    public void HandleGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motionEvent)
        {
            // Use viewport mouse position for consistent coordinates
            var mouseGlobal = GetViewport().GetMousePosition();
            var mouseLocal = ToLocal(mouseGlobal);

            // If currently dragging a node, move it and show drag cursor
            if (isDragging && draggedNode != null)
            {
                Input.SetDefaultCursorShape(Input.CursorShape.Drag);
                var newPos = mouseLocal - dragOffset;
                // If the node is a SkillTreeEntryDisplay, update StartPos so its _Process respects the new base position
                if (draggedNode is SkillTreeEntryDisplay ste)
                    ste.StartPos = newPos;
                else
                    draggedNode.Position = newPos;

                NodeDragged?.Invoke(draggedNode.Name, newPos);
                QueueRedraw();
                return;
            }

            // If middle mouse is pressed, we're panning: set drag cursor
            if (Input.IsMouseButtonPressed(MouseButton.Middle))
            {
                Input.SetDefaultCursorShape(Input.CursorShape.Drag);
                Position += motionEvent.ScreenRelative;
                QueueRedraw();
                return;
            }

            // Otherwise show pointing hand when hovering a draggable node, arrow otherwise
            {
                var children = GetChildren();
                Node2D? hoverCandidate = null;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    if (children[i] is Node2D c && !string.IsNullOrEmpty(c.Name))
                    {
                        var childLocal = c.ToLocal(mouseGlobal);
                        if (c is Sprite2D s)
                        {
                            var r = s.GetRect();
                            if (r.HasPoint(childLocal)) { hoverCandidate = c; break; }
                        }
                        else
                        {
                            var dist = (c.Position - mouseLocal).Length();
                            if (dist < 24) { hoverCandidate = c; break; }
                        }
                    }
                }

                if (hoverCandidate != null && IsNodeDraggable(hoverCandidate.Name))
                    Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
                else
                    Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            }
        }
        else if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
                Scale *= 1.1f;
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
                Scale /= 1.1f;

            Scale = new Vector2(Mathf.Clamp(Scale.X, 0.2f, 0.8f), Mathf.Clamp(Scale.Y, 0.2f, 0.8f));
            QueueRedraw();

            // Left button press -> start dragging / select
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                var mouseGlobal = GetViewport().GetMousePosition();
                var mouseLocal = ToLocal(mouseGlobal);

                if (mouseEvent.Pressed)
                {
                    // pick top-most child under mouse. Iterate children in reverse order so higher Z-order wins
                    var children = GetChildren();
                    Node2D? picked = null;
                    for (int i = children.Count - 1; i >= 0; i--)
                    {
                        if (children[i] is Node2D c && !string.IsNullOrEmpty(c.Name))
                        {
                            // convert mouse into child's local coords and test against Sprite rect if available
                            var childLocal = c.ToLocal(mouseGlobal);
                            if (c is Sprite2D s)
                            {
                                var r = s.GetRect();
                                if (r.HasPoint(childLocal)) { picked = c; break; }
                            }
                            else
                            {
                                // fallback: approximate with small hit area around position
                                var dist = (c.Position - mouseLocal).Length();
                                if (dist < 24) { picked = c; break; }
                            }
                        }
                    }

                    if (picked != null)
                    {
                        // Only start dragging if this node is marked draggable
                        if (IsNodeDraggable(picked.Name))
                        {
                            Input.SetDefaultCursorShape(Input.CursorShape.Drag);
                            isDragging = true;
                            draggedNode = picked;
                            dragOffset = mouseLocal - picked.Position;
                            NodeDragStarted?.Invoke(picked.Name);
                        }
                        else
                        {
                            // Not draggable: still fire selection
                            NodeSelected?.Invoke(picked.Name);
                        }
                    }
                }
                else
                {
                    // Mouse released
                    if (isDragging && draggedNode != null)
                    {
                        NodeDragEnded?.Invoke(draggedNode.Name);
                        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
                        // finalize
                        isDragging = false;
                        draggedNode = null;
                    }
                    else
                    {
                        // Not a drag: treat as a click selection (pick top-most)
                        var children = GetChildren();
                        Node2D? picked = null;
                        for (int i = children.Count - 1; i >= 0; i--)
                        {
                            if (children[i] is Node2D c && !string.IsNullOrEmpty(c.Name))
                            {
                                var childLocal = c.ToLocal(mouseGlobal);
                                if (c is Sprite2D s)
                                {
                                    var r = s.GetRect();
                                    if (r.HasPoint(childLocal)) { picked = c; break; }
                                }
                                else
                                {
                                    var dist = (c.Position - ToLocal(mouseGlobal)).Length();
                                    if (dist < 24) { picked = c; break; }
                                }
                            }
                        }
                        if (picked != null)
                            NodeSelected?.Invoke(picked.Name);
                        else
                            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
                    }
                }
            }
        }
    }

    public override void _Draw()
    {
        // Draw lines using children's Position. This keeps the canvas generic.
        foreach (var kv in Displays)
        {
            var name = kv.Key;
            var display = kv.Value;
            if (!Connections.TryGetValue(name, out var deps) || deps == null)
                continue;

            foreach (var dep in deps)
            {
                if (Displays.TryGetValue(dep, out var depDisplay))
                {
                    DrawLine(display.Position, depDisplay.Position, Colors.White, 8, true);
                }
            }
        }
    }
}

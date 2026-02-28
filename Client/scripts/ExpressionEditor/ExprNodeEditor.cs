using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

/// <summary>
/// Categories for expression nodes shown in the add-node menu.
/// </summary>
public enum ExprCategory
{
    Number,
    Condition,
    Effect,
    Selector,
    String,
    Utility
}

/// <summary>
/// Describes a type of node that can be created in the expression editor.
/// </summary>
public sealed class ExprNodeDefinition
{
    public string Name;
    public string DisplayName;
    public ExprCategory Category;
    public string Description;
    /// <summary>
    /// Input slots: (name, type). Type is "number", "bool", "effect", "selector", "string", or "any".
    /// </summary>
    public (string name, string type)[] Inputs;
    /// <summary>
    /// Output slots: (name, type).
    /// </summary>
    public (string name, string type)[] Outputs;
    /// <summary>
    /// Whether this node has an inline editable value (e.g. constant number, string literal).
    /// </summary>
    public string? InlineValueType;
    /// <summary>
    /// Additional properties this node can configure (e.g. stat name, variable index).
    /// </summary>
    public (string name, string type, string? defaultValue)[] Properties;
    /// <summary>
    /// Whether inputs can be added dynamically (for variadic ops like Add, And, etc.).
    /// </summary>
    public bool Variadic;

    public ExprNodeDefinition(string name, string displayName, ExprCategory category, string description,
        (string, string)[]? inputs = null, (string, string)[]? outputs = null,
        string? inlineValueType = null,
        (string, string, string?)[]? properties = null,
        bool variadic = false)
    {
        Name = name;
        DisplayName = displayName;
        Category = category;
        Description = description;
        Inputs = inputs ?? [];
        Outputs = outputs ?? [];
        InlineValueType = inlineValueType;
        Properties = properties ?? [];
        Variadic = variadic;
    }
}

/// <summary>
/// A visual node-based expression editor built on Godot's GraphEdit.
/// Lets users visually compose JSON-based expression trees for the Cognoscent RPG system.
/// </summary>
public partial class ExprNodeEditor : GraphEdit
{
    /// <summary>
    /// The expected output type for the root expression ("number", "bool", "effect", "selector", "string").
    /// </summary>
    public string OutputType { get; set; } = "number";

    /// <summary>
    /// Fired when the graph is modified.
    /// </summary>
    public event Action? OnGraphChanged;

    private PopupMenu? _addNodeMenu;
    private Vector2 _lastRightClickPos;
    private int _nextNodeId;
    private readonly Dictionary<string, ExprNodeDefinition> _nodeDefinitions = new();
    private GraphNode? _outputNode;

    // Colors for each expression type
    private static readonly Dictionary<string, Color> TypeColors = new()
    {
        ["number"] = new Color(0.4f, 0.7f, 1.0f),   // Blue
        ["bool"] = new Color(0.9f, 0.4f, 0.4f),      // Red
        ["effect"] = new Color(0.6f, 0.3f, 0.9f),     // Purple
        ["selector"] = new Color(0.3f, 0.9f, 0.5f),   // Green
        ["string"] = new Color(1.0f, 0.8f, 0.3f),     // Yellow
        ["any"] = new Color(0.7f, 0.7f, 0.7f),        // Gray
    };

    public override void _Ready()
    {
        base._Ready();
        RegisterAllNodeDefinitions();
        BuildAddNodeMenu();

        ConnectionRequest += OnConnectionRequest;
        DisconnectionRequest += OnDisconnectionRequest;

        // Right-click for add menu
        GuiInput += OnGraphGuiInput;

        // Style
        ShowGrid = true;
        SnappingEnabled = true;
        SnappingDistance = 20;

        MinimapEnabled = true;
    }

    #region Node Definitions

    private void RegisterAllNodeDefinitions()
    {
        // === NUMBER ===
        RegisterDef(new("const_number", "Number", ExprCategory.Number, "A constant numeric value.",
            outputs: [("value", "number")], inlineValueType: "float"));

        RegisterDef(new("var_number", "Variable (Number)", ExprCategory.Number, "Read a numeric variable by index.",
            outputs: [("value", "number")],
            properties: [("index", "int", "0")]));

        RegisterDef(new("add", "Add (+)", ExprCategory.Number, "Sum of inputs.",
            inputs: [("a", "number"), ("b", "number")],
            outputs: [("result", "number")], variadic: true));

        RegisterDef(new("sub", "Subtract (-)", ExprCategory.Number, "Subtraction.",
            inputs: [("a", "number"), ("b", "number")],
            outputs: [("result", "number")], variadic: true));

        RegisterDef(new("mul", "Multiply (*)", ExprCategory.Number, "Multiplication.",
            inputs: [("a", "number"), ("b", "number")],
            outputs: [("result", "number")], variadic: true));

        RegisterDef(new("div", "Divide (/)", ExprCategory.Number, "Division.",
            inputs: [("a", "number"), ("b", "number")],
            outputs: [("result", "number")], variadic: true));

        RegisterDef(new("lerp", "Lerp", ExprCategory.Number, "Linear interpolation between min and max.",
            inputs: [("min", "number"), ("max", "number"), ("t", "number")],
            outputs: [("result", "number")]));

        RegisterDef(new("range", "Random Range", ExprCategory.Number, "Random value between min and max.",
            inputs: [("min", "number"), ("max", "number")],
            outputs: [("result", "number")]));

        RegisterDef(new("dice", "Dice Roll", ExprCategory.Number, "Roll dice in NdM format (e.g. 2d6).",
            outputs: [("result", "number")],
            properties: [("notation", "string", "1d6")]));

        RegisterDef(new("stat", "Read Stat", ExprCategory.Number, "Read a stat value from an entity.",
            inputs: [("entity", "selector"), ("default", "number")],
            outputs: [("value", "number")],
            properties: [("stat_name", "string", "strength")]));

        // === CONDITION ===
        RegisterDef(new("const_bool", "Boolean", ExprCategory.Condition, "A constant true/false value.",
            outputs: [("value", "bool")], inlineValueType: "bool"));

        RegisterDef(new("var_bool", "Variable (Bool)", ExprCategory.Condition, "Read a boolean variable by index.",
            outputs: [("value", "bool")],
            properties: [("index", "int", "0")]));

        RegisterDef(new("and", "And (&&)", ExprCategory.Condition, "All inputs must be true.",
            inputs: [("a", "bool"), ("b", "bool")],
            outputs: [("result", "bool")], variadic: true));

        RegisterDef(new("or", "Or (||)", ExprCategory.Condition, "At least one input must be true.",
            inputs: [("a", "bool"), ("b", "bool")],
            outputs: [("result", "bool")], variadic: true));

        RegisterDef(new("not", "Not (!)", ExprCategory.Condition, "Negates the input.",
            inputs: [("input", "bool")],
            outputs: [("result", "bool")]));

        RegisterDef(new("greater_than", "Greater Than (>)", ExprCategory.Condition, "True if left > right.",
            inputs: [("left", "number"), ("right", "number")],
            outputs: [("result", "bool")]));

        RegisterDef(new("less_than", "Less Than (<)", ExprCategory.Condition, "True if left < right.",
            inputs: [("left", "number"), ("right", "number")],
            outputs: [("result", "bool")]));

        RegisterDef(new("equal", "Equal (==)", ExprCategory.Condition, "True if left == right.",
            inputs: [("left", "number"), ("right", "number")],
            outputs: [("result", "bool")]));

        RegisterDef(new("not_equal", "Not Equal (!=)", ExprCategory.Condition, "True if left != right.",
            inputs: [("left", "number"), ("right", "number")],
            outputs: [("result", "bool")]));

        RegisterDef(new("random_chance", "Random Chance (%)", ExprCategory.Condition, "True with given probability (0.0 - 1.0).",
            inputs: [("probability", "number")],
            outputs: [("result", "bool")]));

        // === EFFECT ===
        RegisterDef(new("no_effect", "No Effect", ExprCategory.Effect, "Does nothing.",
            outputs: [("effect", "effect")]));

        RegisterDef(new("composite", "Composite Effect", ExprCategory.Effect, "Runs multiple effects in sequence.",
            inputs: [("effect_1", "effect"), ("effect_2", "effect")],
            outputs: [("effect", "effect")], variadic: true));

        RegisterDef(new("set_stat", "Set Stat", ExprCategory.Effect, "Sets a stat to a value.",
            inputs: [("target", "selector"), ("value", "number")],
            outputs: [("effect", "effect")],
            properties: [("stat_name", "string", "strength")]));

        RegisterDef(new("add_stat", "Add to Stat", ExprCategory.Effect, "Adds a value to a stat.",
            inputs: [("target", "selector"), ("value", "number")],
            outputs: [("effect", "effect")],
            properties: [("stat_name", "string", "strength")]));

        RegisterDef(new("sub_stat", "Subtract from Stat", ExprCategory.Effect, "Subtracts a value from a stat.",
            inputs: [("target", "selector"), ("value", "number")],
            outputs: [("effect", "effect")],
            properties: [("stat_name", "string", "strength")]));

        RegisterDef(new("add_injury", "Add Injury", ExprCategory.Effect, "Inflicts an injury on a target.",
            inputs: [("target", "selector"), ("severity", "number")],
            outputs: [("effect", "effect")],
            properties: [("injury_type", "string", "cut")]));

        RegisterDef(new("heal_injury", "Heal Injury", ExprCategory.Effect, "Heals an injury type on a target.",
            inputs: [("target", "selector"), ("amount", "number")],
            outputs: [("effect", "effect")],
            properties: [("injury_type", "string", "cut")]));

        RegisterDef(new("add_feature", "Add Feature", ExprCategory.Effect, "Grants a feature to a target.",
            inputs: [("target", "selector"), ("ticks", "number")],
            outputs: [("effect", "effect")],
            properties: [("feature", "string", "")]));

        RegisterDef(new("remove_feature", "Remove Feature", ExprCategory.Effect, "Removes a feature from a target.",
            inputs: [("target", "selector")],
            outputs: [("effect", "effect")],
            properties: [("feature", "string", "")]));

        // === SELECTOR ===
        RegisterDef(new("self_selector", "Self / Caller", ExprCategory.Selector, "References the entity executing the expression.",
            outputs: [("entity", "selector")]));

        RegisterDef(new("target_selector", "Target", ExprCategory.Selector, "References the target entity.",
            outputs: [("entity", "selector")]));

        RegisterDef(new("target_part_selector", "Target Part", ExprCategory.Selector, "References the targeted body part.",
            outputs: [("entity", "selector")]));

        RegisterDef(new("var_selector", "Variable (Entity)", ExprCategory.Selector, "Read an entity variable by index.",
            outputs: [("entity", "selector")],
            properties: [("index", "int", "0")]));

        RegisterDef(new("no_entity", "No Entity (null)", ExprCategory.Selector, "Null entity reference.",
            outputs: [("entity", "selector")]));

        RegisterDef(new("body_part_by_tag", "Body Part by Tag", ExprCategory.Selector, "Find body part by tag.",
            inputs: [("entity", "selector"), ("tag", "string")],
            outputs: [("part", "selector")]));

        RegisterDef(new("body_part_by_name", "Body Part by Name", ExprCategory.Selector, "Find body part by name.",
            inputs: [("entity", "selector"), ("name", "string")],
            outputs: [("part", "selector")]));

        // === STRING ===
        RegisterDef(new("const_string", "String Literal", ExprCategory.String, "A constant string value.",
            outputs: [("value", "string")], inlineValueType: "string"));

        RegisterDef(new("var_string", "Variable (String)", ExprCategory.String, "Read a string variable by index.",
            outputs: [("value", "string")],
            properties: [("index", "int", "0")]));

        RegisterDef(new("string_concat", "Concatenate", ExprCategory.String, "Joins strings together.",
            inputs: [("a", "string"), ("b", "string")],
            outputs: [("result", "string")], variadic: true));

        // === UTILITY ===
        RegisterDef(new("if_number", "If (Number)", ExprCategory.Utility, "Conditional: returns true_val or false_val based on condition.",
            inputs: [("condition", "bool"), ("true_val", "number"), ("false_val", "number")],
            outputs: [("result", "number")]));

        RegisterDef(new("if_bool", "If (Bool)", ExprCategory.Utility, "Conditional: returns true_val or false_val based on condition.",
            inputs: [("condition", "bool"), ("true_val", "bool"), ("false_val", "bool")],
            outputs: [("result", "bool")]));

        RegisterDef(new("if_string", "If (String)", ExprCategory.Utility, "Conditional: returns true_val or false_val based on condition.",
            inputs: [("condition", "bool"), ("true_val", "string"), ("false_val", "string")],
            outputs: [("result", "string")]));

        RegisterDef(new("if_selector", "If (Selector)", ExprCategory.Utility, "Conditional: returns true_val or false_val based on condition.",
            inputs: [("condition", "bool"), ("true_val", "selector"), ("false_val", "selector")],
            outputs: [("result", "selector")]));
    }

    private void RegisterDef(ExprNodeDefinition def)
    {
        _nodeDefinitions[def.Name] = def;
    }

    #endregion

    #region Add Node Menu

    private void BuildAddNodeMenu()
    {
        _addNodeMenu = new PopupMenu();
        _addNodeMenu.Name = "AddNodeMenu";
        AddChild(_addNodeMenu);

        // Organize by category
        var categories = Enum.GetValues<ExprCategory>();
        foreach (var cat in categories)
        {
            var submenu = new PopupMenu();
            submenu.Name = $"Submenu_{cat}";

            var defsInCat = _nodeDefinitions.Values
                .Where(d => d.Category == cat)
                .OrderBy(d => d.DisplayName)
                .ToArray();

            for (int i = 0; i < defsInCat.Length; i++)
            {
                submenu.AddItem(defsInCat[i].DisplayName, i);
                submenu.SetItemMetadata(i, defsInCat[i].Name);
                submenu.SetItemTooltip(i, defsInCat[i].Description);
            }

            submenu.IdPressed += (id) =>
            {
                string defName = submenu.GetItemMetadata((int)id).AsString();
                CreateNodeAtPosition(defName, _lastRightClickPos);
            };

            AddChild(submenu);
            _addNodeMenu.AddSubmenuItem(cat.ToString(), submenu.Name);
        }
    }

    private void OnGraphGuiInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } mb)
        {
            _lastRightClickPos = (GetLocalMousePosition() + ScrollOffset) / Zoom;
            _addNodeMenu?.PopupOnParent(new Rect2I((Vector2I)mb.GlobalPosition, Vector2I.Zero));
        }
    }

    #endregion

    #region Node Creation

    /// <summary>
    /// Creates the "Output" root node that represents the final result of the expression graph.
    /// </summary>
    public void CreateOutputNode(string type)
    {
        OutputType = type;
        if (_outputNode != null)
        {
            _outputNode.QueueFree();
            _outputNode = null;
        }

        var node = new GraphNode();
        node.Title = $"Output ({type})";
        node.Name = "output";
        node.PositionOffset = new Vector2(600, 200);

        // Single input slot matching the output type
        var label = new Label { Text = type };
        node.AddChild(label);
        node.SetSlotEnabledLeft(0, true);
        node.SetSlotTypeLeft(0, GetSlotType(type));
        node.SetSlotColorLeft(0, GetTypeColor(type));

        // Style the output node distinctively
        AddChild(node);
        _outputNode = node;
    }

    /// <summary>
    /// Creates a new expression node at the given position.
    /// </summary>
    public GraphNode CreateNodeAtPosition(string defName, Vector2 position)
    {
        if (!_nodeDefinitions.TryGetValue(defName, out var def))
            throw new ArgumentException($"Unknown node definition: {defName}");

        var node = new GraphNode();
        node.Name = $"expr_{_nextNodeId++}";
        node.Title = def.DisplayName;
        node.PositionOffset = position;
        node.SetMeta("def_name", defName);

        int slotIndex = 0;

        // Properties (editable fields on the node)
        foreach (var prop in def.Properties)
        {
            var hbox = new HBoxContainer();
            hbox.AddChild(new Label { Text = prop.name, CustomMinimumSize = new Vector2(80, 0) });

            Control editor;
            switch (prop.type)
            {
                case "int":
                    var spinInt = new SpinBox { MinValue = 0, MaxValue = 99, Step = 1, Value = int.TryParse(prop.defaultValue, out var iv) ? iv : 0 };
                    spinInt.Name = $"prop_{prop.name}";
                    editor = spinInt;
                    break;
                case "float":
                    var spinFloat = new SpinBox { MinValue = -99999, MaxValue = 99999, Step = 0.01, Value = float.TryParse(prop.defaultValue, out var fv) ? fv : 0 };
                    spinFloat.Name = $"prop_{prop.name}";
                    editor = spinFloat;
                    break;
                default: // string
                    var lineEdit = new LineEdit { Text = prop.defaultValue ?? "", CustomMinimumSize = new Vector2(120, 0) };
                    lineEdit.Name = $"prop_{prop.name}";
                    editor = lineEdit;
                    break;
            }
            hbox.AddChild(editor);
            node.AddChild(hbox);
            node.SetSlotEnabledLeft(slotIndex, false);
            node.SetSlotEnabledRight(slotIndex, false);
            slotIndex++;
        }

        // Inline value (for constants)
        if (def.InlineValueType != null)
        {
            Control inlineEditor;
            switch (def.InlineValueType)
            {
                case "float":
                    var spin = new SpinBox
                    {
                        MinValue = -99999, MaxValue = 99999, Step = 0.01, Value = 0,
                        CustomMinimumSize = new Vector2(120, 0)
                    };
                    spin.Name = "inline_value";
                    inlineEditor = spin;
                    break;
                case "bool":
                    var check = new CheckBox { Text = "Value", ButtonPressed = false };
                    check.Name = "inline_value";
                    inlineEditor = check;
                    break;
                default: // "string"
                    var lineEdit = new LineEdit { Text = "", PlaceholderText = "value...", CustomMinimumSize = new Vector2(150, 0) };
                    lineEdit.Name = "inline_value";
                    inlineEditor = lineEdit;
                    break;
            }
            node.AddChild(inlineEditor);
            node.SetSlotEnabledLeft(slotIndex, false);
            node.SetSlotEnabledRight(slotIndex, false);
            slotIndex++;
        }

        // Input slots
        int inputStartSlot = slotIndex;
        for (int i = 0; i < def.Inputs.Length; i++)
        {
            var input = def.Inputs[i];
            var label = new Label { Text = $"  {input.name}" };
            node.AddChild(label);
            node.SetSlotEnabledLeft(slotIndex, true);
            node.SetSlotTypeLeft(slotIndex, GetSlotType(input.type));
            node.SetSlotColorLeft(slotIndex, GetTypeColor(input.type));
            node.SetSlotEnabledRight(slotIndex, false);
            slotIndex++;
        }

        // Output slots
        if (def.Outputs.Length > 0)
        {
            // If no inputs were added, create a row for the first output
            // Otherwise, mark the first row as also having a right slot
            for (int i = 0; i < def.Outputs.Length; i++)
            {
                var output = def.Outputs[i];
                int targetSlot;
                if (i < inputStartSlot)
                {
                    // Reuse an existing row
                    targetSlot = i;
                }
                else if (i < slotIndex)
                {
                    // Reuse an input row
                    targetSlot = i;
                }
                else
                {
                    // Need a new row
                    var label = new Label { Text = $"{output.name}  ", HorizontalAlignment = HorizontalAlignment.Right };
                    node.AddChild(label);
                    targetSlot = slotIndex;
                    slotIndex++;
                }
                node.SetSlotEnabledRight(targetSlot, true);
                node.SetSlotTypeRight(targetSlot, GetSlotType(output.type));
                node.SetSlotColorRight(targetSlot, GetTypeColor(output.type));
            }
        }

        // Variadic: add a button to add more inputs
        if (def.Variadic)
        {
            var addBtn = new Button { Text = "+ Add Input" };
            addBtn.Name = "add_input_btn";
            int currentInputCount = def.Inputs.Length;
            addBtn.Pressed += () =>
            {
                currentInputCount++;
                string inputType = def.Inputs.Length > 0 ? def.Inputs[0].type : "any";
                var label = new Label { Text = $"  input_{currentInputCount}" };
                // Insert before the add button
                int btnIdx = addBtn.GetIndex();
                node.AddChild(label);
                node.MoveChild(label, btnIdx);

                int newSlotIdx = btnIdx;
                node.SetSlotEnabledLeft(newSlotIdx, true);
                node.SetSlotTypeLeft(newSlotIdx, GetSlotType(inputType));
                node.SetSlotColorLeft(newSlotIdx, GetTypeColor(inputType));
                node.SetSlotEnabledRight(newSlotIdx, false);

                OnGraphChanged?.Invoke();
            };
            node.AddChild(addBtn);
            slotIndex++;
        }

        AddChild(node);
        node.Dragged += (_, _) => OnGraphChanged?.Invoke();
        node.CloseRequest += () =>
        {
            RemoveNodeConnections(node);
            node.QueueFree();
            OnGraphChanged?.Invoke();
        };
        node.Resizable = true;

        OnGraphChanged?.Invoke();
        return node;
    }

    private void RemoveNodeConnections(GraphNode node)
    {
        var connections = GetConnectionList();
        foreach (var conn in connections)
        {
            var fromName = conn["from_node"].AsStringName();
            var toName = conn["to_node"].AsStringName();
            if (fromName == node.Name || toName == node.Name)
            {
                DisconnectNode(fromName, conn["from_port"].AsInt32(), toName, conn["to_port"].AsInt32());
            }
        }
    }

    #endregion

    #region Connections

    private void OnConnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        // Get types
        var fromGraphNode = GetNodeOrNull<GraphNode>(fromNode.ToString());
        var toGraphNode = GetNodeOrNull<GraphNode>(toNode.ToString());
        if (fromGraphNode == null || toGraphNode == null) return;

        int fromType = fromGraphNode.GetSlotTypeRight((int)fromPort);
        int toType = toGraphNode.GetSlotTypeLeft((int)toPort);

        // Type compatibility check (same type or "any" type)
        if (fromType != toType && fromType != GetSlotType("any") && toType != GetSlotType("any"))
            return;

        // Remove existing connection to target port (one input per port)
        var connections = GetConnectionList();
        foreach (var conn in connections)
        {
            if (conn["to_node"].AsStringName() == toNode && conn["to_port"].AsInt32() == (int)toPort)
            {
                DisconnectNode(conn["from_node"].AsStringName(), conn["from_port"].AsInt32(), toNode, (int)toPort);
                break;
            }
        }

        ConnectNode(fromNode, (int)fromPort, toNode, (int)toPort);
        OnGraphChanged?.Invoke();
    }

    private void OnDisconnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        DisconnectNode(fromNode, (int)fromPort, toNode, (int)toPort);
        OnGraphChanged?.Invoke();
    }

    #endregion

    #region Slot Types

    private static int GetSlotType(string type) => type switch
    {
        "number" => 0,
        "bool" => 1,
        "effect" => 2,
        "selector" => 3,
        "string" => 4,
        "any" => 5,
        _ => 5
    };

    public static string GetSlotTypeName(int type) => type switch
    {
        0 => "number",
        1 => "bool",
        2 => "effect",
        3 => "selector",
        4 => "string",
        _ => "any"
    };

    private static Color GetTypeColor(string type)
    {
        return TypeColors.GetValueOrDefault(type, TypeColors["any"]);
    }

    #endregion

    #region Node Access Helpers

    /// <summary>
    /// Gets the definition name stored on a graph node.
    /// </summary>
    public string? GetNodeDefName(GraphNode node)
    {
        return node.HasMeta("def_name") ? node.GetMeta("def_name").AsString() : null;
    }

    /// <summary>
    /// Gets the definition for a given graph node.
    /// </summary>
    public ExprNodeDefinition? GetNodeDefinition(GraphNode node)
    {
        var name = GetNodeDefName(node);
        return name != null && _nodeDefinitions.TryGetValue(name, out var def) ? def : null;
    }

    /// <summary>
    /// Gets a property value from a node by property name.
    /// </summary>
    public string GetNodeProperty(GraphNode node, string propertyName)
    {
        var control = node.FindChild($"prop_{propertyName}", recursive: true);
        return control switch
        {
            SpinBox sb => sb.Value.ToString(),
            LineEdit le => le.Text,
            _ => ""
        };
    }

    /// <summary>
    /// Gets the inline value from a constant node.
    /// </summary>
    public object? GetNodeInlineValue(GraphNode node)
    {
        var control = node.FindChild("inline_value", recursive: true);
        return control switch
        {
            SpinBox sb => (float)sb.Value,
            CheckBox cb => cb.ButtonPressed,
            LineEdit le => le.Text,
            _ => null
        };
    }

    /// <summary>
    /// Gets the output node.
    /// </summary>
    public GraphNode? GetOutputNode() => _outputNode;

    /// <summary>
    /// Gets all expression graph nodes (excluding the output node).
    /// </summary>
    public IEnumerable<GraphNode> GetExpressionNodes()
    {
        foreach (var child in GetChildren())
        {
            if (child is GraphNode gn && gn != _outputNode)
                yield return gn;
        }
    }

    /// <summary>
    /// Finds the node connected to a given input port.
    /// Returns (fromNode, fromPort) or null if nothing is connected.
    /// </summary>
    public (GraphNode node, int port)? GetInputConnection(GraphNode targetNode, int inputPort)
    {
        var connections = GetConnectionList();
        foreach (var conn in connections)
        {
            if (conn["to_node"].AsStringName() == targetNode.Name && conn["to_port"].AsInt32() == inputPort)
            {
                var fromNode = GetNodeOrNull<GraphNode>(conn["from_node"].AsStringName().ToString());
                if (fromNode != null)
                    return (fromNode, conn["from_port"].AsInt32());
            }
        }
        return null;
    }

    /// <summary>
    /// Counts the actual number of connected inputs on a node (for variadic nodes).
    /// </summary>
    public int CountConnectedInputs(GraphNode node)
    {
        int count = 0;
        var connections = GetConnectionList();
        foreach (var conn in connections)
        {
            if (conn["to_node"].AsStringName() == node.Name)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Gets all connected inputs for a node, ordered by port index.
    /// </summary>
    public List<(GraphNode node, int fromPort, int toPort)> GetAllInputConnections(GraphNode targetNode)
    {
        var result = new List<(GraphNode, int, int)>();
        var connections = GetConnectionList();
        foreach (var conn in connections)
        {
            if (conn["to_node"].AsStringName() == targetNode.Name)
            {
                var fromNode = GetNodeOrNull<GraphNode>(conn["from_node"].AsStringName().ToString());
                if (fromNode != null)
                    result.Add((fromNode, conn["from_port"].AsInt32(), conn["to_port"].AsInt32()));
            }
        }
        result.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        return result;
    }

    /// <summary>
    /// Clears the entire graph.
    /// </summary>
    public void ClearGraph()
    {
        ClearConnections();
        foreach (var child in GetChildren().OfType<GraphNode>().ToArray())
        {
            child.QueueFree();
        }
        _outputNode = null;
        _nextNodeId = 0;
    }

    #endregion
}

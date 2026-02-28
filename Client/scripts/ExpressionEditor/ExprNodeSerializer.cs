using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.ui;

/// <summary>
/// Serializes an ExprNodeEditor graph to/from JSON expressions
/// compatible with ExpressionCompiler.
/// </summary>
public static class ExprNodeSerializer
{
    #region Graph → JSON

    /// <summary>
    /// Serializes the entire graph rooted at the output node into a JsonElement.
    /// </summary>
    public static JsonElement? SerializeGraph(ExprNodeEditor editor)
    {
        var outputNode = editor.GetOutputNode();
        if (outputNode == null) return null;

        // Find what's connected to the output node's single input
        var connection = editor.GetInputConnection(outputNode, 0);
        if (connection == null) return null;

        return SerializeNode(editor, connection.Value.node);
    }

    /// <summary>
    /// Serializes a single node and its input subtree into a JsonElement.
    /// </summary>
    private static JsonElement SerializeNode(ExprNodeEditor editor, GraphNode node)
    {
        var defName = editor.GetNodeDefName(node);
        if (defName == null)
            throw new Exception($"Node {node.Name} has no definition.");

        return defName switch
        {
            // === Constants ===
            "const_number" => SerializeConstNumber(editor, node),
            "const_bool" => SerializeConstBool(editor, node),
            "const_string" => SerializeConstString(editor, node),

            // === Variables ===
            "var_number" or "var_bool" or "var_string" or "var_selector"
                => SerializeVariable(editor, node),

            // === Dice ===
            "dice" => SerializeDice(editor, node),

            // === Math Operations ===
            "add" => SerializeMathOp(editor, node, "sum"),
            "sub" => SerializeMathOp(editor, node, "sub"),
            "mul" => SerializeMathOp(editor, node, "mul"),
            "div" => SerializeMathOp(editor, node, "div"),
            "lerp" => SerializeLerp(editor, node),
            "range" => SerializeRange(editor, node),

            // === Stat ===
            "stat" => SerializeStat(editor, node),

            // === Conditions ===
            "and" => SerializeLogicGate(editor, node, "and"),
            "or" => SerializeLogicGate(editor, node, "or"),
            "not" => SerializeNot(editor, node),
            "greater_than" => SerializeComparison(editor, node, ">"),
            "less_than" => SerializeComparison(editor, node, "<"),
            "equal" => SerializeComparison(editor, node, "=="),
            "not_equal" => SerializeComparison(editor, node, "!="),
            "random_chance" => SerializeRandomChance(editor, node),

            // === Effects ===
            "no_effect" => SerializeNoEffect(),
            "composite" => SerializeComposite(editor, node),
            "set_stat" => SerializeStatEffect(editor, node, "set_stat"),
            "add_stat" => SerializeStatEffect(editor, node, "add_stat"),
            "sub_stat" => SerializeStatEffect(editor, node, "sub_stat"),
            "add_injury" => SerializeAddInjury(editor, node),
            "heal_injury" => SerializeHealInjury(editor, node),
            "add_feature" => SerializeAddFeature(editor, node),
            "remove_feature" => SerializeRemoveFeature(editor, node),

            // === Selectors ===
            "self_selector" => JsonDocument.Parse("\"self\"").RootElement.Clone(),
            "target_selector" => JsonDocument.Parse("\"target\"").RootElement.Clone(),
            "target_part_selector" => JsonDocument.Parse("\"target_part\"").RootElement.Clone(),
            "no_entity" => JsonDocument.Parse("null").RootElement.Clone(),
            "body_part_by_tag" => SerializeBodyPartSelector(editor, node, "part_by_tag", "tag"),
            "body_part_by_name" => SerializeBodyPartSelector(editor, node, "part_by_name", "name"),

            // === String ===
            "string_concat" => SerializeStringConcat(editor, node),

            // === Conditionals ===
            "if_number" or "if_bool" or "if_string" or "if_selector"
                => SerializeConditional(editor, node),

            _ => throw new Exception($"Unknown node definition: {defName}")
        };
    }

    #region Constant Serializers

    private static JsonElement SerializeConstNumber(ExprNodeEditor editor, GraphNode node)
    {
        var value = editor.GetNodeInlineValue(node);
        float num = value is float f ? f : 0;
        return JsonDocument.Parse(num.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement.Clone();
    }

    private static JsonElement SerializeConstBool(ExprNodeEditor editor, GraphNode node)
    {
        var value = editor.GetNodeInlineValue(node);
        bool b = value is bool bv && bv;
        return JsonDocument.Parse(b ? "true" : "false").RootElement.Clone();
    }

    private static JsonElement SerializeConstString(ExprNodeEditor editor, GraphNode node)
    {
        var value = editor.GetNodeInlineValue(node);
        string s = value is string sv ? sv : "";
        return JsonSerializer.SerializeToElement(s);
    }

    #endregion

    #region Variable Serializers

    private static JsonElement SerializeVariable(ExprNodeEditor editor, GraphNode node)
    {
        string index = editor.GetNodeProperty(node, "index");
        return JsonSerializer.SerializeToElement($"${index}");
    }

    #endregion

    #region Number Serializers

    private static JsonElement SerializeDice(ExprNodeEditor editor, GraphNode node)
    {
        string notation = editor.GetNodeProperty(node, "notation");
        return JsonSerializer.SerializeToElement(notation);
    }

    private static JsonElement SerializeMathOp(ExprNodeEditor editor, GraphNode node, string op)
    {
        var inputs = editor.GetAllInputConnections(node);
        var numbers = new JsonArray();
        foreach (var (inputNode, _, _) in inputs)
        {
            numbers.Add(JsonNode.Parse(SerializeNode(editor, inputNode).GetRawText()));
        }

        // If no connections, add zeros
        if (numbers.Count == 0)
        {
            numbers.Add(0);
            numbers.Add(0);
        }

        var obj = new JsonObject
        {
            ["op"] = op,
            ["numbers"] = numbers
        };
        return obj.ToElement();
    }

    private static JsonElement SerializeLerp(ExprNodeEditor editor, GraphNode node)
    {
        var obj = new JsonObject { ["op"] = "lerp" };
        var minConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        var maxConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        var tConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 2));

        obj["min"] = minConn != null ? JsonNode.Parse(SerializeNode(editor, minConn.Value.node).GetRawText()) : 0;
        obj["max"] = maxConn != null ? JsonNode.Parse(SerializeNode(editor, maxConn.Value.node).GetRawText()) : 1;
        obj["t"] = tConn != null ? JsonNode.Parse(SerializeNode(editor, tConn.Value.node).GetRawText()) : 0.5f;

        return obj.ToElement();
    }

    private static JsonElement SerializeRange(ExprNodeEditor editor, GraphNode node)
    {
        var obj = new JsonObject { ["op"] = "rand" };
        var minConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        var maxConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));

        obj["min"] = minConn != null ? JsonNode.Parse(SerializeNode(editor, minConn.Value.node).GetRawText()) : 0;
        obj["max"] = maxConn != null ? JsonNode.Parse(SerializeNode(editor, maxConn.Value.node).GetRawText()) : 1;

        return obj.ToElement();
    }

    private static JsonElement SerializeStat(ExprNodeEditor editor, GraphNode node)
    {
        string statName = editor.GetNodeProperty(node, "stat_name");
        var obj = new JsonObject
        {
            ["op"] = "stat",
            ["stat"] = statName
        };

        var entityConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        if (entityConn != null)
            obj["entity"] = JsonNode.Parse(SerializeNode(editor, entityConn.Value.node).GetRawText());
        else
            obj["entity"] = "self";

        var defaultConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (defaultConn != null)
            obj["default"] = JsonNode.Parse(SerializeNode(editor, defaultConn.Value.node).GetRawText());

        return obj.ToElement();
    }

    #endregion

    #region Condition Serializers

    private static JsonElement SerializeLogicGate(ExprNodeEditor editor, GraphNode node, string op)
    {
        var inputs = editor.GetAllInputConnections(node);
        var args = new JsonArray();
        foreach (var (inputNode, _, _) in inputs)
        {
            args.Add(JsonNode.Parse(SerializeNode(editor, inputNode).GetRawText()));
        }
        var obj = new JsonObject
        {
            ["op"] = op,
            ["conditions"] = args
        };
        return obj.ToElement();
    }

    private static JsonElement SerializeNot(ExprNodeEditor editor, GraphNode node)
    {
        var obj = new JsonObject { ["op"] = "not" };
        var inputConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        if (inputConn != null)
            obj["condition"] = JsonNode.Parse(SerializeNode(editor, inputConn.Value.node).GetRawText());
        else
            obj["condition"] = true;
        return obj.ToElement();
    }

    private static JsonElement SerializeComparison(ExprNodeEditor editor, GraphNode node, string op)
    {
        var obj = new JsonObject { ["op"] = op };
        var leftConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        var rightConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));

        obj["left"] = leftConn != null ? JsonNode.Parse(SerializeNode(editor, leftConn.Value.node).GetRawText()) : 0;
        obj["right"] = rightConn != null ? JsonNode.Parse(SerializeNode(editor, rightConn.Value.node).GetRawText()) : 0;

        return obj.ToElement();
    }

    private static JsonElement SerializeRandomChance(ExprNodeEditor editor, GraphNode node)
    {
        var obj = new JsonObject { ["op"] = "rand" };
        var probConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        if (probConn != null)
            obj["probability"] = JsonNode.Parse(SerializeNode(editor, probConn.Value.node).GetRawText());
        else
            obj["probability"] = 0.5f;
        return obj.ToElement();
    }

    #endregion

    #region Effect Serializers

    private static JsonElement SerializeNoEffect()
    {
        return JsonDocument.Parse("\"nop\"").RootElement.Clone();
    }

    private static JsonElement SerializeComposite(ExprNodeEditor editor, GraphNode node)
    {
        var inputs = editor.GetAllInputConnections(node);
        var effects = new JsonArray();
        foreach (var (inputNode, _, _) in inputs)
        {
            effects.Add(JsonNode.Parse(SerializeNode(editor, inputNode).GetRawText()));
        }
        var obj = new JsonObject
        {
            ["effect"] = "composite",
            ["effects"] = effects
        };
        return obj.ToElement();
    }

    private static JsonElement SerializeStatEffect(ExprNodeEditor editor, GraphNode node, string effectName)
    {
        string statName = editor.GetNodeProperty(node, "stat_name");
        var obj = new JsonObject
        {
            ["effect"] = effectName,
            ["stat"] = statName
        };

        var targetConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        obj["target"] = targetConn != null
            ? JsonNode.Parse(SerializeNode(editor, targetConn.Value.node).GetRawText())
            : "self";

        var valueConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (valueConn != null)
            obj["value"] = JsonNode.Parse(SerializeNode(editor, valueConn.Value.node).GetRawText());
        else
            obj["value"] = 0;

        return obj.ToElement();
    }

    private static JsonElement SerializeAddInjury(ExprNodeEditor editor, GraphNode node)
    {
        string injuryType = editor.GetNodeProperty(node, "injury_type");
        var obj = new JsonObject
        {
            ["effect"] = "add_injury",
            ["type"] = injuryType
        };

        var targetConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        obj["target"] = targetConn != null
            ? JsonNode.Parse(SerializeNode(editor, targetConn.Value.node).GetRawText())
            : "self";

        var sevConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (sevConn != null)
            obj["severity"] = JsonNode.Parse(SerializeNode(editor, sevConn.Value.node).GetRawText());
        else
            obj["severity"] = 1;

        return obj.ToElement();
    }

    private static JsonElement SerializeHealInjury(ExprNodeEditor editor, GraphNode node)
    {
        string injuryType = editor.GetNodeProperty(node, "injury_type");
        var obj = new JsonObject
        {
            ["effect"] = "heal_injury",
            ["type"] = injuryType
        };

        var targetConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        obj["target"] = targetConn != null
            ? JsonNode.Parse(SerializeNode(editor, targetConn.Value.node).GetRawText())
            : "self";

        var amountConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (amountConn != null)
            obj["amount"] = JsonNode.Parse(SerializeNode(editor, amountConn.Value.node).GetRawText());
        else
            obj["amount"] = 1;

        return obj.ToElement();
    }

    private static JsonElement SerializeAddFeature(ExprNodeEditor editor, GraphNode node)
    {
        string feature = editor.GetNodeProperty(node, "feature");
        var obj = new JsonObject
        {
            ["effect"] = "add_feature",
            ["feature"] = feature
        };

        var targetConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        obj["target"] = targetConn != null
            ? JsonNode.Parse(SerializeNode(editor, targetConn.Value.node).GetRawText())
            : "self";

        var ticksConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (ticksConn != null)
            obj["ticks"] = JsonNode.Parse(SerializeNode(editor, ticksConn.Value.node).GetRawText());

        return obj.ToElement();
    }

    private static JsonElement SerializeRemoveFeature(ExprNodeEditor editor, GraphNode node)
    {
        string feature = editor.GetNodeProperty(node, "feature");
        var obj = new JsonObject
        {
            ["effect"] = "remove_feature",
            ["feature"] = feature
        };

        var targetConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        obj["target"] = targetConn != null
            ? JsonNode.Parse(SerializeNode(editor, targetConn.Value.node).GetRawText())
            : "self";

        return obj.ToElement();
    }

    #endregion

    #region Selector Serializers

    private static JsonElement SerializeBodyPartSelector(ExprNodeEditor editor, GraphNode node, string op, string paramName)
    {
        var obj = new JsonObject { ["op"] = op };

        var targetConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        obj["target"] = targetConn != null
            ? JsonNode.Parse(SerializeNode(editor, targetConn.Value.node).GetRawText())
            : "self";

        var paramConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (paramConn != null)
            obj[paramName] = JsonNode.Parse(SerializeNode(editor, paramConn.Value.node).GetRawText());
        else
            obj[paramName] = "";

        return obj.ToElement();
    }

    #endregion

    #region String Serializers

    private static JsonElement SerializeStringConcat(ExprNodeEditor editor, GraphNode node)
    {
        var inputs = editor.GetAllInputConnections(node);
        var parts = new JsonArray();
        foreach (var (inputNode, _, _) in inputs)
        {
            parts.Add(JsonNode.Parse(SerializeNode(editor, inputNode).GetRawText()));
        }
        var obj = new JsonObject
        {
            ["op"] = "concat",
            ["strings"] = parts
        };
        return obj.ToElement();
    }

    #endregion

    #region Conditional Serializer

    private static JsonElement SerializeConditional(ExprNodeEditor editor, GraphNode node)
    {
        var obj = new JsonObject { ["op"] = "if" };

        var condConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 0));
        if (condConn != null)
            obj["condition"] = JsonNode.Parse(SerializeNode(editor, condConn.Value.node).GetRawText());
        else
            obj["condition"] = true;

        var trueConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 1));
        if (trueConn != null)
            obj["true"] = JsonNode.Parse(SerializeNode(editor, trueConn.Value.node).GetRawText());

        var falseConn = editor.GetInputConnection(node, GetInputSlotIndex(editor, node, 2));
        if (falseConn != null)
            obj["false"] = JsonNode.Parse(SerializeNode(editor, falseConn.Value.node).GetRawText());

        return obj.ToElement();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Calculates the actual slot index for an input on a node,
    /// accounting for properties and inline values that occupy rows before the input slots.
    /// </summary>
    private static int GetInputSlotIndex(ExprNodeEditor editor, GraphNode node, int inputIndex)
    {
        var def = editor.GetNodeDefinition(node);
        if (def == null) return inputIndex;

        int offset = def.Properties.Length;
        if (def.InlineValueType != null) offset++;
        return offset + inputIndex;
    }

    #endregion

    #endregion

    #region JSON → Graph

    /// <summary>
    /// Deserializes a JSON expression into graph nodes, returning the root node.
    /// </summary>
    public static GraphNode? DeserializeToGraph(ExprNodeEditor editor, JsonElement element, Vector2 position, string expectedType = "any")
    {
        return DeserializeElement(editor, element, position, expectedType);
    }

    private static GraphNode? DeserializeElement(ExprNodeEditor editor, JsonElement element, Vector2 position, string expectedType)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return DeserializeConstNumber(editor, element.GetSingle(), position);

            case JsonValueKind.True:
                return DeserializeConstBool(editor, true, position);

            case JsonValueKind.False:
                if (expectedType == "bool")
                    return DeserializeConstBool(editor, false, position);
                if (expectedType == "effect")
                    return editor.CreateNodeAtPosition("no_effect", position);
                if (expectedType == "selector")
                    return editor.CreateNodeAtPosition("no_entity", position);
                return DeserializeConstBool(editor, false, position);

            case JsonValueKind.Null:
                if (expectedType == "effect")
                    return editor.CreateNodeAtPosition("no_effect", position);
                if (expectedType == "selector")
                    return editor.CreateNodeAtPosition("no_entity", position);
                if (expectedType == "bool")
                    return DeserializeConstBool(editor, false, position);
                return null;

            case JsonValueKind.String:
                return DeserializeString(editor, element.GetString()!, position, expectedType);

            case JsonValueKind.Object:
                return DeserializeObject(editor, element, position, expectedType);

            default:
                return null;
        }
    }

    private static GraphNode DeserializeConstNumber(ExprNodeEditor editor, float value, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("const_number", position);
        var spin = node.FindChild("inline_value", recursive: true) as SpinBox;
        if (spin != null) spin.Value = value;
        return node;
    }

    private static GraphNode DeserializeConstBool(ExprNodeEditor editor, bool value, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("const_bool", position);
        var cb = node.FindChild("inline_value", recursive: true) as CheckBox;
        if (cb != null) cb.ButtonPressed = value;
        return node;
    }

    private static GraphNode DeserializeString(ExprNodeEditor editor, string value, Vector2 position, string expectedType)
    {
        // Variable reference ($N)
        if (value.StartsWith("$") && int.TryParse(value[1..], out int varIdx))
        {
            string defName = expectedType switch
            {
                "number" => "var_number",
                "bool" => "var_bool",
                "string" => "var_string",
                "selector" => "var_selector",
                _ => "var_number"
            };
            var node = editor.CreateNodeAtPosition(defName, position);
            var spin = node.FindChild("prop_index", recursive: true) as SpinBox;
            if (spin != null) spin.Value = varIdx;
            return node;
        }

        // Selector keywords
        if (expectedType == "selector" || value is "self" or "caller" or "target" or "target_part")
        {
            return value.ToLower() switch
            {
                "self" or "caller" => editor.CreateNodeAtPosition("self_selector", position),
                "target" => editor.CreateNodeAtPosition("target_selector", position),
                "target_part" => editor.CreateNodeAtPosition("target_part_selector", position),
                _ => DeserializeConstStringNode(editor, value, position)
            };
        }

        // Dice notation (contains d, D, -, :, or ,)
        if (expectedType == "number" && value.Length > 2)
        {
            var diceChars = new[] { 'd', 'D', '-', ':', ',' };
            if (value.IndexOfAny(diceChars) >= 0)
            {
                var node = editor.CreateNodeAtPosition("dice", position);
                var le = node.FindChild("prop_notation", recursive: true) as LineEdit;
                if (le != null) le.Text = value;
                return node;
            }
        }

        // Bool string literals
        if (expectedType == "bool")
        {
            if (value == "true") return DeserializeConstBool(editor, true, position);
            if (value == "false") return DeserializeConstBool(editor, false, position);
        }

        return DeserializeConstStringNode(editor, value, position);
    }

    private static GraphNode DeserializeConstStringNode(ExprNodeEditor editor, string value, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("const_string", position);
        var le = node.FindChild("inline_value", recursive: true) as LineEdit;
        if (le != null) le.Text = value;
        return node;
    }

    private static GraphNode? DeserializeObject(ExprNodeEditor editor, JsonElement element, Vector2 position, string expectedType)
    {
        // Effect objects use "effect" key
        if (element.TryGetProperty("effect", out var effectProp))
        {
            string effectName = effectProp.GetString()!.ToLower();
            return DeserializeEffect(editor, element, effectName, position);
        }

        // All other objects use "op" key
        if (!element.TryGetProperty("op", out var opProp))
            return null;

        string op = opProp.GetString()!.ToLower();

        // Conditional (if)
        if (op == "if")
            return DeserializeIfNode(editor, element, position, expectedType);

        // Number operations
        return op switch
        {
            "sum" or "plus" or "add" or "addition" or "+" => DeserializeMathNode(editor, element, "add", position),
            "sub" or "subtract" or "minus" or "subtraction" or "-" => DeserializeMathNode(editor, element, "sub", position),
            "mul" or "multiply" or "times" or "multiplication" or "*" => DeserializeMathNode(editor, element, "mul", position),
            "div" or "divide" or "division" or "/" => DeserializeMathNode(editor, element, "div", position),
            "lerp" => DeserializeLerpNode(editor, element, position),
            "rand" or "random" or "range" when expectedType == "number" => DeserializeRangeNode(editor, element, position),
            "rand" or "random" when expectedType == "bool" => DeserializeRandomChanceNode(editor, element, position),
            "stat" or "creature_stat" or "entity_stat" or "entitystat" or "creaturestat" => DeserializeStatNode(editor, element, position),
            "and" => DeserializeLogicGateNode(editor, element, "and", position),
            "or" => DeserializeLogicGateNode(editor, element, "or", position),
            "not" => DeserializeNotNode(editor, element, position),
            ">" => DeserializeComparisonNode(editor, element, "greater_than", position),
            "<" => DeserializeComparisonNode(editor, element, "less_than", position),
            "=" or "==" => DeserializeComparisonNode(editor, element, "equal", position),
            "!=" => DeserializeComparisonNode(editor, element, "not_equal", position),
            ">=" => DeserializeNotWrappedComparison(editor, element, "less_than", position),
            "<=" => DeserializeNotWrappedComparison(editor, element, "greater_than", position),
            "concat" or "join" => DeserializeStringConcatNode(editor, element, position),
            "part_by_tag" or "bp_by_tag" or "bodypart_by_tag" or "body_part_by_tag"
                => DeserializeBodyPartSelectorNode(editor, element, "body_part_by_tag", "tag", position),
            "part_by_name" or "bp_by_name" or "bodypart_by_name" or "body_part_by_name"
                => DeserializeBodyPartSelectorNode(editor, element, "body_part_by_name", "name", position),
            _ => null
        };
    }

    #region Deserialization Helpers

    private const float NODE_SPACING_X = 280;
    private const float NODE_SPACING_Y = 100;

    private static GraphNode? DeserializeMathNode(ExprNodeEditor editor, JsonElement element, string defName, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition(defName, position);
        if (element.TryGetProperty("numbers", out var numbers) && numbers.ValueKind == JsonValueKind.Array)
        {
            var arr = numbers.EnumerateArray().ToArray();
            // Add extra input slots for variadic inputs beyond the default 2
            for (int i = 2; i < arr.Length; i++)
            {
                var addBtn = node.FindChild("add_input_btn", recursive: true) as Button;
                addBtn?.EmitSignal("pressed");
            }
            for (int i = 0; i < arr.Length; i++)
            {
                var childNode = DeserializeElement(editor, arr[i], position - new Vector2(NODE_SPACING_X, -i * NODE_SPACING_Y), "number");
                if (childNode != null)
                    ConnectOutput(editor, childNode, node, i);
            }
        }
        return node;
    }

    private static GraphNode? DeserializeLerpNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("lerp", position);
        string[] keys = ["min", "max", "t"];
        for (int i = 0; i < keys.Length; i++)
        {
            if (element.TryGetProperty(keys[i], out var val))
            {
                var childNode = DeserializeElement(editor, val, position - new Vector2(NODE_SPACING_X, -i * NODE_SPACING_Y), "number");
                if (childNode != null)
                    ConnectOutput(editor, childNode, node, i);
            }
        }
        return node;
    }

    private static GraphNode? DeserializeRangeNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("range", position);
        string[] keys = ["min", "max"];
        for (int i = 0; i < keys.Length; i++)
        {
            if (element.TryGetProperty(keys[i], out var val))
            {
                var childNode = DeserializeElement(editor, val, position - new Vector2(NODE_SPACING_X, -i * NODE_SPACING_Y), "number");
                if (childNode != null)
                    ConnectOutput(editor, childNode, node, i);
            }
        }
        return node;
    }

    private static GraphNode? DeserializeStatNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("stat", position);
        if (element.TryGetProperty("stat", out var statProp))
        {
            var le = node.FindChild("prop_stat_name", recursive: true) as LineEdit;
            if (le != null) le.Text = statProp.GetString() ?? "";
        }
        if (element.TryGetProperty("entity", out var entityProp))
        {
            var childNode = DeserializeElement(editor, entityProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty("default", out var defaultProp))
        {
            var childNode = DeserializeElement(editor, defaultProp, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    private static GraphNode? DeserializeLogicGateNode(ExprNodeEditor editor, JsonElement element, string defName, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition(defName, position);
        if (element.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
        {
            var arr = conditions.EnumerateArray().ToArray();
            for (int i = 2; i < arr.Length; i++)
            {
                var addBtn = node.FindChild("add_input_btn", recursive: true) as Button;
                addBtn?.EmitSignal("pressed");
            }
            for (int i = 0; i < arr.Length; i++)
            {
                var childNode = DeserializeElement(editor, arr[i], position - new Vector2(NODE_SPACING_X, -i * NODE_SPACING_Y), "bool");
                if (childNode != null)
                    ConnectOutput(editor, childNode, node, i);
            }
        }
        return node;
    }

    private static GraphNode? DeserializeNotNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("not", position);
        if (element.TryGetProperty("condition", out var cond))
        {
            var childNode = DeserializeElement(editor, cond, position - new Vector2(NODE_SPACING_X, 0), "bool");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        return node;
    }

    private static GraphNode? DeserializeComparisonNode(ExprNodeEditor editor, JsonElement element, string defName, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition(defName, position);
        if (element.TryGetProperty("left", out var left))
        {
            var childNode = DeserializeElement(editor, left, position - new Vector2(NODE_SPACING_X, 0), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty("right", out var right))
        {
            var childNode = DeserializeElement(editor, right, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    private static GraphNode? DeserializeNotWrappedComparison(ExprNodeEditor editor, JsonElement element, string innerDefName, Vector2 position)
    {
        // >= is Not(< ) and <= is Not(>)
        var notNode = editor.CreateNodeAtPosition("not", position);
        var innerNode = DeserializeComparisonNode(editor, element, innerDefName, position - new Vector2(NODE_SPACING_X, 0));
        if (innerNode != null)
            ConnectOutput(editor, innerNode, notNode, 0);
        return notNode;
    }

    private static GraphNode? DeserializeRandomChanceNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("random_chance", position);
        if (element.TryGetProperty("probability", out var prob))
        {
            var childNode = DeserializeElement(editor, prob, position - new Vector2(NODE_SPACING_X, 0), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        return node;
    }

    private static GraphNode? DeserializeIfNode(ExprNodeEditor editor, JsonElement element, Vector2 position, string expectedType)
    {
        string defName = expectedType switch
        {
            "number" => "if_number",
            "bool" => "if_bool",
            "string" => "if_string",
            "selector" => "if_selector",
            _ => "if_number"
        };

        var node = editor.CreateNodeAtPosition(defName, position);

        if (element.TryGetProperty("condition", out var cond))
        {
            var condNode = DeserializeElement(editor, cond, position - new Vector2(NODE_SPACING_X, 0), "bool");
            if (condNode != null)
                ConnectOutput(editor, condNode, node, 0);
        }
        if (element.TryGetProperty("true", out var trueVal))
        {
            var trueNode = DeserializeElement(editor, trueVal, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), expectedType);
            if (trueNode != null)
                ConnectOutput(editor, trueNode, node, 1);
        }
        if (element.TryGetProperty("false", out var falseVal))
        {
            var falseNode = DeserializeElement(editor, falseVal, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y * 2), expectedType);
            if (falseNode != null)
                ConnectOutput(editor, falseNode, node, 2);
        }
        return node;
    }

    private static GraphNode? DeserializeEffect(ExprNodeEditor editor, JsonElement element, string effectName, Vector2 position)
    {
        return effectName switch
        {
            "nop" or "null" or "noeffect" or "no_effect" => editor.CreateNodeAtPosition("no_effect", position),
            "composite" => DeserializeCompositeEffect(editor, element, position),
            "set_stat" or "setstat" => DeserializeStatEffectNode(editor, element, "set_stat", position),
            "add_stat" or "addstat" => DeserializeStatEffectNode(editor, element, "add_stat", position),
            "sub_stat" or "substat" or "remove_stat" or "removestat" => DeserializeStatEffectNode(editor, element, "sub_stat", position),
            "add_injury" or "addinjury" or "injury" or "hurt" => DeserializeAddInjuryNode(editor, element, position),
            "heal_injury" or "healinjury" => DeserializeHealInjuryNode(editor, element, position),
            "add_feature" or "addfeature" or "add_feat" or "add_condition" or "addcondition"
                => DeserializeAddFeatureNode(editor, element, position),
            "remove_feature" or "removefeature" or "remove_feat"
                => DeserializeRemoveFeatureNode(editor, element, position),
            _ => null
        };
    }

    private static GraphNode? DeserializeCompositeEffect(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("composite", position);
        if (element.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array)
        {
            var arr = effects.EnumerateArray().ToArray();
            for (int i = 2; i < arr.Length; i++)
            {
                var addBtn = node.FindChild("add_input_btn", recursive: true) as Button;
                addBtn?.EmitSignal("pressed");
            }
            for (int i = 0; i < arr.Length; i++)
            {
                var childNode = DeserializeElement(editor, arr[i], position - new Vector2(NODE_SPACING_X, -i * NODE_SPACING_Y), "effect");
                if (childNode != null)
                    ConnectOutput(editor, childNode, node, i);
            }
        }
        return node;
    }

    private static GraphNode? DeserializeStatEffectNode(ExprNodeEditor editor, JsonElement element, string defName, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition(defName, position);
        if (element.TryGetProperty("stat", out var statProp))
        {
            var le = node.FindChild("prop_stat_name", recursive: true) as LineEdit;
            if (le != null) le.Text = statProp.GetString() ?? "";
        }
        if (element.TryGetProperty("target", out var targetProp))
        {
            var childNode = DeserializeElement(editor, targetProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty("value", out var valueProp))
        {
            var childNode = DeserializeElement(editor, valueProp, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    private static GraphNode? DeserializeAddInjuryNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("add_injury", position);
        if (element.TryGetProperty("type", out var typeProp))
        {
            var le = node.FindChild("prop_injury_type", recursive: true) as LineEdit;
            if (le != null) le.Text = typeProp.GetString() ?? "";
        }
        if (element.TryGetProperty("target", out var targetProp))
        {
            var childNode = DeserializeElement(editor, targetProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty("severity", out var sevProp))
        {
            var childNode = DeserializeElement(editor, sevProp, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    private static GraphNode? DeserializeHealInjuryNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("heal_injury", position);
        if (element.TryGetProperty("type", out var typeProp))
        {
            var le = node.FindChild("prop_injury_type", recursive: true) as LineEdit;
            if (le != null) le.Text = typeProp.GetString() ?? "";
        }
        if (element.TryGetProperty("target", out var targetProp))
        {
            var childNode = DeserializeElement(editor, targetProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty("amount", out var amountProp))
        {
            var childNode = DeserializeElement(editor, amountProp, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    private static GraphNode? DeserializeAddFeatureNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("add_feature", position);
        if (element.TryGetProperty("feature", out var featProp))
        {
            var le = node.FindChild("prop_feature", recursive: true) as LineEdit;
            if (le != null) le.Text = featProp.GetString() ?? "";
        }
        if (element.TryGetProperty("target", out var targetProp))
        {
            var childNode = DeserializeElement(editor, targetProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty("ticks", out var ticksProp))
        {
            var childNode = DeserializeElement(editor, ticksProp, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "number");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    private static GraphNode? DeserializeRemoveFeatureNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("remove_feature", position);
        if (element.TryGetProperty("feature", out var featProp))
        {
            var le = node.FindChild("prop_feature", recursive: true) as LineEdit;
            if (le != null) le.Text = featProp.GetString() ?? "";
        }
        if (element.TryGetProperty("target", out var targetProp))
        {
            var childNode = DeserializeElement(editor, targetProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        return node;
    }

    private static GraphNode? DeserializeStringConcatNode(ExprNodeEditor editor, JsonElement element, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition("string_concat", position);
        if (element.TryGetProperty("strings", out var strings) && strings.ValueKind == JsonValueKind.Array)
        {
            var arr = strings.EnumerateArray().ToArray();
            for (int i = 2; i < arr.Length; i++)
            {
                var addBtn = node.FindChild("add_input_btn", recursive: true) as Button;
                addBtn?.EmitSignal("pressed");
            }
            for (int i = 0; i < arr.Length; i++)
            {
                var childNode = DeserializeElement(editor, arr[i], position - new Vector2(NODE_SPACING_X, -i * NODE_SPACING_Y), "string");
                if (childNode != null)
                    ConnectOutput(editor, childNode, node, i);
            }
        }
        return node;
    }

    private static GraphNode? DeserializeBodyPartSelectorNode(ExprNodeEditor editor, JsonElement element, string defName, string paramName, Vector2 position)
    {
        var node = editor.CreateNodeAtPosition(defName, position);
        if (element.TryGetProperty("target", out var targetProp))
        {
            var childNode = DeserializeElement(editor, targetProp, position - new Vector2(NODE_SPACING_X, 0), "selector");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 0);
        }
        if (element.TryGetProperty(paramName, out var paramProp))
        {
            var childNode = DeserializeElement(editor, paramProp, position - new Vector2(NODE_SPACING_X, NODE_SPACING_Y), "string");
            if (childNode != null)
                ConnectOutput(editor, childNode, node, 1);
        }
        return node;
    }

    /// <summary>
    /// Connects the first output of sourceNode to a target input slot.
    /// The input slot index is adjusted for properties/inline values.
    /// </summary>
    private static void ConnectOutput(ExprNodeEditor editor, GraphNode sourceNode, GraphNode targetNode, int logicalInputIndex)
    {
        var def = editor.GetNodeDefinition(targetNode);
        int physicalSlot = logicalInputIndex;
        if (def != null)
        {
            physicalSlot = def.Properties.Length + (def.InlineValueType != null ? 1 : 0) + logicalInputIndex;
        }
        editor.ConnectNode(sourceNode.Name, 0, targetNode.Name, physicalSlot);
    }

    #endregion

    #endregion
}

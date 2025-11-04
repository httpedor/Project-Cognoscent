using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg;

public static class JsonHelpers
{
    // Utility to parse enum operations with fallback
    public static StatModifierType ParseOp(string? op, StatModifierType fallback)
        => Enum.TryParse<StatModifierType>(op ?? string.Empty, true, out var v) ? v : fallback;

    // Deep merge for JsonObject with array merge by name/id
    public static JsonObject Merge(JsonObject first, JsonObject second)
    {
        JsonObject result = new JsonObject();

        // Copy from first, skipping nulls
        foreach (var kvp in first)
        {
            if (kvp.Value is not null)
                result[kvp.Key] = kvp.Value.DeepClone();
        }

        // Merge from second
        foreach (var kvp in second)
        {
            if (kvp.Value is null)
            {
                result.Remove(kvp.Key); // Remove the key if null
                continue;
            }

            if (result[kvp.Key] is JsonObject obj1 && kvp.Value is JsonObject obj2)
            {
                result[kvp.Key] = Merge(obj1, obj2);
            }
            else if (result[kvp.Key] is JsonArray arr1 && kvp.Value is JsonArray arr2)
            {
                result[kvp.Key] = MergeArrays(arr1, arr2);
            }
            else
            {
                result[kvp.Key] = kvp.Value.DeepClone();
            }
        }

        return result;
    }

    public static JsonArray MergeArrays(JsonArray first, JsonArray second, params string[] idKeys)
    {
        var merged = new JsonArray();
        var map = new Dictionary<string, JsonObject>();

        void AddOrMerge(JsonNode? node)
        {
            if (node is JsonObject obj && idKeys.Length > 0)
            {
                JsonValue? idVal = null;
                foreach (var key in idKeys)
                {
                    if (obj[key] is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                    {
                        idVal = val;
                        break;
                    }
                }
                if (idVal is { } nameVal && nameVal.GetValue<string>() is { } name)
                {
                    if (map.TryGetValue(name, out var existing))
                    {
                        map[name] = Merge(existing, obj);
                    }
                    else
                    {
                        map[name] = (JsonObject)obj.DeepClone();
                    }
                }
                else
                    merged.Add(node.DeepClone());
            }
            else
            {
                if (node is not null)
                    merged.Add(node.DeepClone());
            }
        }

        foreach (var item in first) AddOrMerge(item);
        foreach (var item in second) AddOrMerge(item);

        foreach (var obj in map.Values)
            merged.Add(obj);

        return merged;
    }

    public static float GetFloat(JsonElement json)
    {
        switch (json.ValueKind)
        {
            case JsonValueKind.Number:
                return json.GetSingle();
            case JsonValueKind.String:
                var str = json.GetString();
                if (float.TryParse(str, out float v))
                    return v;
                
                if (str.Contains("d"))
                    return RpgMath.RollDice(str);
                if (str.Contains("-") || str.Contains(":") || str.Contains(","))
                {
                    var parts = str.Split(new char[] { '-', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && float.TryParse(parts[0], out float min) && float.TryParse(parts[1], out float max))
                    {
                        if (min > max)
                        {
                            var tmp = min;
                            min = max;
                            max = tmp;
                        }
                        return RpgMath.RandomFloat(min, max);
                    }
                }

                throw new Exception("Invalid float: " + json);
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.False:
                return 0;
            default:
                throw new Exception("Invalid float: " + json);
        }
    }
    public static int GetInt(JsonElement json)
    {
        return (int)GetFloat(json);
    }
}

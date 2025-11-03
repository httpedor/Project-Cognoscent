using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg;

public static class JsonModel
{
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
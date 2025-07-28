using System.Text.Json.Nodes;

namespace Server.AI;

public class AIFuncParameter(string name, string type, string desc, bool required = true)
{
    public string Name => name;
    public string Type => type;
    public string Description => desc;
    public bool Required => required;

    public JsonObject ToJson()
    {
        var obj = new JsonObject
        {
            ["type"] = type,
            ["description"] = desc,
        };
        return obj;
    }
}

public class AIFunction(string name, string desc, AIFuncParameter[] parameters, Func<JsonNode, string> func)
{
    public string Name => name;
    public string Description => desc;
    public AIFuncParameter[] Parameters => parameters;
    public Func<JsonNode, string> Func => func;

    public JsonNode ToJson()
    {
        var properties = new JsonObject();
        foreach (AIFuncParameter param in parameters)
        {
            properties[param.Name] = param.ToJson();
        }
        var required = new JsonArray();
        foreach (AIFuncParameter param in parameters)
        {
            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["parameters"] = properties.Count > 0 ? new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required
                } : null
            }
        };
    }
}
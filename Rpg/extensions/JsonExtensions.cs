using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg;

public static class JsonExtensions
{
    // Parse JsonElement/nullable JsonElement to JsonNode
    public static JsonNode? ToNode(this JsonElement el) => JsonNode.Parse(el.GetRawText());
    public static JsonNode? ToNodeOrNull(this JsonElement? el) => el.HasValue ? JsonNode.Parse(el.Value.GetRawText()) : null;
    public static JsonObject ToObject(this JsonElement el) => (JsonObject)JsonNode.Parse(el.GetRawText())!;
    public static JsonObject? ToObjectOrNull(this JsonElement? el) => el.HasValue ? (JsonObject?)JsonNode.Parse(el.Value.GetRawText()) : null;

    public static JsonElement ToElement(this JsonNode node)
    {
        var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }
    public static JsonElement? ToElementOrNull(this JsonNode? node)
    {
        if (node == null) return null;
        var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }
}
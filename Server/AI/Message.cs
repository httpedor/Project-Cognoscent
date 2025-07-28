using System.Text.Json.Nodes;

namespace Server.AI;

public enum Role
{
    System,
    User,
    Assistant,
    Function
}

public static class RoleExtensions
{
    public static Role FromString(string role)
    {
        return role switch
        {
            "system" => Role.System,
            "user" => Role.User,
            "assistant" => Role.Assistant,
            "function" => Role.Function,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }
}

public record Message(Role Role, string Content, JsonArray? ToolCalls = null)
{
    public Message(JsonObject obj) : this(RoleExtensions.FromString(obj["role"].AsValue().GetValue<string>()), obj["content"].AsValue().GetValue<string>(), obj["tool_calls"]?.AsArray())
    {
    }
    
    public JsonObject ToJson()
    {
        var obj = new JsonObject
        {
            ["role"] = Role switch
                               {
                                   Role.System => "system",
                                   Role.User => "user",
                                   Role.Assistant => "assistant",
                                   Role.Function => "function",
                                   _ => throw new ArgumentOutOfRangeException(nameof(Role), Role, null)
                               },
            ["content"] = Content,
            ["tool_calls"] = ToolCalls
        };
        return obj;
    }

    public override string ToString()
    {
        return ToJson().ToString();
    }
}
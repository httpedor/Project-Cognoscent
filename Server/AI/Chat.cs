using System.Text;
using System.Text.Json.Nodes;

namespace Server.AI;

public class Chat
{
    public List<Message> Messages { get; } = new();

    public Chat(string systemPrompt)
    {
        Messages.Add(new Message(Role.System, systemPrompt));
    }
    
    public void Clear()
    {
        Messages.Clear();
    }

    private async Task<Message?> SendMessage(Message message, string model = "qwen-turbo", int index = 0)
    {
        if (index > 10)
        {
            Console.WriteLine("Too many iterations on Chat.SendMessage(" + index + "), dumping chat history:");
            foreach (Message msg in Messages)
            {
                Console.WriteLine(msg.ToString());
            }
            return null;
        }
        Messages.Add(message);

        
        var messages = new JsonArray();
        foreach (Message msg in Messages)
        {
            messages.Add(msg.ToJson());
        }
        
        var json = new JsonObject
        {
            ["model"] = model,
            ["input"] = new JsonObject(),
            ["parameters"] = new JsonObject(),
        };
        json["input"]!["messages"] = messages;
        json["parameters"]!["tools"] = AI.SystemTools;
        json["parameters"]!["result_format"] = "message";
        

        var response = await AI.CLIENT.PostAsync(AI.URL, new StringContent(json.ToString(), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return null;
        
        string responseBody = await response.Content.ReadAsStringAsync();
        JsonNode? jsonResponse = JsonNode.Parse(responseBody);
        var assistantMessage = new Message((JsonObject)jsonResponse!["output"]!["choices"]![0]!["message"]!);
        Messages.Add(assistantMessage);

        if (assistantMessage.ToolCalls == null) return assistantMessage;
        
        JsonObject function = assistantMessage.ToolCalls["function"]!.AsObject();
        string functionName = function["name"]!.AsValue().GetValue<string>();
        string functionArgs = function["args"]!.ToString();
        string? ret = AI.CallFunction(functionName, JsonNode.Parse(functionArgs)!.AsObject());
        var retMessage = new Message(Role.Function, ret ?? "");
        var finalMsg = await SendMessage(retMessage, model, index+1);
        return finalMsg;
    }

    public async Task<string> Prompt(string content, Role role = Role.User, string model = "qwen-turbo")
    {
        var message = new Message(role, content);
        Message? response = await SendMessage(message, model);
        
        if (response == null)
        {
            Console.WriteLine("Error getting ai response, chat history:" );
            foreach (Message msg in Messages)
            {
                Console.WriteLine(msg.ToString());
            }
            return "Error getting ai response";
        }

        return response.Content;
    }
}
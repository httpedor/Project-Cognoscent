using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg;

public class ItemModel
{
    public string Name = "Unnamed Item";

    public ItemModel(string id, JsonElement json)
    {
    }
}
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg;

public interface ICustomDataContainer
{
    public byte[]? GetCustomData(string id);
    public void SetCustomData(string id, byte[]? data);
}

public class CustomDataContainerMixin
{
    
}
public static class CustomDataContainerExtensions
{

public static bool HasCustomData(this ICustomDataContainer container, string id)
    {
        return container.GetCustomData(id) != null;
    }

    public static void SetCustomData(this ICustomDataContainer container, string id, byte data)
    {
        container.SetCustomData(id, [data]);
    }
    public static void SetCustomData(this ICustomDataContainer container, string id, int data)
    {
        container.SetCustomData(id, BitConverter.GetBytes(data));
    }
    public static void SetCustomData(this ICustomDataContainer container, string id, uint data)
    {
        container.SetCustomData(id, BitConverter.GetBytes(data));
    }

    public static void SetCustomData(this ICustomDataContainer container, string id, float data)
    {
        container.SetCustomData(id, BitConverter.GetBytes(data));
    }

    public static uint GetCustomDataUInt(this ICustomDataContainer container, string id)
    {
        return BitConverter.ToUInt32(container.GetCustomData(id));
    }

    public static float GetCustomDataFloat(this ICustomDataContainer container, string id)
    {
        return BitConverter.ToSingle(container.GetCustomData(id));
    }
    public static void SetCustomData(this ICustomDataContainer container, string id, string value)
    {
        var stream = new MemoryStream();
        stream.WriteString(value);
        container.SetCustomData(id, stream.GetBuffer());
        stream.Dispose();
    }
    public static string? GetCustomDataString(this ICustomDataContainer container, string id)
    {
        var bytes = container.GetCustomData(id);
        if (bytes == null)
            return null;
        var stream = new MemoryStream(bytes);
        return stream.ReadString();
    }
    public static void RemoveCustomData(this ICustomDataContainer container, string id)
    {
        container.SetCustomData(id, null);
    }

    public static void CustomDataFromJson(this ICustomDataContainer container, JsonObject json)
    {
        foreach (var pair in json)
        {
            switch (pair.Value.GetValueKind())
            {
                case JsonValueKind.Number:
                    container.SetCustomData(pair.Key, pair.Value.GetValue<float>());
                    break;
                case JsonValueKind.String:
                    container.SetCustomData(pair.Key, pair.Value.GetValue<string>());
                    break;
                case JsonValueKind.True:
                    container.SetCustomData(pair.Key, (byte)1);
                    break;
            }
        }
    }
}
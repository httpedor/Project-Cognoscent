using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraitGenerator;

namespace Rpg;

public interface ICustomDataContainer
{
    public byte[]? GetCustomData(string id);
    public void SetCustomData(string id, byte[]? data);
}

public static class CustomDataContainerExtensions
{
    extension (ICustomDataContainer container)
    {
        public void RemoveCustomData(string id)
        {
            container.SetCustomData(id, null);
        }
        public void SetCustomData(string id, byte data)
        {
            container.SetCustomData(id, [data]);
        }

        public void SetCustomData(string id, int data)
        {
            container.SetCustomData(id, BitConverter.GetBytes(data));
        }

        public void SetCustomData(string id, uint data)
        {
            container.SetCustomData(id, BitConverter.GetBytes(data));
        }

        public void SetCustomData(string id, float data)
        {
            container.SetCustomData(id, BitConverter.GetBytes(data));
        }

        public void SetCustomData(string id, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            container.SetCustomData(id, bytes);
        }

        public void SetCustomData(string id, JsonObject json)
        {
            var bytes = Encoding.UTF8.GetBytes(json.ToJsonString());
            container.SetCustomData(id, bytes);
        }

        public uint GetCustomDataUInt(string id)
        {
            return BitConverter.ToUInt32(container.GetCustomData(id));
        }

        public float GetCustomDataFloat(string id)
        {
            return BitConverter.ToSingle(container.GetCustomData(id));
        }

        public string? GetCustomDataString(string id)
        {
            var bytes = container.GetCustomData(id);
            if (bytes == null)
                return null;
            return Encoding.UTF8.GetString(bytes);
        }

        public JsonObject? GetCustomDataJson(string id)
        {
            var str = container.GetCustomDataString(id);
            if (str == null)
                return null;
            return JsonNode.Parse(str) as JsonObject;
        }

        public void LoadCustomDataFromJson(JsonObject json)
        {
            foreach (var pair in json)
            {
                var node = pair.Value;
                if (node is JsonValue jv)
                {
                    if (jv.TryGetValue<float>(out var f))
                    {
                        container.SetCustomData(pair.Key, f);
                    }
                    else if (jv.TryGetValue<string>(out var s))
                    {
                        container.SetCustomData(pair.Key, s);
                    }
                    else if (jv.TryGetValue<bool>(out var b) && b)
                    {
                        container.SetCustomData(pair.Key, (byte)1);
                    }
                }
                else if (node is JsonObject jo)
                {
                    container.SetCustomData(pair.Key, jo);
                }
            }
        }
    }
}

[Mixin(typeof(ICustomDataContainer))]
abstract class CustomDataContainerMixin : ICustomDataContainer
{
    protected readonly Dictionary<string, byte[]> customData = new();
    public byte[]? GetCustomData(string id)
    {
        customData.TryGetValue(id, out var data);
        return data;
    }

    public void SetCustomData(string id, byte[]? data)
    {
        if (data == null)
            customData.Remove(id);
        else
            customData[id] = data;
    }

    public bool HasCustomData(string id)
    {
        return customData.ContainsKey(id);
    }

    protected void CustomDataToBytes(Stream stream)
    {
        stream.WriteByte((byte)customData.Count);
        foreach (var pair in customData)
        {
            stream.WriteString(pair.Key);
            stream.WriteUInt32((uint)pair.Value.Length);
            stream.Write(pair.Value);
        }
    }
    protected void CustomDataFromBytes(Stream stream)
    {
        byte count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string name = stream.ReadString();
            byte[] data = new byte[stream.ReadUInt32()];
            stream.ReadExactly(data);
            customData[name] = data;
        }
    }
}

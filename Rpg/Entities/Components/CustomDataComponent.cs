namespace Rpg.Entities;
public partial class CustomDataComponent : Component
{
    protected readonly Dictionary<string, byte[]> customData = new();
    public CustomDataComponent() : base()
    {

    }

    public CustomDataComponent(Stream stream) : base(stream)
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
    public byte[]? Get(string id)
    {
        customData.TryGetValue(id, out var data);
        return data;
    }
    public int? GetInt(string id)
    {
        byte[]? data = Get(id);
        if (data == null || data.Length != 4)
            return null;
        return BitConverter.ToInt32(data);
    }
    public uint? GetUInt(string id)
    {
        byte[]? data = Get(id);
        if (data == null || data.Length != 4)
            return null;
        return BitConverter.ToUInt32(data);
    }
    public void SetInt(string id, int? value)
    {
        Set(id, value == null ? null : BitConverter.GetBytes(value.Value));
    }
    public void SetUInt(string id, uint? value)
    {
        Set(id, value == null ? null : BitConverter.GetBytes(value.Value));
    }
    public float? GetFloat(string id)
    {
        byte[]? data = Get(id);
        if (data == null || data.Length != 4)
            return null;
        return BitConverter.ToSingle(data);
    }
    public void SetFloat(string id, float? value)
    {
        Set(id, value == null ? null : BitConverter.GetBytes(value.Value));
    }
    public string? GetString(string id)
    {
        byte[]? data = Get(id);
        if (data == null)
            return null;
        return System.Text.Encoding.UTF8.GetString(data);
    }
    public void SetString(string id, string? value)
    {
        Set(id, value == null ? null : System.Text.Encoding.UTF8.GetBytes(value));
    }

    public void Set(string id, byte[]? data)
    {
        if (data == null)
            customData.Remove(id);
        else
            customData[id] = data;
    }

    public byte[]? Remove(string id)
    {
        if (customData.TryGetValue(id, out var data))
        {
            customData.Remove(id);
            return data;
        }
        return null;
    }
    public bool Has(string id)
    {
        return customData.ContainsKey(id);
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteByte((byte)customData.Count);
        foreach (var pair in customData)
        {
            stream.WriteString(pair.Key);
            stream.WriteUInt32((uint)pair.Value.Length);
            stream.Write(pair.Value);
        }
    }
}
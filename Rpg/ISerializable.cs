using System.Reflection;
using System.Runtime.Serialization;

namespace Rpg;

public interface ISerializable
{
    private static Dictionary<ushort, Type> types = new();
    private static Dictionary<Type, ushort> ids = new();

    static ISerializable()
    {
        ushort i = 0;
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsSubclassOf(typeof(ISerializable))) continue;

            types.Add(i, type);
            ids.Add(type, i);
            i++;
        }
    }
    public void ToBytes(Stream stream);

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        ToBytes(stream);
        return stream.ToArray();
    }

    public static void ToBytes(ISerializable serializable, Stream stream)
    {
        stream.WriteUInt16(ids[serializable.GetType()]);
        serializable.ToBytes(stream);
    }
    public static ISerializable FromBytes(Stream stream)
    {
        ushort id = stream.ReadUInt16();
        Type t = types[id];
        
        if (t.GetConstructor(new[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get ISerializable constructor: " + t);
        return (ISerializable)Activator.CreateInstance(t, stream)!;
    }

    public static T FromBytes<T>(Stream stream) where T : ISerializable
    {
        var ret = FromBytes(stream);
        if (ret is not T t)
            throw new Exception("FromBytes return value is not " + typeof(T).Name);
        
        return t;
    }
}

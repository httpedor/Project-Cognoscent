namespace Rpg.Inventory;

public class ItemProperty : ISerializable
{
    public static ItemProperty FromBytes(Stream stream)
    {
        var path = stream.ReadString();
        Type? type = Type.GetType(path);

        if (type == null)
            throw new Exception("Failed to get ItemProperty: " + path);
        if (type.GetConstructor(new Type[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get ItemProperty constructor: " + path);
        return (ItemProperty)Activator.CreateInstance(type, stream);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
    }
}

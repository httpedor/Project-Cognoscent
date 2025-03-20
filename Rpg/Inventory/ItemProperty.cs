namespace Rpg.Inventory;

public abstract class ItemProperty : ISerializable
{
    private static Dictionary<string, Type> propsById = new();
    private static Dictionary<Type, string> IdsByProp = new();

    static ItemProperty()
    {
        register("equipment", typeof(EquipmentProperty));
    }
    private static void register(string id, Type type)
    {
        propsById[id] = type;
        IdsByProp[type] = id;
    }

    public readonly Item Item;
    public ItemProperty(Item item)
    {
        Item = item;
    }

    protected ItemProperty(Stream stream)
    {

    }

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

    public static Type GetType(string id)
    {
        return propsById[id];
    }
    public static string GetId(Type type)
    {
        return IdsByProp[type];
    }
    public static string GetId<T>()
    {
        return GetId(typeof(T));
    }
}

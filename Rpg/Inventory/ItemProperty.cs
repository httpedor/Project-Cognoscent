using System.Text.Json.Nodes;

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
        string id = stream.ReadString();
        Type? type = propsById[id];

        if (type == null)
            throw new Exception("Failed to get ItemProperty: " + id);
        if (type.GetConstructor(new[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get ItemProperty constructor: " + id);
        return (ItemProperty)Activator.CreateInstance(type, stream);
    }

    public static ItemProperty FromJson(JsonObject json)
    {
        string id = json["id"]!.GetValue<string>();
        Type? type = propsById[id];
        if (type == null)
            throw new Exception("Failed to get ItemProperty: " + id);
        if (type.GetConstructor(new[] { typeof(JsonObject) }) == null)
            throw new Exception("Failed to get ItemProperty JSON constructor: " + id);
        return (ItemProperty)Activator.CreateInstance(type, json);
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteString(GetId(GetType()));
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

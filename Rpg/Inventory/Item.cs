using System.Numerics;
using System.Text.Json.Nodes;
using Rpg;

namespace Rpg.Inventory;
public struct ItemRef : ISerializable
{
    public Item? Item => BoardRef.Board?.GetItemById(Id);
    public int Id;
    public BoardRef BoardRef;
    public ItemRef(Item item)
    {
        if (item.Holder == null)
            throw new Exception("Cannot create ItemRef from item with null holder");
        if (item.Holder.Board == null)
            throw new Exception("Cannot create ItemRef from item with null board");
        Id = item.Id;
        BoardRef = new BoardRef(item.Holder.Board);
    }
    public ItemRef(Stream stream)
    {
        Id = stream.ReadInt32();
        BoardRef = new BoardRef(stream);
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        BoardRef.ToBytes(stream);
    }
}
public class Item : ISerializable, ISkillSource
{
    public string Icon;
    public IItemHolder? Holder;
    public readonly int Id;
    public string Name;
    public string Description;

    public List<Skill> Skills = new();
    public Dictionary<string, List<StatModifier>> StatModifiers = new();
    public List<Feature> Features = new();
    private readonly Dictionary<string, ItemProperty> properties = new();
    private readonly Dictionary<string, Stat> stats = new();

    string ISkillSource.Name => Name;

    IEnumerable<Skill> ISkillSource.Skills => Skills;

    public Item(string icon, string name, string description)
    {
        Icon = icon;
        Name = name;
        Description = description;
        Id = new Random().Next();
    }

    public Item(Stream stream)
    {
        Id = stream.ReadInt32();
        Icon = stream.ReadLongString();
        Name = stream.ReadString();
        Description = stream.ReadLongString();

        int len = stream.ReadByte();
        properties = new Dictionary<string, ItemProperty>();
        for (int i = 0; i < len; i++)
        {
            ItemProperty prop = ItemProperty.FromBytes(stream);
            properties[ItemProperty.GetId(prop.GetType())] = prop;
        }

        len = stream.ReadByte();
        for (int i = 0; i < len; i++)
        {
            string stat = stream.ReadString();
            StatModifiers[stat] = new List<StatModifier>();
            int modsLen = stream.ReadByte();
            for (int j = 0; j < modsLen; j++)
                StatModifiers[stat].Add(new StatModifier(stream));
        }

        len = stream.ReadByte();
        for (int i = 0; i < len; i++)
        {
            Feature feature = Feature.FromBytes(stream);
            Features.Add(feature);
        }
    }

    public Item(string name, JsonObject json)
    {
        Id = new Random().Next();
        Name = name;
        Icon = json["icon"]!.GetValue<string>();
        Description = json["description"]!.GetValue<string>();

        properties = new Dictionary<string, ItemProperty>();
        if (json.TryGetPropertyValue("properties", out var propsJson))
        {
            foreach ((string key, var value) in propsJson.AsObject())
            {
                var prop = ItemProperty.FromJson((JsonObject)value);
                properties[key] = prop;
            }
        }

        StatModifiers = new Dictionary<string, List<StatModifier>>();
        if (json.TryGetPropertyValue("statModifiers", out var statsJson))
        {
            int i = 0;
            foreach (var modJson in statsJson.AsArray())
            {
                string stat = modJson["stat"]!.GetValue<string>();
                if (!StatModifiers.ContainsKey(stat))
                    StatModifiers[stat] = [];
                StatModifiers[stat].Add(new StatModifier(modJson.AsObject(), $"{Id}-{stat}{i}"));
                i++;
            }
        }

        Features = new List<Feature>();
        if (json.TryGetPropertyValue("features", out var featsJson))
        {
            foreach (var featJson in featsJson.AsArray())
            {
                var feat = Compendium.GetEntry<Feature>(featJson!.GetValue<string>());
                if (feat == null)
                {
                    Console.WriteLine($"WARNING: Feat {featJson} does not exist!");
                    continue;
                }
                Features.Add(feat);
            }
        }
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        stream.WriteLongString(Icon);
        stream.WriteString(Name);
        stream.WriteLongString(Description);

        stream.WriteByte((byte)properties.Count);
        foreach (KeyValuePair<string, ItemProperty> prop in properties)
            prop.Value.ToBytes(stream);
        
        stream.WriteByte((byte)StatModifiers.Count);
        foreach (KeyValuePair<string, List<StatModifier>> entry in StatModifiers)
        {
            stream.WriteString(entry.Key);
            stream.WriteByte((byte)entry.Value.Count);
            foreach (StatModifier mod in entry.Value)
            {
                mod.ToBytes(stream);
            }
        }
        
        stream.WriteByte((byte)Features.Count);
        foreach (Feature feature in Features)
        {
            feature.ToBytes(stream);
        }
    }

    public T? GetProperty<T>() where T : ItemProperty
    {
        string propId = ItemProperty.GetId<T>();
        if (properties.TryGetValue(propId, out ItemProperty? property))
            return (T?)property;
        return null;
    }

    public bool HasProperty<T>() where T : ItemProperty
    {
        return properties.ContainsKey(ItemProperty.GetId<T>());
    }
    public bool HasProperty(Type t)
    {
        return properties.ContainsKey(ItemProperty.GetId(t));
    }
}

public static class EquipmentSlot
{
    public const string Head = "head";
    public const string Back = "back";
    public const string Chest = "chest";
    public const string Ear = "ear";
    public const string Foot = "foot";
    public const string Shoulder = "shoulder";
    public const string Arm = "arm";
    public const string Hand = "hand";
    public const string Leg = "leg";
    public const string Neck = "neck";
    public const string Finger = "finger";
    public const string Waist = "waist";
    public const string Wrist = "wrist";
    public const string Eye = "eye";
    public const string Hold = "hold";
}
using System.Numerics;
using Rpg.Entities;

namespace Rpg.Inventory;
public class Item : ISerializable, ISkillSource
{
    public string Icon;
    public IInventoryHolder? Holder;
    public readonly int Id;
    public string Name;
    public string Description;

    public List<Skill> Skills = new();
    public Dictionary<string, StatModifier> StatModifiers = new();
    private Dictionary<string, ItemProperty> Properties = new();

    String ISkillSource.Name => Name;

    IEnumerable<Skill> ISkillSource.Skills => Skills;

    public Item(string icon, string name, string description)
    {
        Icon = icon;
        Name = name;
        Description = description;
    }

    public Item(Stream stream)
    {
        Id = stream.ReadInt32();
        Icon = stream.ReadLongString();
        Name = stream.ReadString();
        Description = stream.ReadLongString();

        var len = stream.ReadByte();
        Properties = new();
        for (int i = 0; i < len; i++)
        {
            var prop = ItemProperty.FromBytes(stream);
            Properties[ItemProperty.GetId(prop.GetType())] = prop;
        }
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        stream.WriteLongString(Icon);
        stream.WriteString(Name);
        stream.WriteLongString(Description);

        stream.WriteByte((Byte)Properties.Count);
        foreach (var prop in Properties)
            prop.Value.ToBytes(stream);
    }

    public T? GetProperty<T>() where T : ItemProperty
    {
        return (T?)Properties[ItemProperty.GetId<T>()];
    }

    public bool HasProperty<T>() where T : ItemProperty
    {
        return Properties.ContainsKey(ItemProperty.GetId<T>());
    }
    public bool HasProperty(Type t)
    {
        return Properties.ContainsKey(ItemProperty.GetId(t));
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
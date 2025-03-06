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
    public Vector2 Size;
    public Dictionary<string, StatModifier[]> EquippedModifiers;
    public List<string> Slots;
    public List<Skill> Skills;
    public List<ItemProperty> Properties;

    String ISkillSource.Name => Name;

    Creature? ISkillSource.Creature {
        get {
            if (Holder is Creature c)
                return c;
            return null;
        }
    }
    IEnumerable<Skill> ISkillSource.Skills => Skills;

    public Item(string icon, string name, string description)
    {
        Icon = icon;
        Name = name;
        Description = description;
        Size = new Vector2(1, 1);
        EquippedModifiers = new Dictionary<string, StatModifier[]>();
    }

    public Item(Stream stream)
    {
        Id = stream.ReadInt32();
        Icon = stream.ReadLongString();
        Name = stream.ReadString();
        Description = stream.ReadLongString();
        Size = stream.ReadVec2();
        byte count = (Byte)stream.ReadByte();
        EquippedModifiers = new Dictionary<string, StatModifier[]>();
        for (int i = 0; i < count; i++)
        {
            string key = stream.ReadString();
            byte modCount = (Byte)stream.ReadByte();
            EquippedModifiers[key] = new StatModifier[modCount];
            for (int j = 0; j < modCount; j++)
            {
                StatModifier mod = new StatModifier(stream);
                EquippedModifiers[key][j] = mod;
            }
        }
        Slots = new List<string>();

        count = (Byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
            Slots.Add(stream.ReadString());
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        stream.WriteLongString(Icon);
        stream.WriteString(Name);
        stream.WriteLongString(Description);
        stream.WriteVec2(Size);
        stream.WriteByte((byte)EquippedModifiers.Count);
        foreach (var modArr in EquippedModifiers.Values)
        {
            stream.WriteByte((Byte)modArr.Length);
            foreach (var mod in modArr)
                mod.ToBytes(stream);
        }

        stream.WriteByte((byte)Slots.Count);
        foreach (var slot in Slots)
            stream.WriteString(slot);
    }

    public static Item FromBytes(Stream stream)
    {
        var path = stream.ReadString();
        Type? type = Type.GetType(path);

        if (type == null)
            throw new Exception("Failed to get item type: " + path);
        
        if (type.GetConstructor(new Type[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get item constructor: " + path);
        
        return (Item)Activator.CreateInstance(type, stream);
    }

    float ISkillSource.GetStat(string id, float defaultValue)
    {
        if (EquippedModifiers.ContainsKey(id))
        {
            float sum = 0;
            foreach (var mod in EquippedModifiers[id])
            {
                if (mod.Type == StatModifierType.Flat)
                    sum += mod.Value;
            }
            return sum;
        }
        else
            return defaultValue;
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
using System.Text.Json.Nodes;
using Rpg.Entities.Interfaces;
using Rpg.Features;
using Rpg.Skills;

namespace Rpg.Entities.Components.Inventory;

public class ItemEvent(Item item, Component component) : ComponentEvent(component)
{
    public Item Item = item;
}
public class ItemEquippedEvent(Item item, Component component) : ItemEvent(item, component)
{
}
public class ItemUnequippedEvent(Item item, Component component) : ItemEvent(item, component)
{
}
public class ItemHeldEvent(Item item, Component component) : ItemEvent(item, component)
{
}
public class ItemUnheldEvent(Item item, Component component) : ItemEvent(item, component)
{
}
public partial class Item : Component
{
    public string Icon;
    public IItemHolder? Holder;
    public string Name => Entity.Name;
    public string Description;

    [RequiredComponent(typeof(StatsContainer))]
    public StatsContainer stats;

    public List<Skill> ProvidedSkills = new();
    public List<Feature> ProvidedFeatures = new();
    public Dictionary<string, List<StatModifier>> StatModifiers = new();

    public Item(string icon, string name, string description) : base()
    {
        Icon = icon;
        Description = description;
    }

    public Item(Stream stream) : base(stream)
    {
        Icon = stream.ReadLongString();
        Description = stream.ReadLongString();

        int len = stream.ReadByte();
        for (int i = 0; i < len; i++)
        {
            string stat = stream.ReadString();
            StatModifiers[stat] = new List<StatModifier>();
            int modsLen = stream.ReadByte();
            for (int j = 0; j < modsLen; j++)
                StatModifiers[stat].Add(new StatModifier(stream));
        }
        
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteLongString(Icon);
        stream.WriteLongString(Description);

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
    }

    public T? GetProperty<T>() where T : ItemProperty
    {
        return Entity.GetComponent<T>();
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
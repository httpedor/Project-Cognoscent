
using System.Text.Json.Nodes;
using Rpg;
using Rpg.Entities;
using Rpg.Inventory;

public partial class ItemComponent : Component, ISkillSource
{
    public string Icon;
    public Entity? Holder;
    public string Name;
    public string Description;

    [RequiredComponent(typeof(StatsComponent))]
    public StatsComponent stats;

    public List<Skill> Skills = new();
    public Dictionary<string, List<StatModifier>> StatModifiers = new();

    string ISkillSource.Name => Name;

    IEnumerable<Skill> ISkillSource.Skills => Skills;

    public ItemComponent(string icon, string name, string description) : base()
    {
        Icon = icon;
        Name = name;
        Description = description;
    }

    public ItemComponent(Stream stream) : base(stream)
    {
        Icon = stream.ReadLongString();
        Name = stream.ReadString();
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
        stream.WriteString(Name);
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
}
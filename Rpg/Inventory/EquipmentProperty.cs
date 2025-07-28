
using Rpg;

namespace Rpg.Inventory;

public class EquipmentProperty : ItemProperty, ISkillSource
{
    /// <summary>
    /// The <see cref="EquipmentSlot"/> this equipment is equipped in.
    /// </summary>
    public string Slot;
    /// <summary>
    /// The body parts this equipment covers.
    /// </summary>
    public List<string> Coverage;
    /// <summary>
    /// If the damage received(post modifiers) to surpasses this, the damage modifiers aren't applied.
    /// </summary>
    public float ArmorValue;
    /// <summary>
    /// Stat modifiers applied to the creature wearing this.
    /// [statId -> modifiers]
    /// </summary>
    public Dictionary<string, List<StatModifier>> StatModifiers = new();
    /// <summary>
    /// StatModifiers applied when a certain DamageType is applied in bodyparts covered by this equipment.
    /// </summary>
    public Dictionary<DamageType, List<StatModifier>> DamageModifiers = new();
    /// <summary>
    /// Which damages turn into which when parts covered by this equipment are hit.
    /// </summary>
    public Dictionary<DamageType, DamageType> DamageConversions = new();
    public List<Skill> Skills = new();
    public List<Feature> Features = new();
    public BodyPart? EquippedPart;
    public EquipmentProperty(Item item, string equipmentSlot, params string[] coverage): base(item)
    {
        Slot = equipmentSlot;
        Coverage = coverage.ToList();
        EquippedPart = null;
    }

    protected EquipmentProperty(Stream stream) : base(stream)
    {
        Slot = stream.ReadString();
        int count = stream.ReadByte();
        Coverage = new List<string>(count);
        for (int i = 0; i < count; i++)
            Coverage.Add(stream.ReadString());
        ArmorValue = stream.ReadFloat();
        
        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string statId = stream.ReadString();
            int modCount = stream.ReadByte();
            for (int j = 0; j < modCount; j++)
            {
                StatModifier modifier = new(stream);
                if (!StatModifiers.ContainsKey(statId))
                    StatModifiers[statId] = new List<StatModifier>();
                StatModifiers[statId].Add(modifier);
            }
        }
        
        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            DamageType type = DamageType.FromBytes(stream);
            int modCount = stream.ReadByte();
            for (int j = 0; j < modCount; j++)
            {
                StatModifier modifier = new(stream);
                if (!DamageModifiers.ContainsKey(type))
                    DamageModifiers[type] = new List<StatModifier>();
                DamageModifiers[type].Add(modifier);
            }
        }
        
        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            DamageType type1 = DamageType.FromBytes(stream);
            DamageType type2 = DamageType.FromBytes(stream);
            DamageConversions[type1] = type2;
        }
        
        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            Skill skill = Skill.FromBytes(stream);
            Skills.Add(skill);
        }
        
        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            Feature feature = Feature.FromBytes(stream);
            Features.Add(feature);
        }
    }

    public virtual void OnEquip(BodyPart equippedPart)
    {
        EquippedPart = equippedPart;
        Item.Holder = equippedPart;

        if (equippedPart.Owner == null)
            return;
        
        foreach (var entry in StatModifiers)
        {
            var stat = equippedPart.Owner.GetStat(entry.Key);
            if (stat == null)
                continue;
            foreach (StatModifier mod in entry.Value)
                stat.SetModifier(mod);
        }

        foreach (Feature feat in Features)
        {
            equippedPart.Owner.AddFeature(feat);
        }
    }
    public virtual void OnUnequip()
    {
        if (EquippedPart?.Owner == null)
            return;
        
        foreach (var entry in StatModifiers)
        {
            var stat = EquippedPart.Owner.GetStat(entry.Key);
            if (stat == null)
                continue;
            foreach (StatModifier mod in entry.Value)
                stat.RemoveModifier(mod);
        }

        foreach (Feature feat in Features)
        {
            EquippedPart.Owner.RemoveFeature(feat);
        }
        
        EquippedPart = null;
        Item.Holder = null;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        
        stream.WriteString(Slot);
        stream.WriteByte((byte)Coverage.Count);
        foreach (string coverage in Coverage)
            stream.WriteString(coverage);
        stream.WriteFloat(ArmorValue);
        
        stream.WriteByte((byte)StatModifiers.Count);
        foreach (KeyValuePair<string, List<StatModifier>> entry in StatModifiers)
        {
            stream.WriteString(entry.Key);
            stream.WriteByte((byte)entry.Value.Count);
            foreach (StatModifier mod in entry.Value)
                mod.ToBytes(stream);
        }
        
        stream.WriteByte((byte)DamageModifiers.Count);
        foreach (var entry in DamageModifiers)
        {
            entry.Key.ToBytes(stream);
            stream.WriteByte((byte)entry.Value.Count);
            foreach (StatModifier mod in entry.Value)
                mod.ToBytes(stream);
        }
        stream.WriteByte((byte)DamageConversions.Count);
        foreach (var entry in DamageConversions)
        {
            entry.Key.ToBytes(stream);
            entry.Value.ToBytes(stream);
        }
        
        stream.WriteByte((byte)Skills.Count);
        foreach (Skill skill in Skills)
        {
            skill.ToBytes(stream);
        }
        
        stream.WriteByte((byte)Features.Count);
        foreach (Feature feature in Features)
        {
            feature.ToBytes(stream);
        }
    }

    public string Name => Item.Name;
    IEnumerable<Skill> ISkillSource.Skills => Skills;

}

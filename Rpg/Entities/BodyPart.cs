

using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Rpg;
using Rpg.Inventory;

namespace Rpg.Entities;

public enum BodyType
{
    Humanoid
}

public class Body : ISerializable
{
    public event Action<BodyPart, Injury>? OnInjuryAdded;
    public event Action<BodyPart, Injury>? OnInjuryRemoved;
    private Dictionary<string, HashSet<BodyPart>> equipmentSlots = new();
    private Dictionary<BodyPart, HashSet<EquipmentProperty>> partsCovered = new();
    private Dictionary<string, HashSet<BodyPart>> partsByName = new();
    public IEnumerable<BodyPart> PartsWithEquipSlots => equipmentSlots.Values.SelectMany(x => x);
    public readonly Creature Owner;
    public readonly BodyType Type;
    private BodyPart root;
    public BodyPart Root
    {
        get => root;
        set
        {
            root = value;
            foreach (BodyPart child in Parts)
                OnPartAdded(child);
        }
    }

    public IEnumerable<BodyPart> Parts
    {
        get
        {
            List<BodyPart> parts = root.AllChildren;
            parts.Add(root);
            return parts;
        }
    }

    public BodyPart? Brain
    {
        private set;
        get;
    }
    
    private Body(Creature owner, BodyType type)
    {
        Owner = owner;
        Type = type;
        Brain = null;
    }

    public Body(Stream stream, Creature owner)
    {
        Owner = owner;
        Type = (BodyType)stream.ReadByte();
        Root = new BodyPart(stream, this);
    }

    public void OnPartAdded(BodyPart part)
    {
        if (part.Name.Equals(BodyPart.Parts.BRAIN))
            Brain = part;

        foreach (var slot in part.EquipmentSlots)
        {
            if (!equipmentSlots.ContainsKey(slot))
                equipmentSlots[slot] = new HashSet<BodyPart>();
            equipmentSlots[slot].Add(part);
        }
        if (!partsByName.ContainsKey(part.Name))
            partsByName[part.Name] = new HashSet<BodyPart>();
        partsByName[part.Name].Add(part);
        partsCovered[part] = new();
        part.UpdateStatModifiers();
    }


    public void OnPartRemoved(BodyPart part)
    {
        if (part.Name.Equals(BodyPart.Parts.BRAIN))
            Brain = null;
        
        foreach (var slot in part.EquipmentSlots)
            equipmentSlots[slot].Remove(part);
        partsCovered.Remove(part);
        part.RemoveStatModifiers();
    }

    public void OnPartDie(BodyPart part)
    {
        foreach (var child in part.Children)
        {
            part.UpdateStatModifiers();
            OnPartDie(child);
        }
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteByte((Byte)Type);
        Root.ToBytes(stream);
    }

    public BodyPart? GetBodyPart(string path)
    {
        return Root.GetChildByPath(path);
    }

    public IEnumerable<BodyPart> GetPartsThatCanEquip(string slot)
    {
        if (equipmentSlots.ContainsKey(slot))
            return equipmentSlots[slot];
        return Array.Empty<BodyPart>();
    }

    public static Body NewHumanoidBody(Creature owner)
    {
        Body ret = new Body(owner, BodyType.Humanoid);
        ret.Root = BodyPart.NewHumanoidBody(ret);
        return ret;
    }

    public void _invokeInjuryEvent(BodyPart bp, Injury inj, bool added)
    {
        if (added)
            OnInjuryAdded?.Invoke(bp, inj);
        else
            OnInjuryRemoved?.Invoke(bp, inj);
    }
}

public class BodyPart : ISerializable, ISkillSource, IInventoryHolder
{
    public static class Flag
    {
        public static byte None => 0x0;
        public static byte Hard => 0x1;
        public static byte Soft => 0x2;
        public static byte Internal => 0x4;
        public static byte Overlaps => 0x8;
    }
    public struct BodyPartStat : ISerializable
    {
        public float atFull;
        public float atZero;
        public StatModifierType op;
        public bool standloneHealthOnly;

        public BodyPartStat(float atFull, float atZero, StatModifierType op, bool sho)
        {
            this.atFull = atFull;
            this.atZero = atZero;
            this.op = op;
            standloneHealthOnly = sho;
        }

        public BodyPartStat(Stream stream)
        {
            atFull = stream.ReadFloat();
            atZero = stream.ReadFloat();
            op = (StatModifierType)stream.ReadByte();
            standloneHealthOnly = stream.ReadByte() == 1;
        }

        public void ToBytes(Stream stream)
        {
            stream.WriteFloat(atFull);
            stream.WriteFloat(atZero);
            stream.WriteByte((Byte)op);
            stream.WriteByte((Byte)(standloneHealthOnly ? 1 : 0));
        }

        public StatModifier CalculateFor(BodyPart target, string? name = null)
        {
            if (name == null)
                name = target.Path + "_mod";
            float hpPercentage;
            if (standloneHealthOnly)
                hpPercentage = target.HealthStandalone/target.MaxHealth;
            else
                hpPercentage = target.Health/target.MaxHealth;

            return new StatModifier(name, RpgMath.Lerp(atZero, atFull, hpPercentage), op);
        }
    }
    public static byte[] Flags = new byte[] { Flag.Hard, Flag.Soft, Flag.Internal};
    
    public event System.Action<BodyPart>? OnChildAdded;
    public event System.Action<BodyPart>? OnChildRemoved;
    public event System.Action<Injury>? OnInjuryAdded;
    public event System.Action<Injury>? OnInjuryRemoved;

    private string name;
    public string Name => name;
    public Creature Owner => BodyInfo.Owner;

    //This should only be used if the parent is alive
    public float HealthStandalone
    {
        get
        {
            float sum = MaxHealth;
            foreach (var injury in injuries)
            {
                if (injury.Type.Instakill)
                    return 0;
                sum -= injury.Severity;
            }
            return sum;
        }
    }
    public float Health
    {
        get
        {
            if (Parent != null && Parent.Health <= 0)
                return 0;

            return HealthStandalone;
        }
    }
    public bool IsAlive
    {
        get
        {
            return Health > 0;
        }
    }
    public float MaxHealth;
    private float surfaceArea = 0;
    public IList<BodyPart> Children;
    public List<BodyPart> AllChildren
    {
        get
        {
            List<BodyPart> children = new List<BodyPart>(Children);
            foreach (BodyPart child in Children)
            {
                children.AddRange(child.AllChildren);
            }
            return children;
        }
    }
    public Skill[] Skills;
    public IEnumerable<BodyPart> InternalOrgans
    {
        get
        {
            foreach (BodyPart child in Children)
            {
                if (child.IsInternal)
                    yield return child;
            }
        }
    }
    public IEnumerable<BodyPart> OverlappingParts
    {
        get
        {
            foreach (BodyPart child in Children)
            {
                if (child.OverlapsParent)
                    yield return child;
            }
        }
    }
    private BodyPart? parent = null;
    public BodyPart? Parent => parent;
    public string Path => parent == null ? name : parent.Path + "/" + name;
    public BodyPart Root => parent == null ? this : parent.Root;
    public bool IsRoot => parent == null;
    private byte flags = 0;
    private List<Injury> injuries = new List<Injury>();
    public ImmutableArray<Injury> Injuries => injuries.ToImmutableArray();

    private List<String> equipmentSlots;
    public IEnumerable<String> EquipmentSlots => equipmentSlots;

    private Body bodyInfo;
    public Body BodyInfo => bodyInfo;


    /// <summary>
    /// What stats are provided by this body part. Dynamically updated on HP(100% to 0%)
    /// </summary>
    public Dictionary<string, BodyPartStat[]> Stats { get; } = new();

    public List<Item> EquippedItems = new();

    public bool IsInternal
    {
        get => (flags & Flag.Internal) != 0;
        set
        {
            if (value)
                flags |= Flag.Internal;
            else
                flags &= (byte)~Flag.Internal;
        }
    }
    public bool IsHard
    {
        get => (flags & Flag.Hard) != 0;
        set
        {
            if (value)
                flags |= Flag.Hard;
            else
                flags &= (byte)~Flag.Hard;
        }
    }
    public bool IsSoft
    {
        get => (flags & Flag.Soft) != 0;
        set
        {
            if (value)
                flags |= Flag.Soft;
            else
                flags &= (byte)~Flag.Soft;
        }
    }
    public bool OverlapsParent
    {
        get => (flags & Flag.Overlaps) != 0;
        set
        {
            if (value)
                flags |= Flag.Overlaps;
            else
                flags &= (byte)~Flag.Overlaps;
        }
    }

    IEnumerable<Skill> ISkillSource.Skills => Skills;

    public IEnumerable<Item> Items => EquippedItems;

    public BodyPart(string name, Body body, int maxHealth, float surfaceArea, Skill[] actions, string[] equipmentSlots, Injury[] conditions, byte flags, params BodyPart[] children)
    {
        this.bodyInfo = body;
        this.name = name;
        this.MaxHealth = maxHealth;
        this.Children = new List<BodyPart>(children);
        this.Skills = actions;
        this.surfaceArea = surfaceArea;
        this.flags = flags;
        this.equipmentSlots = new List<string>(equipmentSlots);
        this.injuries = new List<Injury>(conditions);

        foreach (var child in children)
            child.parent = this;
    }
    public BodyPart(Stream stream, Body body)
    {
        name = stream.ReadString();
        MaxHealth = stream.ReadFloat();
        surfaceArea = stream.ReadFloat();
        flags = (byte)stream.ReadByte();
        equipmentSlots = new List<string>();
        injuries = new List<Injury>();

        byte slotCount = (byte)stream.ReadByte();        
        for (int i = 0; i < slotCount; i++)
            equipmentSlots.Add(stream.ReadString());

        byte conditionCount = (byte)stream.ReadByte();
        for (int i = 0; i < conditionCount; i++)
            injuries.Add(new Injury(stream));

        this.bodyInfo = body;

        byte actionCount = (byte)stream.ReadByte();
        Skills = new Skill[actionCount];
        for (int i = 0; i < actionCount; i++)
        {
            Skills[i] = (Skill)Skill.FromBytes(stream);
        }

        Children = new List<BodyPart>();
        var childCount = stream.ReadByte();
        for (int i = 0; i < childCount; i++)
        {
            Children.Add(new BodyPart(stream, body)
            {
                parent = this,
            });
        }

        var statCount = stream.ReadByte();
        for (int i = 0; i < statCount; i++)
        {
            string statName = stream.ReadString();
            int modCount = stream.ReadByte();
            BodyPartStat[] stats = new BodyPartStat[modCount];
            for (int j = 0; j < modCount; j++)
            {
                stats[j] = new BodyPartStat(stream);
            }
            Stats[statName] = stats;
        }
    }

    public bool HasFlag(byte flag)
    {
        return (flags & flag) != 0;
    }

    public bool CanEquipSlot(string slot)
    {
        return equipmentSlots.Contains(slot);
    }

    public BodyPart? GetChild(string name)
    {
        foreach (BodyPart child in Children)
        {
            if (child.name.Equals(name))
            {
                return child;
            }
        }
        return null;
    }

    public BodyPart? GetChildByPath(string path)
    {
        if (path.Equals(Name))
            return this;
        if (path.StartsWith(Name + "/"))
            path = path.Substring(Name.Length + 1);
        string[] parts = path.Split('/');
        BodyPart? current = this;
        foreach (string part in parts)
        {
            if (part == "..")
                current = parent;
            else
                current = current.GetChild(part);
            if (current == null)
                return null;
        }
        return current;
    }

    public bool HasChild(string name)
    {
        return GetChild(name) != null;
    }

    public void RemoveChild(string name)
    {
        BodyPart? child = GetChild(name);
        if (child != null)
        {
            bodyInfo.OnPartRemoved(child);
            Children.Remove(child);

            OnChildRemoved?.Invoke(child);
        }
    }
    public void AddChild(BodyPart child)
    {
        Children.Add(child);
        child.parent = this;
        child.bodyInfo = BodyInfo;
    
        bodyInfo.OnPartAdded(child);

        OnChildAdded?.Invoke(child);
    }

    public void UpdateStatModifiers()
    {

        if (Owner == null)
            return;

        foreach (var entry in Stats)
        {
            var stat = Owner.GetOrCreateStat(entry.Key, 0);
            int i = 0;
            foreach (var mod in entry.Value)
            {
                stat.SetModifier(mod.CalculateFor(this, Path + "_mod" + i));
                i++;
            }
        }
    }

    public void RemoveStatModifiers()
    {
        foreach (var entry in Stats)
        {
            var stat = Owner.GetOrCreateStat(entry.Key);
            int i = 0;
            foreach (var mod in entry.Value)
            {
                stat.RemoveModifier(Path + "_mod" + i);
                i++;
            }
        }
    }

    public void AddInjury(Injury condition)
    {
        injuries.Add(condition);
        OnInjuryAdded?.Invoke(condition);
        bodyInfo._invokeInjuryEvent(this, condition, true);

        if (Health <= 0)
            bodyInfo.OnPartDie(this);

        if (!SidedLogic.Instance.IsClient())
            UpdateStatModifiers();
    }

    public void RemoveInjury(Injury condition)
    {
        for (int i = injuries.Count - 1; i >= 0; i--)
        {
            if (injuries[i].Equals(condition))
            {
                injuries.RemoveAt(i);
                OnInjuryRemoved?.Invoke(condition);
                bodyInfo._invokeInjuryEvent(this, condition, false);
                return;
            }
        }
    }

    public void RemoveCondition(int index)
    {
        Injury condition = injuries[index];
        injuries.RemoveAt(index);
        OnInjuryRemoved?.Invoke(condition);
    }

    void IInventoryHolder.AddItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            return;
        
        Equip(ep);
    }
    void IInventoryHolder.RemoveItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            return;
        
        Unequip(ep);
    }

    public void Equip(EquipmentProperty ep)
    {
        if (ep.EquippedPart != null)
            ep.EquippedPart.Unequip(ep);
        
        EquippedItems.Add(ep.Item);
        ep.EquippedPart = this;
        ep.Item.Holder = this;
    }
    public void Unequip(EquipmentProperty ep)
    {
        EquippedItems.Remove(ep.Item);
        ep.EquippedPart = null;
        ep.Item.Holder = null;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(name);
        stream.WriteFloat(MaxHealth);
        stream.WriteFloat(surfaceArea);
        stream.WriteByte(flags);
        
        stream.WriteByte((byte)equipmentSlots.Count);
        foreach (string tag in equipmentSlots)
        {
            stream.WriteString(tag);
        }

        stream.WriteByte((byte)injuries.Count);
        foreach (Injury condition in injuries)
        {
            condition.ToBytes(stream);
        }

        stream.WriteByte((byte)Skills.Length);
        foreach (var action in Skills)
        {
            action.ToBytes(stream);
        }
        stream.WriteByte((byte)Children.Count);
        foreach (BodyPart child in Children)
        {
            child.ToBytes(stream);
        }

        stream.WriteByte((byte)Stats.Count);
        foreach (var entry in Stats)
        {
            stream.WriteString(entry.Key);
            stream.WriteByte((Byte)entry.Value.Length);
            foreach (var mod in entry.Value)
            {
                mod.ToBytes(stream);
            }
        }
    }

    public string PrintPretty()
    {
        return PrintPretty("", true);
    }

    public string PrintPretty(string indent, bool last)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(indent);
        if (last)
        {
            sb.Append("\\-");
            indent += "  ";
        }
        else
        {
            sb.Append("|-");
            indent += "| ";
        }
        sb.AppendLine(Name);

        for (int i = 0; i < Children.Count; i++)
            sb.Append(Children[i].PrintPretty(indent, i == Children.Count - 1));

        return sb.ToString();
    }

    public void ClearEvents()
    {
        OnChildAdded = null;
        OnChildRemoved = null;
        OnInjuryAdded = null;
        OnInjuryRemoved = null;
    }

    /// <summary>
    /// Creates a new body structure for a creature that follows human anatomy
    /// </summary>
    /// <param name="owner">The creature that this body is for</param>
    /// <returns>The Torso body part</returns>
    public static BodyPart NewHumanoidBody(Body body)
    {
        return new Builder(body, Parts.TORSO).Health(40).Size(100).Soft().Equipment(EquipmentSlot.Back, EquipmentSlot.Chest, EquipmentSlot.Waist).Children(
            new Builder(body, Parts.NECK).Health(25).Size(7.5f).Soft().Equipment(EquipmentSlot.Neck)
            .Stat(CreatureStats.BLOOD_FLOW, 0, -1, StatModifierType.Percent).Stat(CreatureStats.RESPIRATION, 0, -1, StatModifierType.Percent)
            .Children(
                new Builder(body, Parts.HEAD).Health(25).Size(300).Soft().Equipment(EquipmentSlot.Head).Children(
                    new Builder(body, Parts.SKULL).Health(25).Size(90).Flags(Flag.Internal, Flag.Hard).Children(
                        new Builder(body, Parts.BRAIN).Health(10).Size(80).Flags(Flag.Internal, Flag.Soft).Stat(CreatureStats.CONSCIOUSNESS, 1).Build()
                    ).Build(),
                    new Builder(body, Parts.EAR_LEFT).Health(12).Size(7).Flags(Flag.Overlaps, Flag.Soft).Stat(CreatureStats.HEARING, 0.5f).Equipment(EquipmentSlot.Ear).Build(),
                    new Builder(body, Parts.EAR_RIGHT).Health(12).Size(7).Flags(Flag.Overlaps, Flag.Soft).Stat(CreatureStats.HEARING, 0.5f).Equipment(EquipmentSlot.Ear).Build(),
                    new Builder(body, Parts.EYE_RIGHT).Health(5).Size(7).Flags(Flag.Overlaps, Flag.Soft).Stat(CreatureStats.SIGHT, 0.5f).Equipment(EquipmentSlot.Eye).Build(),
                    new Builder(body, Parts.EYE_LEFT).Health(5).Size(7).Flags(Flag.Overlaps, Flag.Soft).Stat(CreatureStats.SIGHT, 0.5f).Equipment(EquipmentSlot.Eye).Build(),
                    new Builder(body, Parts.NOSE).Health(10).Size(10).Flags(Flag.Overlaps, Flag.Soft).Stat(CreatureStats.RESPIRATION, 0, -0.15f, StatModifierType.Percent).Build(),
                    new Builder(body, Parts.JAW).Health(20).Size(10).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.SOCIAL, 0, -0.8f, StatModifierType.Percent, true).Children(
                        new Builder(body, Parts.TONGUE).Health(10).Size(1).Flags(Flag.Soft, Flag.Internal).Stat(CreatureStats.SOCIAL, 0, -0.8f, StatModifierType.Percent, true)
                    ).Build()
                ).Build()
            ).Build(),
            new Builder(body, Parts.SHOULDER_LEFT).Health(30).Size(12).Flags(Flag.Hard, Flag.Soft).Equipment(EquipmentSlot.Shoulder)
            .Stat(CreatureStats.AGILITY, 0, -0.1f, StatModifierType.Percent, true)
            .Children(
                new Builder(body, Parts.ARM_LEFT).Health(30).Size(150).Flags(Flag.Hard, Flag.Soft)
                .Stat(CreatureStats.AGILITY, 0, -0.1f, StatModifierType.Percent, true).Stat(CreatureStats.UTILITY_STRENGTH, 0, 20f, StatModifierType.Flat, true)
                .Equipment(EquipmentSlot.Arm).Children(
                    new Builder(body, Parts.HAND_LEFT).Health(20).Size(15).Flags(Flag.Soft, Flag.Hard, Flag.Soft).Equipment(EquipmentSlot.Wrist, EquipmentSlot.Hand, EquipmentSlot.Hold)
                    .Stat(CreatureStats.AGILITY, 0, -0.1f, StatModifierType.Percent, true).Stat(CreatureStats.UTILITY_STRENGTH, 0, -0.1f, StatModifierType.Percent, true)
                    .Skills(new AttackSkill(InjuryType.Bruise).WithName("Soco").WithIcon("punch"))
                    .Children(
                        new Builder(body, Parts.FINGER_THUMB_LEFT).Health(8).Size(6).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.15f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_INDEX_LEFT).Health(8).Size(7).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_MIDDLE_LEFT).Health(8).Size(8).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_RING_LEFT).Health(8).Size(7).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_LITTLE_LEFT).Health(8).Size(6).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent)
                    )
                ).Build()
            ).Build(),
            new Builder(body, Parts.SHOULDER_RIGHT).Health(30).Size(12).Flags(Flag.Soft, Flag.Hard, Flag.Soft).Equipment(EquipmentSlot.Shoulder)
            .Stat(CreatureStats.AGILITY, 0, -0.1f, StatModifierType.Percent, true)
            .Children(
                new Builder(body, Parts.ARM_RIGHT).Health(30).Size(150).Flags(Flag.Soft, Flag.Hard, Flag.Soft).Equipment(EquipmentSlot.Arm)
                .Stat(CreatureStats.AGILITY, 0, -0.1f, StatModifierType.Percent, true).Stat(CreatureStats.UTILITY_STRENGTH, 0, 20f, StatModifierType.Flat, true)
                .Children(
                    new Builder(body, Parts.HAND_RIGHT).Health(20).Size(15).Flags(Flag.Soft, Flag.Hard, Flag.Soft).Equipment(EquipmentSlot.Wrist, EquipmentSlot.Hand, EquipmentSlot.Hold)
                    .Stat(CreatureStats.AGILITY, 0, -0.1f, StatModifierType.Percent, true).Stat(CreatureStats.UTILITY_STRENGTH, 0, -0.1f, StatModifierType.Percent, true)
                    .Skills(new AttackSkill(InjuryType.Bruise).WithName("Soco").WithIcon("punch"))
                    .Children(
                        new Builder(body, Parts.FINGER_THUMB_RIGHT).Health(8).Size(6).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.15f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_INDEX_RIGHT).Health(8).Size(7).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_MIDDLE_RIGHT).Health(8).Size(8).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_RING_RIGHT).Health(8).Size(7).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent),
                        new Builder(body, Parts.FINGER_LITTLE_RIGHT).Health(8).Size(6).Flags(Flag.Overlaps, Flag.Soft, Flag.Hard).Stat(CreatureStats.DEXTERITY, 0, -0.05f, StatModifierType.Percent)
                    )
                    .Build()
                ).Build()
            ).Build(),
            new Builder(body, Parts.LEFT_LUNG).Health(10).Size(5).Flags(Flag.Internal, Flag.Soft).Stat(CreatureStats.RESPIRATION, 0.5f).Build(),
            new Builder(body, Parts.RIGHT_LUNG).Health(10).Size(5).Flags(Flag.Internal, Flag.Soft).Stat(CreatureStats.RESPIRATION, 0.5f).Build(),
            new Builder(body, Parts.KIDNEY_RIGHT).Health(15).Size(2).Flags(Flag.Internal, Flag.Soft).Stat(CreatureStats.BLOOD_FLOW, 0, -0.25f, StatModifierType.Percent).Build(),
            new Builder(body, Parts.KIDNEY_LEFT).Health(15).Size(2).Flags(Flag.Internal, Flag.Soft).Stat(CreatureStats.BLOOD_FLOW, 0, -0.25f, StatModifierType.Percent).Build(),
            new Builder(body, Parts.HEART).Health(15).Size(3.5f).Flags(Flag.Internal, Flag.Soft).Stat(CreatureStats.BLOOD_FLOW, 1f).Build(),
            new Builder(body, Parts.RIGHT_LEG).Health(30).Size(25).Equipment(EquipmentSlot.Leg).Stat(CreatureStats.MOVEMENT_STRENGTH, 0, -0.5f, StatModifierType.Percent, true)
            .Skills(new AttackSkill(InjuryType.Bruise).WithName("Chute").WithIcon("kick"))
            .Children(
                new Builder(body, Parts.FOOT_RIGHT).Health(25).Size(10).Equipment(EquipmentSlot.Foot).Stat(CreatureStats.MOVEMENT_STRENGTH, 0, -0.5f, StatModifierType.Percent, true).Build()
            ).Build(),
            new Builder(body, Parts.LEFT_LEG).Health(30).Size(25).Equipment(EquipmentSlot.Leg).Stat(CreatureStats.MOVEMENT_STRENGTH, 0, -0.5f, StatModifierType.Percent, true)
            .Skills(new AttackSkill(InjuryType.Bruise).WithName("Chute").WithIcon("kick"))
            .Children(
                new Builder(body, Parts.FOOT_LEFT).Health(25).Size(10).Equipment(EquipmentSlot.Foot).Stat(CreatureStats.MOVEMENT_STRENGTH, 0, -0.5f, StatModifierType.Percent, true).Build()
            ).Build()
        ).Build();
    }

    public static string BuildPath(params string[] names)
    {
        var result = new StringBuilder();
        foreach (var part in names)
        {
            result.Append(part);
            result.Append(".");
        }
        return result.ToString(0, result.Length-1);
    }

    public Single GetStat(String id, Single defaultValue = 0)
    {
        if (Stats.ContainsKey(id))
        {
            float sum = 0;
            foreach (var stat in Stats[id])
            {
                if (stat.op != StatModifierType.Flat)
                    continue;
                sum += stat.CalculateFor(this).Value;
            }

            return sum;
        }
        return defaultValue;
    }

    public static class Parts
    {
        public const string RIGHT = "right";
        public const string LEFT = "left";
        public const string TORSO = "torso";
        public const string NECK = "neck";
        public const string HEAD = "head";
        public const string HEART = "heart";
        public const string LUNG = "lung";
        public const string LEFT_LUNG = LUNG + " " + LEFT;
        public const string RIGHT_LUNG = LUNG + " " + RIGHT;
        public const string EAR = "ear";
        public const string EYE = "eye";
        public const string NOSE = "nose";
        public const string EAR_RIGHT = EAR + " " + RIGHT;
        public const string EAR_LEFT = EAR + " " + LEFT;
        public const string EYE_RIGHT = EYE + " " + RIGHT;
        public const string EYE_LEFT = EYE + " " + LEFT;
        public const string ARM = "arm";
        public const string ARM_LEFT = ARM + " " + LEFT;
        public const string ARM_RIGHT = ARM + " " + RIGHT;
        public const string SHOULDER = "shoulder";
        public const string SHOULDER_LEFT = SHOULDER + " " + LEFT;
        public const string SHOULDER_RIGHT = SHOULDER + " " + RIGHT;
        public const string HAND = "hand";
        public const string HAND_LEFT = HAND + " " + LEFT;
        public const string HAND_RIGHT = HAND + " " + RIGHT;
        public const string LEG = "leg";
        public const string RIGHT_LEG = LEG + " " + RIGHT;
        public const string LEFT_LEG = LEG + " " + LEFT;
        public const string FOOT = "foot";
        public const string FOOT_LEFT = FOOT + " " + LEFT;
        public const string FOOT_RIGHT = FOOT + " " + RIGHT;
        public const string BRAIN = "brain";
        public const string SKULL = "skull";
        public const string TIBIA = "tibia";
        public const string FEMUR = "femur";
        public const string JAW = "jaw";
        public const string FINGER = "finger";
        public const string FINGER_THUMB = "finger thumb";
        public const string FINGER_INDEX = "finger index";
        public const string FINGER_MIDDLE = "finger middle";
        public const string FINGER_RING = "finger ring";
        public const string FINGER_LITTLE = "finger little";
        public const string FINGER_THUMB_RIGHT = FINGER_THUMB + " " + RIGHT;
        public const string FINGER_INDEX_RIGHT = FINGER_INDEX + " " + RIGHT;
        public const string FINGER_MIDDLE_RIGHT = FINGER_MIDDLE + " " + RIGHT;
        public const string FINGER_RING_RIGHT = FINGER_RING + " " + RIGHT;
        public const string FINGER_LITTLE_RIGHT = FINGER_LITTLE + " " + RIGHT;
        public const string FINGER_THUMB_LEFT = FINGER_THUMB + " " + LEFT;
        public const string FINGER_INDEX_LEFT = FINGER_INDEX + " " + LEFT;
        public const string FINGER_MIDDLE_LEFT = FINGER_MIDDLE + " " + LEFT;
        public const string FINGER_RING_LEFT = FINGER_RING + " " + LEFT;
        public const string FINGER_LITTLE_LEFT = FINGER_LITTLE + " " + LEFT;
        public const string KIDNEY = "kidney";
        public const string KIDNEY_RIGHT = KIDNEY + " " + RIGHT;
        public const string KIDNEY_LEFT = KIDNEY + " " + LEFT;
        public const string TONGUE = "tongue";
    
        public static string Translate(string name)
        {
            switch (name)
            {
                case TORSO:
                    return "Peito";
                case NECK:
                    return "Pescoço";
                case HEAD:
                    return "Cabeça";
                case HEART:
                    return "Coração";
                case LUNG:
                    return "Pulmão";
                case EAR:
                    return "Orelha";
                case EAR_LEFT:
                    return "Orelha Esquerda";
                case EAR_RIGHT:
                    return "Orelha Direita";
                case EYE:
                    return "Olho";
                case NOSE:
                    return "Nariz";
                case ARM:
                    return "Braço";
                case HAND:
                    return "Mão";
                case HAND_LEFT:
                    return "Mão Esquerda";
                case HAND_RIGHT:
                    return "Mão Direita";
                case LEG:
                    return "Perna";
                case LEFT_LEG:
                    return "Perna Esquerda";
                case RIGHT_LEG:
                    return "Perna Direita";
                case FOOT:
                    return "Pé";
                case BRAIN:
                    return "Cérebro";
                case JAW:
                    return "Mandibula";
                case FINGER:
                    return "Dedo";
                case FINGER_INDEX:
                    return "Dedo Indicador";
                case FINGER_MIDDLE:
                    return "Dedo do Meio";
                case FINGER_RING:
                    return "Dedo Anelar";
                case FINGER_LITTLE:
                    return "Dedinho";
                case FINGER_THUMB:
                    return "Dedão";
                case KIDNEY:
                    return "Rim";
                case TONGUE:
                    return "Língua";
                case SHOULDER:
                    return "Ombro";
                case SKULL:
                    return "Crânio";
                default:
                {
                    if (name.Contains(" "))
                    {
                        var index = name.LastIndexOf(" ");
                        var left = name.Substring(0, index);
                        var right = name.Substring(index+1);
                        if (right.Equals(LEFT))
                        {
                            return Translate(left) + " Esquerdo";
                        }
                        if (right.Equals(RIGHT))
                        {
                            return Translate(left) + " Direito";
                        }

                    }
                    return name;
                }
            }
        }
    }

    public class Builder
    {
        private string name;
        private Body body;
        private int maxHealth;
        private float sizePercentage;
        private Skill[] skills;
        private byte flags;
        private List<BodyPart> children = new List<BodyPart>();
        private List<string> equipmentSlots = new List<string>();
        private List<Injury> conditions = new List<Injury>();
        private Dictionary<string, List<BodyPartStat>> stats { get; } = new();
        
        public Builder(Body body, string name)
        {
            this.name = name;
            this.body = body;
            maxHealth = 1;
            sizePercentage = 0;
            skills = Array.Empty<Skill>();
            flags = 0;
        }

        public Builder Name(string name)
        {
            this.name = name;
            return this;
        }

        public Builder Health(int maxHealth)
        {
            this.maxHealth = maxHealth;
            return this;
        }

        public Builder Size(float percentage)
        {
            sizePercentage = percentage/100;
            return this;
        }

        public Builder Skills(params Skill[] actions)
        {
            this.skills = actions;
            return this;
        }
        
        public Builder Equipment(params string[] slots)
        {
            equipmentSlots.AddRange(slots);
            return this;
        }

        public Builder Flags(params byte[] flags)
        {
            foreach (byte flag in flags)
            {
                this.flags |= flag;
            }
            return this;
        }

        public Builder Soft()
        {
            return Flags(Flag.Soft);
        }
        public Builder Hard()
        {
            return Flags(Flag.Hard);
        }
        public Builder Overlaps()
        {
            return Flags(Flag.Overlaps);
        }

        public Builder Child(BodyPart child)
        {
            children.Add(child);
            return this;
        }

        public Builder Children(params BodyPart[] children)
        {
            this.children.AddRange(children);
            return this;
        }

        public Builder Children(params Builder[] children)
        {
            return Children(children.Select(c => c.Build()).ToArray());
        }

        public Builder Stat(string stat, float atFull, float atZero = 0, StatModifierType operation = StatModifierType.Flat, bool standaloneHPOnly = false)
        {
            if (!stats.ContainsKey(stat))
                stats[stat] = new List<BodyPartStat>();
            stats[stat].Add(new BodyPartStat(atFull, atZero, operation, standaloneHPOnly));
            return this;
        }

        public BodyPart Build()
        {
            var ret = new BodyPart(name, body, maxHealth, sizePercentage, skills, equipmentSlots.ToArray(), conditions.ToArray(), flags, children.ToArray());
            foreach (var entry in stats)
            {
                ret.Stats[entry.Key] = entry.Value.ToArray();
            }
            return ret;
        }
    }
}

public class BodyPartRef : ISerializable
{
    private BodyPart? cache;
    public CreatureRef Owner;
    public string Path;
    public BodyPart? BodyPart
    {
        get
        {
            if (cache == null)
                cache = Owner.Creature?.GetBodyPart(Path);
            return cache;
        }
    } 
    
    public BodyPartRef(CreatureRef owner, string path)
    {
        Owner = owner;
        Path = path;
    }
    public BodyPartRef(BodyPart part)
    {
        Owner = new CreatureRef(part.Owner);
        Path = part.Path;
    }
    public BodyPartRef(Stream stream)
    {
        Owner = new CreatureRef(stream);
        Path = stream.ReadString();
    }
    public void ToBytes(Stream stream)
    {
        Owner.ToBytes(stream);
        stream.WriteString(Path);
    }
}
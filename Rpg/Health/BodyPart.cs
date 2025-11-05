using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Inventory;

namespace Rpg;

public partial class BodyPart : ISerializable, ISkillSource, IItemHolder, IDamageable, IFeatureContainer, ITaggable, IStatHolder
{
    public readonly struct BodyPartStat(float atFull, float atZero, StatModifierType op, bool sho, bool ato)
        : ISerializable
    {
        public readonly float atFull = atFull;
        public readonly float atZero = atZero;
        public readonly StatModifierType op = op;
        public readonly bool standloneHealthOnly = sho;
        public readonly bool appliesToOwner = ato;

        public BodyPartStat(Stream stream) : this(stream.ReadFloat(), stream.ReadFloat(), (StatModifierType)stream.ReadByte(), stream.ReadByte() == 1, stream.ReadByte() == 1)
        {
        }

        public void ToBytes(Stream stream)
        {
            stream.WriteFloat(atFull);
            stream.WriteFloat(atZero);
            stream.WriteByte((byte)op);
            stream.WriteByte((byte)(standloneHealthOnly ? 1 : 0));
            stream.WriteByte((byte)(appliesToOwner ? 1 : 0));
        }

        public StatModifier CalculateFor(BodyPart target, string? name = null)
        {
            name ??= target.Path + "_mod";
            double hpPercentage;
            if (standloneHealthOnly)
                hpPercentage = target.HealthStandalone/target.MaxHealth;
            else
                hpPercentage = target.Health/target.MaxHealth;

            return new StatModifier(name, RpgMath.Lerp(atZero, atFull, (float)hpPercentage), op);
        }
    }
    
    public event Action<BodyPart>? OnChildAdded;
    public event Action<BodyPart>? OnChildRemoved;
    public event Action<Injury>? OnInjuryAdded;
    public event Action<Injury>? OnInjuryRemoved;
    public event Action<EquipmentProperty, string>? OnEquipped;
    public event Action<EquipmentProperty>? OnUnequipped;

    /// <summary>
    /// The name of the body part. This must be unique within its parent.
    /// </summary>
    public readonly string Name;
    string ISkillSource.Name => Name;
    string IFeatureContainer.Name => Name;

    /// <summary>
    /// BBCode link to this body part
    /// </summary>
    public string BBLink => Name + (Owner != null ? " de " + Owner.BBLink : "");
    /// <summary>
    /// The group this body part belongs to (e.g., "left arm", "right leg", "head").
    /// This is used to calculate stats and effects that apply to groups of body parts.
    /// </summary>
    public readonly string Group;

    /// <summary>
    /// The body this part belongs to.
    /// </summary>
    public Body? Body
    {
        get;
        internal set
        {
            field = value;
            foreach (BodyPart child in Children)
                child.Body = value;
        }
    }

    /// <summary>
    /// The creature that owns this body part.
    /// Shorthand for Body?.Owner
    /// </summary>
    public Creature? Owner => Body?.Owner;
    /// <summary>
    /// The surface area of this body part relative to its parent.
    /// </summary>
    public float Size { get; }
    /// <summary>
    /// The absolute surface area of this body part (relative to the whole body).
    /// </summary>
    public float AbsoluteSize => Size * (Parent?.Size ?? 1);
    private readonly Feature[] ownerFeatures;
    /// <summary>
    /// Features provided to the owner by this body part
    /// </summary>
    public IEnumerable<Feature> FeaturesForOwner => ownerFeatures;
    /// <summary>
    /// All enabled features on this body part and its owner.
    /// </summary>
    public IEnumerable<Feature> EnabledFeatures
    {
        get
        {
            foreach (var t in features.Values)
            {
                if (t.enabled)
                    yield return t.feature;
            }

            if (Owner != null)
            {
                foreach (var f in Owner.Features)
                    yield return f;
            }
        }
    }

    /// <summary>
    /// Enabled features that only affect this body part.
    /// </summary>
    public IEnumerable<Feature> SelfEnabledFeatures
    {
        get
        {
            foreach (var t in features.Values)
            {
                if (t.enabled)
                    yield return t.feature;
            }
        }
    }

    private readonly Skill[] skills;
    /// <summary>
    /// Skills provided by this body part
    /// </summary>
    public IEnumerable<Skill> Skills => skills;
    
    private readonly Dictionary<string, Item?> equipmentSlots;
    /// <summary>
    /// The equipment slots that this body part has
    /// </summary>
    public IEnumerable<string> EquipmentSlots => equipmentSlots.Keys;

    /// <summary>
    /// The parent body part of this part. Null if this is the root part.
    /// </summary>
    public BodyPart? Parent { get; private set; }
    /// <summary>
    /// The full path to this body part in the body hierarchy.
    /// </summary>
    public string Path => Parent == null ? Name : Parent.Path + "/" + Name;
    /// <summary>
    /// The root body part of this body.
    /// </summary>
    public BodyPart Root => Parent == null ? this : Parent.Root;
    public bool IsRoot => Parent == null;

    private readonly Dictionary<string, BodyPart> children;
    /// <summary>
    /// The child body parts of this part. This can be interpreted as organs, sub-parts or extensions of this part.
    /// </summary>
    public IReadOnlyCollection<BodyPart> Children => children.Values;
    /// <summary>
    /// The internal organs of this body part.
    /// This is found using the <c>internal</c> tag.
    /// </summary>
    public IEnumerable<BodyPart> InternalOrgans
    {
        get
        {
            foreach (var child in Children)
            {
                if (child.IsInternal)
                    yield return child;
            }
        }
    }
    /// <summary>
    /// Children including children of children.
    /// </summary>
    public IEnumerable<BodyPart> AllChildren
    {
        get
        {
            foreach (BodyPart child in Children)
            {
                yield return child;
                foreach (BodyPart descendant in child.AllChildren)
                    yield return descendant;
            }
        }
    }
    
    private readonly List<Injury> injuries;
    /// <summary>
    /// The injuries currently affecting this body part.
    /// Exposed as a read-only list to prevent external mutation.
    /// </summary>
    public IReadOnlyList<Injury> Injuries => injuries.AsReadOnly();
    /// <summary>
    /// Some body parts feel more pain than others. This multiplier defines how much more pain this body part feels.
    /// </summary>
    public readonly float PainMultiplier = 1;
    /// <summary>
    /// The total pain currently felt from this body part.
    /// </summary>
    public double Pain => injuries.Sum(injury => injury.Type.Pain * injury.Severity * PainMultiplier);
    public double MaxHealth { get; private set; }
    /// <summary>
    /// The current health of this body part, ignoring parent body parts.
    /// </summary>
    public double HealthStandalone
    {
        get
        {
            double sum = MaxHealth;
            foreach (Injury injury in injuries)
            {
                if (injury.Type.Instakill)
                    return 0;
                sum -= injury.Severity;
            }
            return Math.Max(sum, 0);
        }
    }
    /// <summary>
    /// The current health of this body part, considering if the parent is dead.
    /// </summary>
    public double Health
    {
        get
        {
            if (Parent is { Health: <= 0 })
                return 0;

            return HealthStandalone;
        }
    }
    public bool IsAlive => Health > 0;
    

    /// <summary>
    /// What stats are provided by this body part. Dynamically updated on HP(100% to 0%)
    /// </summary>
    public readonly Dictionary<string, BodyPartStat[]> ProvidedStats = new();
    /// <summary>
    /// Damage modifiers applied when this body part takes damage of a certain type.
    /// </summary>
    public readonly Dictionary<DamageType, StatModifier[]> DamageModifiers = new();
    
    /// <summary>
    /// Equipment that is currently protecting this body part.
    /// </summary>
    public IEnumerable<EquipmentProperty> CoveringEquipment => Body?.GetCoveringEquipment(this) ?? Enumerable.Empty<EquipmentProperty>();
    /// <summary>
    /// Items currently equipped on this body part.
    /// </summary>
    public IEnumerable<Item> Items
    {
        get
        {
            foreach (var it in equipmentSlots.Values)
            {
                if (it != null)
                    yield return it;
            }
        }
    }
    public Board? Board => Owner?.Board;

    public bool IsInternal => Is(BodyTags.Internal);
    public bool IsHard => Is(BodyTags.Hard);
    public bool IsSoft => !Is(BodyTags.Hard);


    public BodyPart(string name, string group, int maxHealth, float surfaceArea, float painMult, Skill[] actions, Feature[] features, string[] equipmentSlots, Injury[] conditions, string[] tags, params BodyPart[] children)
    {
        Name = name;
        Group = group;
        MaxHealth = maxHealth;
        PainMultiplier = painMult;
        this.children = new Dictionary<string, BodyPart>(children.Length);
        skills = actions;
        ownerFeatures = features;
        Size = surfaceArea;
        this.equipmentSlots = new Dictionary<string, Item?>();
        foreach (string slot in equipmentSlots)
            this.equipmentSlots[slot] = null;
        injuries = new List<Injury>(conditions);
        this.tags = new HashSet<string>(tags);

        foreach (BodyPart child in children)
        {
            if (this.children.ContainsKey(child.Name))
                throw new ArgumentException($"Duplicate child name '{child.Name}' when creating BodyPart '{Name}'.");
            this.children[child.Name] = child;
            child.Parent = this;
        }
    }
    public BodyPart(Stream stream, Body? body = null)
    {
        Name = stream.ReadString();
        Group = stream.ReadString();
        MaxHealth = stream.ReadDouble();
        Size = stream.ReadFloat();

        equipmentSlots = new Dictionary<string, Item?>();
        injuries = new List<Injury>();

        byte slotCount = (byte)stream.ReadByte();        
        for (int i = 0; i < slotCount; i++)
        {
            string slot = stream.ReadString();
            if (stream.ReadByte() == 0)
                equipmentSlots[slot] = null;
            else
                equipmentSlots[slot] = new Item(stream);
        }

        byte conditionCount = (byte)stream.ReadByte();
        for (int i = 0; i < conditionCount; i++)
            injuries.Add(new Injury(stream));

        byte actionCount = (byte)stream.ReadByte();
        skills = new Skill[actionCount];
        for (int i = 0; i < actionCount; i++)
        {
            skills[i] = Skill.FromBytes(stream);
        }

        int childCount = stream.ReadByte();
        children = new Dictionary<string, BodyPart>(childCount);
        for (int i = 0; i < childCount; i++)
        {
            var child = new BodyPart(stream, body)
            {
                Parent = this
            };
            if (children.ContainsKey(child.Name))
                throw new ArgumentException($"Duplicate child name '{child.Name}' when deserializing BodyPart '{Name}'.");
            children[child.Name] = child;
        }

        int count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string statName = stream.ReadString();
            int modCount = stream.ReadByte();
            var stats = new BodyPartStat[modCount];
            for (int j = 0; j < modCount; j++)
            {
                stats[j] = new BodyPartStat(stream);
            }
            ProvidedStats[statName] = stats;
        }

        count = stream.ReadByte();
        ownerFeatures = new Feature[count];
        for (int i = 0; i < count; i++)
        {
            ownerFeatures[i] = Feature.FromBytes(stream);
        }

        StatsFromBytes(stream);
        FeaturesFromBytes(stream);
        CustomDataFromBytes(stream);
        TagsFromBytes(stream);
        Body = body;
    }

    public bool CanEquipSlot(string slot)
    {
        return equipmentSlots.ContainsKey(slot) && equipmentSlots[slot] == null;
    }

    public Item? GetEquippedItem(string slot)
    {
        return equipmentSlots.GetValueOrDefault(slot);
    }

    public BodyPart? GetChild(string name)
    {
        if (children.TryGetValue(name, out var child))
            return child;
        return null;
    }

    public BodyPart? GetChildByPath(string path)
    {
        if (path.Equals(Name))
            return this;
        if (path.StartsWith(Name + "/"))
            path = path[(Name.Length + 1)..];
        string[] parts = path.Split('/');
        BodyPart? current = this;
        foreach (string part in parts)
        {
            current = part == ".." ? Parent : current.GetChild(part);
            if (current == null)
                return null;
        }
        return current;
    }

    public bool HasChild(string name)
    {
        return children.ContainsKey(name);
    }

    public void RemoveChild(string name)
    {
        BodyPart? child = GetChild(name);
        if (child == null) return;

    Body?.OnPartRemoved(child);
    child.Parent = null;
    child.Body = null;
    children.Remove(child.Name);

        OnChildRemoved?.Invoke(child);
    }
    public void AddChild(BodyPart child)
    {
        // Remove from previous parent if any
        child.Parent?.RemoveChild(child.Name);

        // If we already have a child with the same name, remove it first to keep uniqueness
        if (children.ContainsKey(child.Name))
            RemoveChild(child.Name);

        children[child.Name] = child;
        child.Parent = this;
        child.Body = Body;

        Body?.OnPartAdded(child);

        OnChildAdded?.Invoke(child);
    }

    public void UpdateStatModifiers()
    {

        if (Owner == null)
            return;

    foreach (var entry in ProvidedStats)
        {
            var stat = Owner.GetStat(entry.Key);
            if (stat == null)
                continue;
            int i = 0;
            foreach (BodyPartStat mod in entry.Value)
            {
                if (!mod.appliesToOwner) continue;
                stat.SetModifier(mod.CalculateFor(this, Path + "_mod" + i));
                i++;
            }
        }
    }

    public void RemoveStatModifiers()
    {
        if (Owner == null)
            return;
        
    foreach (var entry in ProvidedStats)
        {
            var stat = Owner.GetStat(entry.Key);
            if (stat == null)
                continue;
            int i = 0;
            foreach (BodyPartStat mod in entry.Value)
            {
                if (!mod.appliesToOwner) continue;
                stat.RemoveModifier(Path + "_mod" + i);
                i++;
            }
        }
    }

    public double Damage(DamageSource source, double amount)
    {

        DamageType type = source.Type;
        List<StatModifier> dmgMods = [.. DamageModifiers.GetValueOrDefault(type, Array.Empty<StatModifier>())];
        if (Owner != null)
        {
            foreach (Feature feat in EnabledFeatures)
                dmgMods.AddRange(feat.ModifyReceivingDamageModifiers(this, source, amount));
        }

        double damage = Stat.ApplyModifiers(dmgMods, (float)amount);
        string formula = damage.ToString("0.##") + " após modificadores";
        if (Owner != null)
        {
            foreach (Feature feat in EnabledFeatures)
            {
                var info = feat.ModifyReceivingDamage(this, source, damage);
                if (damage == info.Item1)
                    continue;
                formula += "\n" + info.Item1;
                if (info.Item2 != null)
                    formula += info.Item2;
                damage = info.Item1;
            }
        }
        
        Owner?.Log($"{BBLink} recebeu [hint={formula}]{damage}[/hint] de dano {type.BBHint}.");
        
        if (damage <= 0)
            return 0;

        //TODO: Figure out damage overflow to parents and internals
        AddInjury(source.Type.InjuryResolver(source, damage, this));
        
        return damage;
    }

    public void AddInjury(Injury condition)
    {
        injuries.Add(condition);
        OnInjuryAdded?.Invoke(condition);
        Body?.NotifyInjury(this, condition, true);

        if (Health <= 0)
            Body?.OnPartDie(this);

        if (!SidedLogic.Instance.IsClient())
        {
            UpdateStatModifiers();
            foreach (var feat in EnabledFeatures)
            {
                feat.OnInjured(this, condition);
            }
        }
    }

    public void RemoveInjury(Injury condition)
    {
        for (int i = injuries.Count - 1; i >= 0; i--)
        {
            if (injuries[i].Equals(condition))
            {
                injuries.RemoveAt(i);
                OnInjuryRemoved?.Invoke(condition);
                Body?.NotifyInjury(this, condition, false);
                return;
            }
        }
    }

    public void RemoveInjury(int index)
    {
        Injury condition = injuries[index];
        injuries.RemoveAt(index);
        OnInjuryRemoved?.Invoke(condition);
    }
    
    public void AddFeature(Feature feature)
    {
        ((IFeatureContainer)this).AddFeature(feature);
        OnFeatureAdded?.Invoke(feature);
    }

    public Feature? RemoveFeature(string id)
    {
        var f = ((IFeatureContainer)this).RemoveFeature(id);
        if (f != null)
            OnFeatureRemoved?.Invoke(f);
        return f;
    }
    public bool CanAddItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            return CanEquipSlot(EquipmentSlot.Hold);
        
        return CanEquipSlot(ep.Slot) || CanEquipSlot(EquipmentSlot.Hold);
    }
    public void AddItem(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            Equip(item);
        else
        {
            if (CanEquipSlot(ep.Slot))
                Equip(item, ep.Slot);
            else
                Equip(item);
        }
    }
    public void RemoveItem(Item item)
    {
        Unequip(item);
    }

    public void Equip(Item item, string slot = EquipmentSlot.Hold)
    {
        if (!equipmentSlots.TryGetValue(slot, out Item? equipped))
            return;

        item.Holder?.RemoveItem(item);
        if (equipped != null)
            Unequip(equipped);
        equipmentSlots[slot] = item;

        if (slot == EquipmentSlot.Hold)
        {
            Body?.OnHeld(this, item);
        }
        else
        {
            var ep = item.GetProperty<EquipmentProperty>();
            if (ep == null || slot != ep.Slot) return;
            
            ep.OnEquip(this);
            Body?.OnEquipped(this, ep);
            OnEquipped?.Invoke(ep, slot);
        }
    }
    public void Unequip(Item item)
    {
        string? slot = null;
        foreach (var pair in equipmentSlots)
        {
            if (pair.Value != item) continue;
            
            slot = pair.Key;
            break;
        }

        if (slot == null)
            return;
        
        equipmentSlots[slot] = null;

        if (slot == EquipmentSlot.Hold)
        {
            Body?.OnUnheld(this, item);
        }
        else
        {
            var ep = item.GetProperty<EquipmentProperty>();
            if (ep == null || slot != ep.Slot) return;
            
            ep.OnUnequip();
            Body?.OnUnequip(this, ep);
            OnUnequipped?.Invoke(ep);
        }
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteString(Group);
        stream.WriteDouble(MaxHealth);
        stream.WriteFloat(Size);
        
        stream.WriteByte((byte)equipmentSlots.Count);
        foreach (var slot in equipmentSlots)
        {
            stream.WriteString(slot.Key);
            if (slot.Value != null)
            {
                stream.WriteByte(1);
                slot.Value.ToBytes(stream);
            }
            else
                stream.WriteByte(0);
        }

        stream.WriteByte((byte)injuries.Count);
        foreach (Injury condition in injuries)
        {
            condition.ToBytes(stream);
        }

        stream.WriteByte((byte)skills.Length);
        foreach (Skill action in Skills)
        {
            action.ToBytes(stream);
        }
        stream.WriteByte((byte)children.Count);
        foreach (BodyPart child in children.Values)
        {
            child.ToBytes(stream);
        }

        stream.WriteByte((byte)ProvidedStats.Count);
        foreach (var entry in ProvidedStats)
        {
            stream.WriteString(entry.Key);
            stream.WriteByte((byte)entry.Value.Length);
            foreach (BodyPartStat mod in entry.Value)
            {
                mod.ToBytes(stream);
            }
        }
        
        stream.WriteByte((byte)ownerFeatures.Length);
        foreach (var ownerFeature in ownerFeatures)
        {
            ownerFeature.ToBytes(stream);
        }

        StatsToBytes(stream);
        FeaturesToBytes(stream);
        CustomDataToBytes(stream);
        TagsToBytes(stream);
    }

    public JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["name"] = Name,
            ["maxHealth"] = MaxHealth,
            ["area"] = Size
        };

        if (!string.IsNullOrEmpty(Group) && Group != "default")
        {
            json["group"] = Group;
        }

        if (EquipmentSlots.Any())
        {
            var slotsArray = new JsonArray();
            foreach (string slot in EquipmentSlots)
            {
                slotsArray.Add(slot);
            }
            json["slots"] = slotsArray;
        }

        if (skills.Length > 0)
        {
            var skillsArray = new JsonArray();
            foreach (var skill in Skills)
            {
                //skillsArray.Add(skill.ToJson());
            }
            json["skills"] = skillsArray;
        }

        if (children.Count > 0)
        {
            var childrenArray = new JsonArray();
            foreach (var child in children.Values)
            {
                childrenArray.Add(child.ToJson());
            }
            json["children"] = childrenArray;
        }

        if (ProvidedStats.Count > 0)
        {
            var statsArray = new JsonArray();
            foreach (var entry in ProvidedStats)
            {
                var statName = entry.Key;
                foreach (var stat in entry.Value)
                {
                    var obj = new JsonObject();
                    obj["stat"] = statName;
                    obj["atFull"] = stat.atFull;
                    obj["atZero"] = stat.atZero;
                    obj["standaloneHPOnly"] = stat.standloneHealthOnly;
                    obj["operation"] = stat.op.ToString();
                    statsArray.Add(obj);
                }
            }
            json["stats"] = statsArray;
        }

        if (DamageModifiers.Count > 0)
        {
            var damageModifiersArray = new JsonArray();
            foreach (var entry in DamageModifiers)
            {
                var dt = entry.Key;
                foreach (var mod in entry.Value)
                {
                    var obj = new JsonObject();
                    obj["damageType"] = dt.Name;
                    obj["value"] = mod.Value;
                    obj["operation"] = mod.Type.ToString();
                }
            }
            json["damageModifiers"] = damageModifiersArray;
        }

        return json;
    }

    public string PrintPretty(string indent = "", bool last = true)
    {
        var sb = new StringBuilder();
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

        var childList = children.Values.ToList();
        for (int i = 0; i < childList.Count; i++)
            sb.Append(childList[i].PrintPretty(indent, i == childList.Count - 1));

        return sb.ToString();
    }

    public override string ToString()
    {
        return "BodyPart[" + Name + "]";
    }

    public float GetStat(string id, float defaultValue = 0)
    {
    if (!ProvidedStats.TryGetValue(id, out var stats)) return defaultValue;

    return stats.Where(stat => stat.op == StatModifierType.Flat).Sum(stat => stat.CalculateFor(this).Value);
    }
}

public class BodyPartRef(CreatureRef owner, string path) : ISerializable
{
    public CreatureRef Owner = owner;
    public readonly string Path = path;
    public BodyPart? BodyPart => field ??= Owner.Creature?.GetBodyPart(Path);

    public BodyPartRef(BodyPart part) : this(new CreatureRef(part.Owner!), part.Path)
    {
        if (part.Owner == null)
            throw new ArgumentException("BodyPart's owner cannot be null when creating a BodyPartRef.");
    }
    public BodyPartRef(Stream stream) : this(new CreatureRef(stream), stream.ReadString())
    {
    }
    public void ToBytes(Stream stream)
    {
        Owner.ToBytes(stream);
        stream.WriteString(Path);
    }
}
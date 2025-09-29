using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Inventory;

namespace Rpg;

public class BodyPart : ISerializable, ISkillSource, IItemHolder, IDamageable, IFeatureSource
{
    public static class Flag
    {
        public static byte None => 0x0;
        public static byte HasBone => 0x1;
        public static byte Hard => 0x2;
        public static byte Internal => 0x4;
        public static byte Overlaps => 0x8;
    }
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
    public event Action<Feature>? OnFeatureAdded;
    public event Action<Feature>? OnFeatureRemoved;

    public string Name { get; }
    public string BBLink => Name + (Owner != null ? " de " + Owner.BBLink : "");
    public string Group { get; }

    public Body? Body
    {
        get;
        private set
        {
            field = value;
            foreach (BodyPart child in Children)
                child.Body = value;
        }
    }

    public Creature? Owner => Body?.Owner;
    public float Size { get; }
    public float AbsoluteSize => Size * (Parent?.Size ?? 1);
    private byte flags;
    protected Dictionary<string, byte[]> customData = new();
    private readonly Feature[] ownerFeatures;
    public IEnumerable<Feature> FeaturesForOwner => ownerFeatures;
    private readonly Dictionary<string, (Feature feature, bool enabled)> features = new();
    public IEnumerable<Feature> Features => features.Values.Select(t => t.feature);
    public Dictionary<string, (Feature feature, bool enabled)> FeaturesDict => features;
    public IEnumerable<Feature> EnabledFeatures => features.Values.Where(t => t.enabled).Select(t => t.feature).Concat(Owner != null ? Owner.Features : Enumerable.Empty<Feature>());
    public IEnumerable<Feature> SelfEnabledFeatures => features.Values.Where(t => t.enabled).Select(t => t.feature);

    private readonly Skill[] skills;
    public IEnumerable<Skill> Skills => skills;
    
    private readonly Dictionary<string, Item?> equipmentSlots;
    public IEnumerable<string> EquipmentSlots => equipmentSlots.Keys;

    public BodyPart? Parent { get; private set; }
    public string Path => Parent == null ? Name : Parent.Path + "/" + Name;
    public BodyPart Root => Parent == null ? this : Parent.Root;
    public bool IsRoot => Parent == null;
    
    public readonly IList<BodyPart> Children;
    public IEnumerable<BodyPart> InternalOrgans => Children.Where(child => child.IsInternal);
    public IEnumerable<BodyPart> OverlappingParts => Children.Where(child => child.OverlapsParent);
    public List<BodyPart> AllChildren
    {
        get
        {
            var children = new List<BodyPart>(Children);
            foreach (BodyPart child in Children)
            {
                children.AddRange(child.AllChildren);
            }
            return children;
        }
    }
    
    private readonly List<Injury> injuries;
    public IEnumerable<Injury> Injuries => injuries;
    public readonly float PainMultiplier = 1;
    public double Pain => injuries.Sum(injury => injury.Type.Pain * injury.Severity * PainMultiplier);
    public double MaxHealth { get; private set; }
    //This should only be used if the parent is alive
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
    public readonly Dictionary<string, BodyPartStat[]> Stats = new();
    public readonly Dictionary<DamageType, StatModifier[]> DamageModifiers = new();
    
    public IEnumerable<EquipmentProperty> CoveringEquipment => Body.GetCoveringEquipment(this);
    public IEnumerable<Item> Items => equipmentSlots.Values.Where(x => x != null)!;
    public Board? Board => Owner?.Board;

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
        get => !IsHard;
        set => IsHard = !value;
    }
    public bool HasBone
    {
        get => (flags & Flag.HasBone) != 0;
        set
        {
            if (value)
                flags |= Flag.HasBone;
            else
                flags &= (byte)~Flag.HasBone;
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


    public BodyPart(string name, string group, int maxHealth, float surfaceArea, float painMult, Skill[] actions, Feature[] features, string[] equipmentSlots, Injury[] conditions, byte flags, params BodyPart[] children)
    {
        Name = name;
        Group = group;
        MaxHealth = maxHealth;
        PainMultiplier = painMult;
        Children = new List<BodyPart>(children);
        skills = actions;
        ownerFeatures = features;
        Size = surfaceArea;
        this.flags = flags;
        this.equipmentSlots = new Dictionary<string, Item?>();
        foreach (string slot in equipmentSlots)
            this.equipmentSlots[slot] = null;
        injuries = new List<Injury>(conditions);

        foreach (BodyPart child in children)
            child.Parent = this;
    }
    public BodyPart(Stream stream, Body? body = null)
    {
        Name = stream.ReadString();
        Group = stream.ReadString();
        MaxHealth = stream.ReadDouble();
        Size = stream.ReadFloat();
        flags = (byte)stream.ReadByte();
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

        Children = new List<BodyPart>();
        int childCount = stream.ReadByte();
        for (int i = 0; i < childCount; i++)
        {
            Children.Add(new BodyPart(stream, body)
            {
                Parent = this
            });
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
            Stats[statName] = stats;
        }

        count = stream.ReadByte();
        ownerFeatures = new Feature[count];
        for (int i = 0; i < count; i++)
        {
            ownerFeatures[i] = Feature.FromBytes(stream);
        }

        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            bool enabled = stream.ReadByte() != 0;
            var feat = Feature.FromBytes(stream);
            features[feat.GetId()] = (feat, enabled);
        }

        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string id = stream.ReadString();
            uint size = stream.ReadUInt32();
            byte[] data = new byte[size];
            stream.ReadExactly(data);
            customData[id] = data;
        }
        Body = body;
    }

    public bool HasFlag(byte flag)
    {
        return (flags & flag) != 0;
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
        return Children.FirstOrDefault(child => child.Name.Equals(name));
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
        return GetChild(name) != null;
    }

    public void RemoveChild(string name)
    {
        BodyPart? child = GetChild(name);
        if (child == null) return;

        Body?.OnPartRemoved(child);
        child.Parent = null;
        child.Body = null;
        Children.Remove(child);

        OnChildRemoved?.Invoke(child);
    }
    public void AddChild(BodyPart child)
    {
        child.Parent?.RemoveChild(child.Name);
        
        Children.Add(child);
        child.Parent = this;
        child.Body = Body;
    
        Body?.OnPartAdded(child);

        OnChildAdded?.Invoke(child);
    }

    public void UpdateStatModifiers()
    {

        if (Owner == null)
            return;

        foreach (var entry in Stats)
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
        
        foreach (var entry in Stats)
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
        List<StatModifier> dmgMods = new();
        dmgMods.AddRange(DamageModifiers.GetValueOrDefault(type, Array.Empty<StatModifier>()));
        dmgMods.AddRange(IsSoft ? type.GetModifiersForSoft() : type.GetModifiersForHard());
        if (Owner != null)
        {
            foreach (Feature feat in Owner.Features.Concat(Features))
                dmgMods.AddRange(feat.ModifyReceivingDamageModifiers(this, source, amount));
        }

        double damage = Stat.ApplyModifiers(dmgMods, (float)amount);
        string formula = damage.ToString("0.##") + " após modificadores";
        if (Owner != null)
        {
            foreach (Feature feat in Owner.Features)
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
        
        Owner.Log($"{BBLink} recebeu [hint={formula}]{damage}[/hint] de dano {type.BBHint}.");
        
        if (damage <= 0)
            return 0;

        if (damage > Health/2 && HasBone)
        {
            double boneDmgChance = (damage - Health/2) / Health/2;
            if (RpgMath.RandomFloat(0, 1) < boneDmgChance)
            {
                double boneDmg = damage/2;
                damage -= boneDmg;
                AddInjury(new Injury(type.OnHard, boneDmg));
            }
        }
        
        AddInjury(new Injury(IsSoft ? type.OnSoft : type.OnHard, damage));
        
        return damage;
    }

    public void AddInjury(Injury condition)
    {
        injuries.Add(condition);
        OnInjuryAdded?.Invoke(condition);
        Body?._invokeInjuryEvent(this, condition, true);

        if (Health <= 0)
            Body?.OnPartDie(this);

        if (!SidedLogic.Instance.IsClient())
        {
            UpdateStatModifiers();
            foreach (var feat in SelfEnabledFeatures.Concat(Owner?.EnabledFeatures ?? []))
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
                Body?._invokeInjuryEvent(this, condition, false);
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
        ((IFeatureSource)this).AddFeature(feature);
        OnFeatureAdded?.Invoke(feature);
    }

    public Feature? RemoveFeature(string id)
    {
        var f = ((IFeatureSource)this).RemoveFeature(id);
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
    
    public byte[]? GetCustomData(string id)
    {
        return customData.GetValueOrDefault(id);
    }

    public void SetCustomData(string id, byte[]? data)
    {
        if (data == null)
            customData.Remove(id);
        else
            customData[id] = data;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteString(Group);
        stream.WriteDouble(MaxHealth);
        stream.WriteFloat(Size);
        stream.WriteByte(flags);
        
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
        stream.WriteByte((byte)Children.Count);
        foreach (BodyPart child in Children)
        {
            child.ToBytes(stream);
        }

        stream.WriteByte((byte)Stats.Count);
        foreach (var entry in Stats)
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
        
        stream.WriteByte((byte)features.Count);
        foreach (var tuple in features.Values)
        {
            stream.WriteByte((byte)(tuple.enabled ? 1 : 0));
            tuple.feature.ToBytes(stream);
        }
        
        stream.WriteByte((byte)customData.Count);
        foreach (var pair in customData)
        {
            stream.WriteString(pair.Key);
            stream.WriteUInt32((uint)pair.Value.Length);
            stream.Write(pair.Value);
        }
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

        if (flags != Flag.None)
        {
            var flagsArray = new JsonArray();
            if (HasBone)
                flagsArray.Add("HasBone");
            if (IsHard)
                flagsArray.Add("IsHard");
            if (IsInternal)
                flagsArray.Add("Internal");
            if (OverlapsParent)
                flagsArray.Add("Overlaps");
            json["flags"] = flagsArray;
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

        if (Children.Count > 0)
        {
            var childrenArray = new JsonArray();
            foreach (var child in Children)
            {
                childrenArray.Add(child.ToJson());
            }
            json["children"] = childrenArray;
        }

        if (Stats.Count > 0)
        {
            var statsArray = new JsonArray();
            foreach (var entry in Stats)
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

        for (int i = 0; i < Children.Count; i++)
            sb.Append(Children[i].PrintPretty(indent, i == Children.Count - 1));

        return sb.ToString();
    }

    public override string ToString()
    {
        return "BodyPart[" + Name + "]";
    }

    public void ClearEvents()
    {
        OnChildAdded = null;
        OnChildRemoved = null;
        OnInjuryAdded = null;
        OnInjuryRemoved = null;
    }

    public static BodyPart? NewBody(JsonObject json)
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        try
        {
            var builder = new Builder(json["name"].ToString());

            // Required fields
            builder.Health((int)json["maxHealth"].GetValue<float>());
            float area = json["area"].GetValue<float>();
            builder.Size(area * 100f); // Convert from fraction to percentage

            // Optional group
            if (json.TryGetPropertyValue("group", out JsonNode? groupNode))
                builder.Group(groupNode.ToString());

            // Equipment slots
            if (json.TryGetPropertyValue("slots", out JsonNode? slotsNode))
            {
                string[] slots = slotsNode.AsArray().Select(s => s.ToString()).ToArray();
                builder.Equipment(slots);
            }
            
            // Stats (Modifiers)
            if (json.TryGetPropertyValue("stats", out JsonNode? statsNode))
            {
                foreach (JsonNode? statJson in statsNode.AsArray())
                {
                    if (statJson is JsonObject statObject)
                    {
                        if (statObject.TryGetPropertyValue("stat", out JsonNode? statNameNode) &&
                            statObject.TryGetPropertyValue("atFull", out JsonNode? atFullNode))
                        {
                            float zero = 0;
                            bool sho = false;
                            StatModifierType op = StatModifierType.Flat;
                            if (statObject.TryGetPropertyValue("atZero", out JsonNode? atZeroNode))
                                zero = atZeroNode.GetValue<float>();
                            if (statObject.TryGetPropertyValue("standaloneHPOnly", out JsonNode? standaloneHPOnlyNode))
                                sho = standaloneHPOnlyNode.GetValue<bool>();
                            if (statObject.TryGetPropertyValue("operation", out JsonNode? operationNode) &&
                                Enum.TryParse<StatModifierType>(operationNode.ToString(), true, out var operation))
                                op = operation;

                            builder.Stat(statNameNode.GetValue<string>(), atFullNode.GetValue<float>(), zero, op, sho);
                        }
                        else
                        {
                            Console.WriteLine("Warning: Invalid stat modifier in JSON: " + statJson);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: Expected object for stat modifier, got: " + statJson);
                    }
                }
            }
            
            //Damage Modifiers
            if (json.TryGetPropertyValue("damageModifiers", out JsonNode? damageModsNode))
            {
                foreach (JsonNode? modJson in damageModsNode.AsArray())
                {
                    if (modJson is JsonObject modObject)
                    {
                        DamageType dt = DamageType.FromName(modObject["damageType"].GetValue<string>());
                        if (dt == null)
                        {
                            Console.WriteLine("Warning: Invalid damage type: " + modObject["damageType"].GetValue<string>());
                            continue;
                        }
                        float value = modObject["value"].GetValue<float>();
                        StatModifierType operation = StatModifierType.Percent;
                        if (modObject.ContainsKey("operation"))
                            operation = Enum.Parse<StatModifierType>(modObject["operation"].GetValue<string>());
                        builder.DamageModifier(dt, value, operation);
                    }
                    else
                    {
                        Console.WriteLine("Warning: Invalid damage modifier in JSON: " + modJson);
                    }
                }
            }

            // Flags
            if (json.TryGetPropertyValue("flags", out JsonNode? flagsNode))
            {
                List<byte> flagBytes = new();
                foreach (JsonNode? flag in flagsNode.AsArray())
                {
                    switch (flag.ToString())
                    {
                        case "HasBone": flagBytes.Add(Flag.HasBone); break;
                        case "Hard": flagBytes.Add(Flag.Hard); break;
                        case "Internal": flagBytes.Add(Flag.Internal); break;
                        case "Overlaps": flagBytes.Add(Flag.Overlaps); break;
                    }
                }

                builder.Flags(flagBytes.ToArray());
            }

            if (json.TryGetPropertyValue("skills", out JsonNode? skillsNode))
            {
                List<Skill> skills = new();
                foreach (JsonNode? skillJson in skillsNode.AsArray())
                {
                    string skillName = skillJson.GetValue<string>();
                    var skill = Compendium.GetEntryObject<Skill>(skillName);
                    if (skill != null)
                        skills.Add(skill);
                    else
                        Console.WriteLine("Warning: Invalid skill name in BodyPart JSON: " + skillJson);
                }
                builder.Skills(skills.ToArray());
            }

            if (json.TryGetPropertyValue("selfFeatures", out JsonNode? featuresNode))
            {
                List<Feature> features = new();
                foreach (JsonNode? featureJson in featuresNode.AsArray())
                {
                    string name = featureJson.GetValue<string>();
                    Feature? feature = Compendium.GetEntryObject<Feature>(name);
                    if (feature == null)
                        Console.WriteLine("Warning: Invalid feature in JSON: " + name);
                    else
                        features.Add(feature);
                }
                builder.Features(features.ToArray());
            }

            if (json.TryGetPropertyValue("features", out JsonNode? creatureFeaturesNode))
            {
                foreach (JsonNode? featureJson in creatureFeaturesNode.AsArray())
                {
                    string name = featureJson.GetValue<string>();
                    Feature? feature = Compendium.GetEntryObject<Feature>(name);
                    if (feature == null)
                        Console.WriteLine("Warning: Invalid creature feature in JSON: " + name);
                    else
                        builder.OwnerFeatures(feature);
                }
            }

            // Recursive children
            if (json.TryGetPropertyValue("children", out JsonNode? childrenNode))
            {
                foreach (JsonNode? childJson in childrenNode.AsArray())
                {
                    BodyPart? child = NewBody(childJson.AsObject());
                    if (child == null)
                        throw new JsonException("Invalid child: " + childJson);
                    builder.Child(child);
                }
            }

            var ret = builder.Build();
            //CustomData
            if (json.TryGetPropertyValue("metadata", out JsonNode? metadataNode))
                ret.CustomDataFromJson(metadataNode.AsObject());
            return ret;
        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        catch (JsonException e)
        {
            Console.WriteLine("Error reading json body: " + json);
            Console.WriteLine(e);
        }
        catch (NullReferenceException e)
        {
            Console.WriteLine("Error reading json body: " + json);
            Console.WriteLine(e);
        }

        return null;
    }
         
    public static string BuildPath(params string[] names)
    {
        var result = new StringBuilder();
        foreach (string part in names)
        {
            result.Append(part);
            result.Append('.');
        }
        return result.ToString(0, result.Length-1);
    }

    public float GetStat(string id, float defaultValue = 0)
    {
        if (!Stats.TryGetValue(id, out var stats)) return defaultValue;

        return stats.Where(stat => stat.op == StatModifierType.Flat).Sum(stat => stat.CalculateFor(this).Value);
    }

    public static class Groups
    {
        public const string UPPER_LEFT = "upper-left";
        public const string UPPER_RIGHT = "upper-right";
        public const string LOWER_LEFT = "lower-left";
        public const string LOWER_RIGHT = "lower-right";
    }

    public class Builder(string name)
    {
        private string name = name;
        private string group = "default";
        private int maxHealth = 1;
        private float painMult;
        private float sizePercentage;
        private Skill[] skills = Array.Empty<Skill>();
        private byte flags;
        private readonly List<BodyPart> children = new();
        private readonly List<string> equipmentSlots = [];
        private readonly List<Injury> injuries = [];
        private readonly List<Feature> features = [];
        private readonly List<Feature> selfFeatures = [];
        private readonly Dictionary<string, List<BodyPartStat>> stats = new();
        private readonly Dictionary<DamageType, List<StatModifier>> damageMods = new();

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

        public Builder Features(params Feature[] features)
        {
            selfFeatures.AddRange(features);
            return this;
        }
        public Builder OwnerFeatures(params Feature[] features)
        {
            this.features.AddRange(features);
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

        public Builder Hard()
        {
            return Flags(Flag.Hard);
        }
        public Builder Overlaps()
        {
            return Flags(Flag.Overlaps);
        }
        public Builder HasBone()
        {
            return Flags(Flag.HasBone);
        }
        public Builder Internal()
        {
            return Flags(Flag.Internal);
        }

        public Builder Pain(float mult)
        {
            painMult = mult;
            return this;
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

        public Builder Children(params Builder[] newChildren)
        {
            return Children(newChildren.Select(c => c.Build()).ToArray());
        }

        public Builder Group(string group)
        {
            this.group = group;
            return this;
        }

        public Builder Stat(string stat, float atFull, float atZero = 0, StatModifierType operation = StatModifierType.Flat, bool standaloneHPOnly = false, bool applyToOwner = true)
        {
            if (!stats.ContainsKey(stat))
                stats[stat] = new List<BodyPartStat>();
            stats[stat].Add(new BodyPartStat(atFull, atZero, operation, standaloneHPOnly, applyToOwner));
            return this;
        }

        public Builder DamageModifier(DamageType type, float value, StatModifierType operation = StatModifierType.Percent)
        {
            if (!damageMods.ContainsKey(type))
                damageMods[type] = new();
            damageMods[type].Add(new StatModifier($"{name}-{type.Name}-mod", value, operation));
            return this;
        }

        public BodyPart Build()
        {
            var ret = new BodyPart(name, group, maxHealth, sizePercentage, painMult, skills, features.ToArray(), equipmentSlots.ToArray(), injuries.ToArray(), flags, children.ToArray());
            foreach (var entry in stats)
                ret.Stats[entry.Key] = entry.Value.ToArray();
            foreach (var feat in selfFeatures)
                ret.AddFeature(feat);
            return ret;
        }
    }

}

public class BodyPartRef(CreatureRef owner, string path) : ISerializable
{
    public CreatureRef Owner = owner;
    public readonly string Path = path;
    public BodyPart? BodyPart => field ??= Owner.Creature?.GetBodyPart(Path);

    public BodyPartRef(BodyPart part) : this(new CreatureRef(part.Owner), part.Path)
    {
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
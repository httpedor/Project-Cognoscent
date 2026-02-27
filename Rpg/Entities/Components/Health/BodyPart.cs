using System.Text;
using Rpg.Entities.Components.Inventory;
using Rpg.Entities.Interfaces;
using Rpg.Features;
using Rpg.Health;
using Rpg.Skills;

namespace Rpg.Entities.Components.Health;

public class BodyPartEvent(BodyPart bodyPart) : ComponentEvent(bodyPart)
{
    public BodyPart Part = bodyPart;
}
public class BodyPartInjuryEvent(BodyPart bodyPart, Injury injury) : BodyPartEvent(bodyPart)
{
    public Injury Injury = injury;
}
public class BodyPartInjuryAddedEvent(BodyPart bodyPart, Injury injury) : BodyPartInjuryEvent(bodyPart, injury)
{
}
public class BodyPartInjuryRemovedEvent(BodyPart bodyPart, Injury injury) : BodyPartInjuryEvent(bodyPart, injury) 
{
}
public class BodyPartInjuryChangedEvent(BodyPart bodyPart, Injury oldInjury, Injury newInjury) : BodyPartInjuryEvent(bodyPart, oldInjury) 
{
    public Injury OldInjury = oldInjury;
    public Injury NewInjury = newInjury;
}
public class BodyPartChildAddedEvent(BodyPart bodyPart, BodyPart child) : BodyPartEvent(bodyPart)
{
    public BodyPart Child = child;
}
public class BodyPartChildRemovedEvent(BodyPart bodyPart, BodyPart child) : BodyPartEvent(bodyPart)
{
    public BodyPart Child = child;
}
public class BodyPartDiedEvent(BodyPart bodyPart) : BodyPartEvent(bodyPart)
{
}
public partial class BodyPart : Component, ISerializable, IDamageable, ITaggable, IItemHolder, ISkillProvider
{
    public readonly struct BodyPartStat(float atFull, float atZero, StatModifierType op, bool sho, bool ato)
        : ISerializable
    {
        public readonly float atFull = atFull;
        public readonly float atZero = atZero;
        public readonly StatModifierType op = op;
        public readonly bool standaloneHealthOnly = sho;
        public readonly bool appliesToOwner = ato;

        public BodyPartStat(Stream stream) : this(stream.ReadFloat(), stream.ReadFloat(), (StatModifierType)stream.ReadByte(), stream.ReadByte() == 1, stream.ReadByte() == 1)
        {
        }

        public void ToBytes(Stream stream)
        {
            stream.WriteFloat(atFull);
            stream.WriteFloat(atZero);
            stream.WriteByte((byte)op);
            stream.WriteByte((byte)(standaloneHealthOnly ? 1 : 0));
            stream.WriteByte((byte)(appliesToOwner ? 1 : 0));
        }

        public StatModifier CalculateFor(BodyPart target, string? name = null)
        {
            name ??= target.Entity.Id + "_mod";
            double hpPercentage;
            if (standaloneHealthOnly)
                hpPercentage = target.HealthStandalone/target.MaxHealth;
            else
                hpPercentage = target.Health/target.MaxHealth;

            return new StatModifier(name, RpgMath.Lerp(atZero, atFull, (float)hpPercentage), op);
        }
    }

    [OptionalComponent(typeof(FeaturesContainer))]
    private FeaturesContainer? featuresComponent;
    [RequiredComponent(typeof(StatsContainer))]
    private StatsContainer statsComponent;

    public string Name => Entity.Name;
    /// <summary>
    /// BBCode link to this body part
    /// </summary>
    public string BBLink => Name + (OwnerEntity != null ? " de " + OwnerEntity.BBLink : "");
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
    /// The entity that owns this body part.
    /// Shorthand for Body?.Entity
    /// </summary>
    public Entity? OwnerEntity => Body?.Entity;
    private readonly Feature[] providedFeatures;
    /// <summary>
    /// Features provided to the owner by this body part
    /// </summary>
    public IEnumerable<Feature> ProvidedFeatures => providedFeatures;

    private readonly Skill[] providedSkills;
    /// <summary>
    /// Skills provided by this body part
    /// </summary>
    public IEnumerable<Skill> ProvidedSkills => providedSkills;
    
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
    /// The root body part of this body.
    /// </summary>
    public BodyPart Root => Parent == null ? this : Parent.Root;
    public bool IsRoot => Parent == null;

    private readonly Dictionary<int, BodyPart> children;
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
    public double Pain => statsComponent.GetStatValue(StatIds.Pain);
    public double MaxHealth => statsComponent.GetStatValue(StatIds.MaxHealth);
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
                if (injury.Type.Instakill.Eval(OwnerEntity, Entity, Entity))
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
    /// Tags associated with this body part.
    /// </summary>
    private HashSet<string> tags = new();
    HashSet<string> ITaggable.Tags
    {
        get => tags;
        set => tags = value;
    }
    
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

    public bool IsInternal => this.Is(BodyTags.Internal);
    public bool IsHard => this.Is(BodyTags.Hard);
    public bool IsSoft => !this.Is(BodyTags.Hard);


    public BodyPart(string group, Skill[] skills, Feature[] providedFeatures, string[] equipmentSlots, Injury[] conditions, string[] tags, params BodyPart[] children)
    {
        Group = group;
        this.children = new(children.Length);
        providedSkills = skills;
        this.providedFeatures = providedFeatures;
        this.equipmentSlots = new Dictionary<string, Item?>();
        foreach (string slot in equipmentSlots)
            this.equipmentSlots[slot] = null;
        injuries = [.. conditions];
        this.tags = [.. tags];

        foreach (BodyPart child in children)
        {
            if (this.children.ContainsKey(child.Entity.Id))
                throw new ArgumentException($"Duplicate child name '{child.Name}' when creating BodyPart '{Name}'.");
            this.children[child.Entity.Id] = child;
            child.Parent = this;
        }
    }
    public BodyPart(Stream stream)
    {
        Group = stream.ReadString();

        equipmentSlots = new Dictionary<string, Item?>();
        injuries = new List<Injury>();

        byte slotCount = (byte)stream.ReadByte();        
        for (int i = 0; i < slotCount; i++)
        {
            //TODO: The items should be referenced by ID, not fully loaded here
            string slot = stream.ReadString();
            if (stream.ReadBoolean())
                LookForComponent<Item>("equipmentSlots", stream.ReadInt32());
            else
                equipmentSlots[slot] = null;
        }

        byte injuryCount = (byte)stream.ReadByte();
        for (int i = 0; i < injuryCount; i++)
            injuries.Add(new Injury(stream));

        byte skillCount = (byte)stream.ReadByte();
        providedSkills = new Skill[skillCount];
        for (int i = 0; i < skillCount; i++)
        {
            providedSkills[i] = Skill.FromBytes(stream);
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
        providedFeatures = new Feature[count];
        for (int i = 0; i < count; i++)
        {
            providedFeatures[i] = Feature.FromBytes(stream);
        }
        this.LoadTags(stream);

        children = new Dictionary<int, BodyPart>();
        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            int childId = stream.ReadInt32();
            LookForComponent<BodyPart>("children", childId);
        }
    }
    protected override void OnFoundComponents(string group, List<Component> components)
    {
        if (group == "children")
        {
            foreach (var comp in components)
            {
                if (comp is BodyPart part)
                {
                    children[part.Entity.Id] = part;
                    part.Parent = this;
                }
            }
        }
        else if (group == "equipmentSlots")
        {
            foreach (var comp in components)
            {
                if (comp is Item item)
                {
                    equipmentSlots[item.GetProperty<EquipmentProperty>()?.Slot ?? EquipmentSlot.Hold] = item;
                }
            }
        }
    }
    public override void OnInit(Entity entity)
    {
        base.OnInit(entity);
        statsComponent.CreateStatIfNotExists(new Stat(StatIds.MaxHealth, 10));
        statsComponent.CreateStatIfNotExists(new Stat(StatIds.Pain, 0));
    }

    public bool CanEquipSlot(string slot)
    {
        return equipmentSlots.ContainsKey(slot) && equipmentSlots[slot] == null;
    }

    public Item? GetEquippedItem(string slot)
    {
        return equipmentSlots.GetValueOrDefault(slot);
    }

    public BodyPart? GetChild(int id)
    {
        if (children.TryGetValue(id, out var child))
            return child;
        return null;
    }

    public bool HasChild(int id)
    {
        return children.ContainsKey(id);
    }

    public void RemoveChild(int id)
    {
        BodyPart? child = GetChild(id);
        if (child == null) return;

        Body?.UnindexPart(child);
        child.Parent = null;
        child.Body = null;
        children.Remove(child.Entity.Id);

        Entity.DispatchEvent(new BodyPartChildRemovedEvent(this, child));
    }
    public void AddChild(BodyPart child)
    {
        // Remove from previous parent if any
        child.Parent?.RemoveChild(child.Entity.Id);

        // If we already have a child with the same name, remove it first to keep uniqueness
        if (children.ContainsKey(child.Entity.Id))
            RemoveChild(child.Entity.Id);

        children[child.Entity.Id] = child;
        child.Parent = this;
        child.Body = Body;

        Body?.IndexPart(child);

        Entity.DispatchEvent(new BodyPartChildAddedEvent(this, child));
    }

    public void UpdateStatModifiers()
    {

        if (OwnerEntity == null || OwnerEntity.TryGetComponent<StatsContainer>(out var ownerStats) == false)
            return;

        foreach (var entry in ProvidedStats)
        {
            var stat = ownerStats.GetStat(entry.Key);
            if (stat == null)
                continue;
            int i = 0;
            foreach (BodyPartStat mod in entry.Value)
            {
                if (!mod.appliesToOwner) continue;
                stat.SetModifier(mod.CalculateFor(this, Entity.Id + "_mod" + i));
                i++;
            }
        }
    }

    public void RemoveStatModifiers()
    {
        if (OwnerEntity == null || OwnerEntity.TryGetComponent<StatsContainer>(out var ownerStats) == false)
            return;
        
        foreach (var entry in ProvidedStats)
        {
            var stat = ownerStats.GetStat(entry.Key);
            if (stat == null)
                continue;
            int i = 0;
            foreach (BodyPartStat mod in entry.Value)
            {
                if (!mod.appliesToOwner) continue;
                stat.RemoveModifier(Entity.Id + "_mod" + i);
                i++;
            }
        }
    }

    public double Damage(DamageInstance damageInstance)
    {
        DamageEvent ev = new(this, damageInstance);
        ev.DamageModifiers = [.. DamageModifiers.GetValueOrDefault(damageInstance.Source.Type, Array.Empty<StatModifier>())];
        Entity.DispatchEvent(ev);
        if (OwnerEntity != null)
        {
            OwnerEntity.DispatchEvent(ev);
        }

        var source = damageInstance.Source;
        var amount = damageInstance.Amount;

        double damage = Stat.ApplyModifiers(ev.DamageModifiers, (float)amount);
        string formula = damage.ToString("0.##") + " após modificadores"; //TODO: Mostrar os modificadores também na fórmula
        formula += ev.Formula;
        OwnerEntity?.Log($"{BBLink} recebeu [hint={formula}]{damage}[/hint] de dano {damageInstance.Source.Type.BBHint}.");
        
        if (damage <= 0)
            return 0;

        //TODO: Figure out damage overflow to parents and internals
        AddInjury(source.Type.InjuryResolver(new DamageInstance(source, (float)damage), this));
        
        return damage;
    }

    public void AddInjury(Injury condition)
    {
        injuries.Add(condition);
        var ev = new BodyPartInjuryAddedEvent(this, condition);
        Entity.DispatchEvent(ev);
        OwnerEntity?.DispatchEvent(ev);

        if (Health <= 0)
        {
            Entity.DispatchEvent(new BodyPartDiedEvent(this));
            OwnerEntity?.DispatchEvent(new BodyPartDiedEvent(this));
        }
    }

    public void ChangeInjury(Injury oldCondition, Injury newCondition)
    {
        for (int i = 0; i < injuries.Count; i++)
        {
            if (injuries[i].Equals(oldCondition))
            {
                injuries[i] = newCondition;
                Entity.DispatchEvent(new BodyPartInjuryChangedEvent(this, oldCondition, newCondition));
                OwnerEntity?.DispatchEvent(new BodyPartInjuryChangedEvent(this, oldCondition, newCondition));
                return;
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
                Entity.DispatchEvent(new BodyPartInjuryRemovedEvent(this, condition));
                OwnerEntity?.DispatchEvent(new BodyPartInjuryRemovedEvent(this, condition));
                return;
            }
        }
    }

    public void RemoveInjury(int index)
    {
        Injury condition = injuries[index];
        injuries.RemoveAt(index);
        Entity.DispatchEvent(new BodyPartInjuryRemovedEvent(this, condition));
        OwnerEntity?.DispatchEvent(new BodyPartInjuryRemovedEvent(this, condition));
    }

    public bool HasItem(Item item)
    {
        return Items.Contains(item);
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
        if (equipmentSlots.TryGetValue(slot, out Item? equipped))
            return;

        item.Holder?.RemoveItem(item);
        equipmentSlots[slot] = item;

        if (slot == EquipmentSlot.Hold)
        {
            var ev = new ItemHeldEvent(item, this);
            Entity.DispatchEvent(ev);
            OwnerEntity?.DispatchEvent(ev);
        }
        else
        {
            var ep = item.GetProperty<EquipmentProperty>();
            if (ep == null || slot != ep.Slot) return;
            
            ep.OnEquip(this);
            var ev = new ItemEquippedEvent(item, this);
            Entity.DispatchEvent(ev);
            OwnerEntity?.DispatchEvent(ev);
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
            var ev = new ItemUnheldEvent(item, this);
            Entity.DispatchEvent(ev);
            OwnerEntity?.DispatchEvent(ev);
        }
        else
        {
            var ep = item.GetProperty<EquipmentProperty>();
            if (ep == null || slot != ep.Slot) return;
            
            ep.OnUnequip();
            var ev = new ItemUnequippedEvent(item, this);
            Entity.DispatchEvent(ev);
            OwnerEntity?.DispatchEvent(ev);
        }
    }
    
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Group);
        
        stream.WriteByte((byte)equipmentSlots.Count);
        foreach (var slot in equipmentSlots)
        {
            stream.WriteString(slot.Key);
            if (slot.Value != null)
            {
                stream.WriteBoolean(true);
                stream.WriteInt32(slot.Value.Entity.Id);
            }
            else
                stream.WriteBoolean(false);
        }

        stream.WriteByte((byte)injuries.Count);
        foreach (Injury condition in injuries)
        {
            condition.ToBytes(stream);
        }

        stream.WriteByte((byte)providedSkills.Length);
        foreach (Skill action in ProvidedSkills)
        {
            action.ToBytes(stream);
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
        
        stream.WriteByte((byte)providedFeatures.Length);
        foreach (var ownerFeature in providedFeatures)
        {
            ownerFeature.ToBytes(stream);
        }

        this.SaveTags(stream);

        stream.WriteByte((byte)children.Count);
        foreach (var child in children.Values)
        {
            stream.WriteInt32(child.Entity.Id);
        }
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

    public IEnumerable<Skill> GetSkillsFor(SkillExecutor executor)
    {
        return providedSkills;
    }
}
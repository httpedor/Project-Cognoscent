using System.Numerics;
using System.Text;
using Rpg.Entities.Components.Inventory;
using Rpg.Entities.Interfaces;
using Rpg.Features;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg.Entities.Components.Health;

public class BodyPartEvent(BodyPart bodyPart) : ComponentEvent(bodyPart)
{
    public BodyPart Part = bodyPart;
}
public class BodyLayerEvent(BodyLayer bodyLayer) : BodyPartEvent(bodyLayer.Part)
{
    public BodyLayer Layer = bodyLayer;
}
public class BodyLayerInjuryEvent(BodyLayer layer, Injury injury) : BodyLayerEvent(layer)
{
    public Injury Injury = injury;
}
public class BodyLayerInjuryAddedEvent(BodyLayer layer, Injury injury) : BodyLayerInjuryEvent(layer, injury) {}
public class BodyLayerInjuryRemovedEvent(BodyLayer layer, Injury injury) : BodyLayerInjuryEvent(layer, injury) {}
public class BodyLayerInjuryChangedEvent(BodyLayer layer, Injury oldInjury, Injury newInjury) : BodyLayerInjuryEvent(layer, oldInjury) 
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
public class BodyPartDiedEvent(BodyPart bodyPart) : BodyPartEvent(bodyPart) {}
public class BodyLayerDiedEvent(BodyLayer layer) : BodyLayerEvent(layer) {}

public readonly struct BodyProvidedStat(float atFull, float atZero, StatModifierType op, bool sho, bool local)
    : ISerializable
{
    public readonly float atFull = atFull;
    public readonly float atZero = atZero;
    public readonly StatModifierType op = op;
    public readonly bool standaloneHealthOnly = sho;
    public readonly bool isLocal = local;

    public BodyProvidedStat(Stream stream) : this(stream.ReadFloat(), stream.ReadFloat(), (StatModifierType)stream.ReadByte(), stream.ReadByte() == 1, stream.ReadByte() == 1)
    {
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteFloat(atFull);
        stream.WriteFloat(atZero);
        stream.WriteByte((byte)op);
        stream.WriteByte((byte)(standaloneHealthOnly ? 1 : 0));
        stream.WriteByte((byte)(isLocal ? 1 : 0));
    }

    private StatModifier CalculateFor(double hpPercentage, string id, string displayName, string? name = null)
    {
        name ??= id + "_mod";
        return new StatModifier(name, RpgMath.Lerp(atZero, atFull, (float)hpPercentage), op)
        {
            DisplayName = displayName
        };

    }
    public StatModifier CalculateFor(BodyLayer target, string? name = null)
    {
        name ??= target.Entity.Id + "_mod";
        double hpPercentage;
        if (standaloneHealthOnly)
            hpPercentage = target.HealthStandalone/target.MaxHealth;
        else
            hpPercentage = target.Health/target.MaxHealth;
        return CalculateFor(hpPercentage, name, target.Part.Name + " " + target.Name);
    }
    public StatModifier CalculateFor(BodyPart target, string? name = null)
    {
        name ??= target.Entity.Id + "_mod";
        double hpPercentage;
        if (standaloneHealthOnly)
            hpPercentage = target.HealthStandalone/target.MaxHealth;
        else
            hpPercentage = target.Health/target.MaxHealth;
        return CalculateFor(hpPercentage, name, target.Name);
    }
}
public class BodyLayer : ISerializable, IDamageable
{
    public BodyPart Part;
    public Entity Entity => Part.Entity;
    public Board Board => Entity.Board;
    public double HealthStandalone => MaxHealth - Injuries.Sum(injury => injury.Severity);
    public double Health {
        get {
            if (Part.Parent == null || Part.Parent.IsAlive) return HealthStandalone;
            return 0;
        }
    }
    public string Name = "";
    /// <summary>
    /// How much blood flows through this layer, affecting bleeding and healing
    /// </summary>
    public float BloodFlow = 0;
    /// <summary>
    /// The maximum health of this layer.
    /// </summary>
    public float MaxHealth = 10;
    /// <summary>
    /// Damage modifiers applied when this layer takes damage of a certain type. This can be used to simulate armor, skin, muscle, etc.
    /// </summary>
    public Dictionary<Expr<bool>, StatModifier> DamageModifiers = new();
    /// <summary>
    /// How much damage this layer can absorb in a single hit before the damage starts overflowing to the next layer.
    /// </summary>
    public Dictionary<Expr<bool>, Expr<float>> PenetrationResistance = new();
    /// <summary>
    /// How much an injury's severity is reduced per second due to natural healing.
    /// </summary>
    public Expr<float> RegenerationRate = new ConstNumberExpr(0);
    /// <summary>
    /// Executed when an injury is naturally healed, allowing for some injuries to leave scars or other consequences even after they are fully healed.
    /// </summary>
    public EffectExpr OnHeal = new NoEffectExpr();
    /// <summary>
    /// How much pain an injury on this layer causes, as a multiplier. This can be used to simulate that injuries to certain layers are more painful than others.
    /// </summary>
    public Expr<float> PainMultiplier = new ConstNumberExpr(1);
    /// <summary>
    /// Whether this layer getting to 0 health instantly disables the body part. Simulating things like motor function loss 
    /// </summary>
    public Expr<bool> KillsOnZeroHealth = new ConstConditionExpr(false);
    
    /// <summary>
    /// Whether this layer will be bypassed by this specific damage instance, simulating things like "chance of bullet going through skin but not hitting the bone". This is checked before penetration resistance, so if this returns true the damage will directly hit the next layer without being reduced by penetration resistance or modifiers.
    /// </summary>
    public Expr<bool> BypassLayer = new ConstConditionExpr(false);
    /// <summary>
    /// How much of the body part's surface area this layer covers. This is used to calculate how likely it is for an attack to hit this layer, and is used to calculate bypass chance. This is a value from 0 to 1, where 1 means the layer covers the entire body part and 0 means it doesn't cover any of it.
    /// </summary>
    public float SurfaceArea = 1;
    /// <summary>
    /// What injuries this layer currently has.
    /// </summary>
    public List<Injury> Injuries = new();
    /// <summary>
    /// What stats are provided by this body part. Dynamically updated on HP(100% to 0%)
    /// </summary>
    public readonly Dictionary<string, BodyProvidedStat[]> ProvidedStats = new();
    public BodyLayer(BodyPart part)
    {
        Part = part;
    }
    public BodyLayer(Stream stream, BodyPart part) : this(part)
    {
        Name = stream.ReadString();
        BloodFlow = stream.ReadFloat();
        MaxHealth = stream.ReadFloat();

        byte damageModCount = (byte)stream.ReadByte();
        for (int i = 0; i < damageModCount; i++)
        {
            var condition = BaseExpr.Deserialize<Expr<bool>>(stream);
            var mod = new StatModifier(stream);
            DamageModifiers[condition] = mod;
        }

        byte penResCount = (byte)stream.ReadByte();
        for (int i = 0; i < penResCount; i++)
        {
            var condition = BaseExpr.Deserialize<Expr<bool>>(stream);
            var res = BaseExpr.Deserialize<Expr<float>>(stream);
            PenetrationResistance[condition] = res;
        }

        RegenerationRate = BaseExpr.Deserialize<Expr<float>>(stream);
        OnHeal = BaseExpr.Deserialize<EffectExpr>(stream);
        PainMultiplier = BaseExpr.Deserialize<Expr<float>>(stream);
        KillsOnZeroHealth = BaseExpr.Deserialize<Expr<bool>>(stream);
        BypassLayer = BaseExpr.Deserialize<Expr<bool>>(stream);

        byte injuryCount = (byte)stream.ReadByte();
        for (int i = 0; i < injuryCount; i++)
            Injuries.Add(new Injury(stream));
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteFloat(BloodFlow);
        stream.WriteFloat(MaxHealth);

        stream.WriteByte((byte)DamageModifiers.Count);
        foreach (var pair in DamageModifiers)
        {
            pair.Key.ToBytes(stream);
            pair.Value.ToBytes(stream);
        }

        stream.WriteByte((byte)PenetrationResistance.Count);
        foreach (var pair in PenetrationResistance)
        {
            pair.Key.ToBytes(stream);
            pair.Value.ToBytes(stream);
        }

        RegenerationRate.ToBytes(stream);
        OnHeal.ToBytes(stream);
        PainMultiplier.ToBytes(stream);
        KillsOnZeroHealth.ToBytes(stream);
        BypassLayer.ToBytes(stream);

        stream.WriteByte((byte)Injuries.Count);
        foreach (Injury injury in Injuries)
            injury.ToBytes(stream);
    }
    public void AddInjury(Injury condition)
    {
        var oldHealth = Health;
        Injuries.Add(condition);
        var ev = new BodyLayerInjuryAddedEvent(this, condition);
        DispatchEvent(ev);

        if (Health <= 0 && oldHealth > 0)
            DispatchEvent(new BodyLayerDiedEvent(this));
    }
    public void ChangeInjury(Injury oldCondition, Injury newCondition)
    {
        for (int i = 0; i < Injuries.Count; i++)
        {
            if (Injuries[i].Equals(oldCondition))
            {
                Injuries[i] = newCondition;
                DispatchEvent(new BodyLayerInjuryChangedEvent(this, oldCondition, newCondition));
                return;
            }
        }
    }

    public void RemoveInjury(Injury condition)
    {
        for (int i = Injuries.Count - 1; i >= 0; i--)
        {
            if (Injuries[i].Equals(condition))
            {
                Injuries.RemoveAt(i);
                DispatchEvent(new BodyLayerInjuryRemovedEvent(this, condition));
                return;
            }
        }
    }

    public void RemoveInjury(int index)
    {
        Injury condition = Injuries[index];
        Injuries.RemoveAt(index);
        DispatchEvent(new BodyLayerInjuryRemovedEvent(this, condition));
    }
    public double Damage(DamageInstance damageInstance)
    {
        throw new NotImplementedException();
    }

    private void DispatchEvent(BodyLayerEvent ble)
    {
        Part.Entity.DispatchEvent(ble);
        Part.OwnerEntity?.DispatchEvent(ble);
    }
}

[RegisterComponent]
public partial class BodyPart : Component, ISerializable, IDamageable, ITaggable, IItemHolder, ISkillProvider,
    ComponentEventHandler<BodyLayerDiedEvent>
{

    [OptionalComponent(typeof(FeaturesContainer))]
    private FeaturesContainer? featuresComponent;
    [RequiredComponent(typeof(StatsContainer))]
    private StatsContainer statsComponent;
    private EvalContext ctx;

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
    
    /// <summary>
    /// Stat modifiers provided by this body part itself (not via layers).
    /// </summary>
    public readonly Dictionary<string, BodyProvidedStat[]> ProvidedStats = new();

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

    /// <summary>
    /// Layers, ordered from outermost at [0] -> innermost at [length]
    /// </summary>
    internal BodyLayer[] layers = Array.Empty<BodyLayer>();
    internal Dictionary<string, int> layersByName = new();
    /// <summary>
    /// The layers of this body part, ordered from outer to innermost
    /// </summary>
    public IEnumerable<BodyLayer> Layers => layers;
    
    public double HealthStandalone => layers.Sum(l => l.HealthStandalone);
    public double Health => layers.Sum(l => l.Health);
    public double MaxHealth => layers.Sum(l => l.MaxHealth);

    public bool VitalForGroup = false;

    public IEnumerable<Injury> Injuries => Layers.SelectMany(layer => layer.Injuries);
    public IEnumerable<(Injury injury, BodyLayer layer)> InjuriesWithLayers
    {
        get
        {
            foreach (var layer in layers)
            {
                foreach (var injury in layer.Injuries)
                    yield return (injury, layer);
            }
        }
    }
    
    public double Pain => statsComponent.GetStatValue(StatIds.Pain);
    public bool IsAlive
    {
        get {
            foreach (var layer in layers)
            {
                if (layer.KillsOnZeroHealth.Eval(ctx) && layer.Health <= 0)
                    return false;
            }
            return true;
        }
    }
    
    /// <summary>
    /// Tags associated with this body part.
    /// </summary>
    private HashSet<string> tags = new();
    public HashSet<string> Tags
    {
        get => tags;
        set => tags = value;
    }
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
    /// <summary>
    /// Helper indicating this part is classified as 'hard' (e.g. bone, armor).
    /// Determined by the presence of the "hard" tag.
    /// </summary>
    public bool IsHard => Tags.Contains("hard");
    /// <summary>
    /// Helper indicating this part is classified as 'soft' (e.g. flesh, muscle).
    /// Determined by the presence of the "soft" tag.
    /// </summary>
    public bool IsSoft => Tags.Contains("soft");


    public BodyPart(string group, Skill[] skills, Feature[] providedFeatures, string[] equipmentSlots, string[] tags, params BodyPart[] children)
    {
        Group = group;
        this.children = new(children.Length);
        providedSkills = skills;
        this.providedFeatures = providedFeatures;
        this.equipmentSlots = new Dictionary<string, Item?>();
        foreach (string slot in equipmentSlots)
            this.equipmentSlots[slot] = null;
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
        VitalForGroup = stream.ReadBoolean();
        equipmentSlots = new Dictionary<string, Item?>();

        byte slotCount = (byte)stream.ReadByte();        
        for (int i = 0; i < slotCount; i++)
        {
            string slot = stream.ReadString();
            if (stream.ReadBoolean())
                LookForComponent<Item>("equipmentSlots", stream.ReadInt32());
            else
                equipmentSlots[slot] = null;
        }

        byte skillCount = (byte)stream.ReadByte();
        providedSkills = new Skill[skillCount];
        for (int i = 0; i < skillCount; i++)
        {
            providedSkills[i] = Skill.FromBytes(stream);
        }

        int count = stream.ReadByte();
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
        ctx = new EvalContext()
        {
            Board = Board,
            Caller = this,
            Target = this,
            TargetComponent = this
        };
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

        // apply part-level modifiers first
        foreach (var entry in ProvidedStats)
        {
            var stat = ownerStats.GetStat(entry.Key);
            if (stat == null)
                continue;
            int i = 0;
            foreach (BodyProvidedStat mod in entry.Value)
            {
                if (mod.isLocal)
                    stat.SetModifier(mod.CalculateFor(this, Entity.Id + "_part_mod" + i));
                i++;
            }
        }
        // then layer modifiers
        foreach (var layer in Layers)
        {
            foreach (var entry in layer.ProvidedStats)
            {
                var stat = ownerStats.GetStat(entry.Key);
                if (stat == null)
                    continue;
                int i = 0;
                foreach (BodyProvidedStat mod in entry.Value)
                {
                    if (mod.isLocal)
                        stat.SetModifier(mod.CalculateFor(layer, Entity.Id + "_" + layer.Name + "_mod" + i));
                    i++;
                }
            }
        }
    }

    public void RemoveStatModifiers()
    {
        if (OwnerEntity == null || OwnerEntity.TryGetComponent<StatsContainer>(out var ownerStats) == false)
            return;
        
        // remove part-level modifiers
        foreach (var entry in ProvidedStats)
        {
            var stat = ownerStats.GetStat(entry.Key);
            if (stat == null)
                continue;
            int i = 0;
            foreach (BodyProvidedStat mod in entry.Value)
            {
                if (!mod.isLocal) { i++; continue; }
                stat.RemoveModifier(Entity.Id + "_part_mod" + i);
                i++;
            }
        }
        // then layer modifiers
        foreach (var layer in layers)
        {
            foreach (var entry in layer.ProvidedStats)
            {
                var stat = ownerStats.GetStat(entry.Key);
                if (stat == null)
                    continue;
                int i = 0;
                foreach (BodyProvidedStat mod in entry.Value)
                {
                    if (!mod.isLocal) continue;
                    stat.RemoveModifier(Entity.Id + "_" + layer.Name + "_mod" + i);
                    i++;
                }
            }
        }
    }

    /// <summary>
    /// Gets a BodyLayer based on it's deepness. 0 = outermost layer
    /// </summary>
    /// <param name="index">Layer Deepness</param>
    /// <returns></returns>
    public BodyLayer GetLayer(int index)
    {
        return layers[index];
    }
    public BodyLayer? FindLayer(string name)
    {
        var layerIndex = layersByName.GetValueOrDefault(name, -1);
        if (layerIndex == -1)
            return null;
        return layers[layerIndex];
    }
    public int FindLayerIndex(string name)
    {
        return layersByName.GetValueOrDefault(name, -1);
    }
    public BodyLayer FirstLayer => layers[0];
    public BodyLayer LastLayer => layers[layers.Length];
    public int LayerCount => layers.Length;

    public double Damage(DamageInstance damageInstance)
    {
        DamageEvent ev = new(this, damageInstance);
        var ctx = new EvalContext(damageInstance.Amount, damageInstance.Source.Type)
        {
            TargetComponent = this,
            Target = damageInstance.Source.Attacker,
            Caller = OwnerEntity
        };
        ev.DamageModifiers = new();
        foreach (var layer in layers)
        {
            foreach (var pair in layer.DamageModifiers)
            {
                if (pair.Key.Eval(ctx))
                    ev.DamageModifiers.Add(pair.Value);
            }
        }
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
        FirstLayer.AddInjury(source.Type.InjuryResolver(damageInstance, this));
        
        return damage;
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
    
    public float GetLocalStat(string statName)
    {
        //If no body, only this body part's stats apply, so calculate them manually.
        var body = OwnerEntity?.Body;
        if (body == null)
        {
            List<StatModifier> mods = new();
            foreach (var layer in layers)
            {
                foreach (var mod in layer.ProvidedStats.GetValueOrDefault(statName) ?? [])
                {
                    mods.Add(mod.CalculateFor(layer));
                }
            }
            if (mods.Count == 0)
                return 0;
            return Stat.ApplyModifiers(mods);
        }
        return body.GetLocalStat(Group, statName);
    }
    
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Group);
        stream.WriteBoolean(VitalForGroup);
        
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

        stream.WriteByte((byte)providedSkills.Length);
        foreach (Skill action in ProvidedSkills)
        {
            action.ToBytes(stream);
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

    public void HandleEvent(BodyLayerDiedEvent ev)
    {
        if (ev.Layer.KillsOnZeroHealth.Eval(ctx))
        {
            var ev2 = new BodyPartDiedEvent(this);
            Entity.DispatchEvent(ev2);
            OwnerEntity?.DispatchEvent(ev2);
        }
    }
}
using Rpg.Entities.Components.Inventory;
using Rpg.Entities.Interfaces;
using Rpg.Features;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg.Entities.Components.Health;

//TODO: Implement stat thresholds. Planning to use it to create "asfixiation" status when respiratory stat is too low

public partial class Body : Component, ISerializable, ITaggable,
    ITickableComponent,
    ISkillProvider,
    ComponentEventHandler<BodyPartInjuryAddedEvent>,
    ComponentEventHandler<BodyPartInjuryRemovedEvent>,
    ComponentEventHandler<BodyPartInjuryChangedEvent>,
    ComponentEventHandler<BodyPartDiedEvent>,
    ComponentEventHandler<ItemHeldEvent>,
    ComponentEventHandler<ItemUnheldEvent>,
    ComponentEventHandler<ItemEquippedEvent>,
    ComponentEventHandler<ItemUnequippedEvent>,
    ComponentEventHandler<StatsContainerEvent>
{
    public readonly EvalContext Context = new EvalContext();
    public BodyModel? Model { get; init; }
    private readonly Dictionary<string, HashSet<BodyPart>> equipmentSlots = new();
    private readonly Dictionary<BodyPart, HashSet<EquipmentProperty>> partsCovered = new();
    private readonly Dictionary<string, HashSet<BodyPart>> partsByName = new();
    private readonly Dictionary<string, HashSet<BodyPart>> partsByGroup = new();
    private readonly Dictionary<string, HashSet<BodyPart>> partsByTag = new();
    private readonly Dictionary<Injury, BodyPart> injuriesCache = new();
    private readonly HashSet<BodyPart> partsCache = new();

    public BodyStat[] Stats {get; init;} = Array.Empty<BodyStat>();
    private Dictionary<string, BodyStat> statsCache = new();
    public Feature[] Features {get; init;} = Array.Empty<Feature>();
    public IEnumerable<BodyPart> PartsWithEquipSlots => equipmentSlots.Values.SelectMany(x => x);
    public IEnumerable<Injury> Injuries => injuriesCache.Keys;
    public bool IsAlive;
    public bool IsConscious;
    public bool IsDead => !IsAlive;

    public IEnumerable<string> Groups => partsByGroup.Keys;

    public string Name
    {
        get;
        private set;
    }
    
    public BodyPart Root
    {
        get;
        set
        {
            value.Body = this;
            if (field != null)
            {
                UnindexPart(field);
                if (field.Body == this)
                    field.Body = null;
            }
            field = value;
            IndexPart(field);
        }
    }

    public IEnumerable<BodyPart> Parts => partsCache;
    public HashSet<string> Tags = new();
    HashSet<string> ITaggable.Tags {get => Tags; set => Tags = value;}

    public Body(string name, BodyPart root, BodyModel? model = null) : base()
    {
        Name = name;
        Root = root;
        Model = model;
    }

    public Body(Stream stream) : base(stream)
    {
        Name = stream.ReadString();
        this.LoadTags(stream);
        int count = stream.ReadByte();
        Stats = new BodyStat[count];
        // read stat definitions
        for (int i = 0; i < count; i++)
        {
            Stats[i] = new BodyStat(stream);
        }

        LookForComponent<BodyPart>("root", stream.ReadInt32());
    }

    protected override void OnFoundComponents(string group, List<Component> components)
    {
        if (group == "root" && components.Count > 0)
        {
            Root = (BodyPart)components[0];
        }
    }
    public override void OnInit(Entity entity)
    {
        base.OnInit(entity);
        statsCache = Stats.ToDictionary(stat => stat.Definition.Id, stat => stat);
        var statsContainer = Entity.Stats;
        if (statsContainer != null)
        {
            foreach (var stat in Stats)
            {
                statsContainer.CreateStat(stat.Definition);
            }
        }
        Context.Target = entity;
        Context.Caller = entity;
        Context.Board = entity.Board;
        Context.Variables = new object[2];
    }
    public override void OnReady()
    {
        base.OnReady();
        IndexPart(Root);
    }

    public void OnTick()
    {
        foreach (var stat in Stats)
        {
            stat.Tick(this);
        }

        foreach (var (oldInjury, part) in injuriesCache.ToArray())
        {
            var injury = oldInjury;
            if (Entity.ExistanceTicks % 50 == 0)
            {
                Context.Variables[0] = injury.Severity;
                injury = new Injury { Type = oldInjury.Type, Severity = oldInjury.Severity - oldInjury.Type.NaturalHeal.Eval(Context) };
                if (injury.Severity <= 0)
                {
                    part.RemoveInjury(injury);
                    continue;
                }
                else
                {
                    part.ChangeInjury(injury, injury);
                }
            }
            foreach (var creation in injury.Type.InjuryCreations)
            {
                Context.Variables[0] = injury.Severity;
                if (Entity.ExistanceTicks % creation.interval == 0 && creation.condition.Eval(Context))
                {
                    part.AddInjury(creation.injuryModel.Eval(Context));
                }
            }

            foreach (var conversion in injury.Type.InjuryConversions)
            {
                Context.Variables[0] = injury.Severity;
                if (Entity.ExistanceTicks % conversion.interval == 0 && conversion.condition.Eval(Context))
                {
                    part.RemoveInjury(injury);
                    part.AddInjury(conversion.injuryModel.Eval(Context));
                }
            }
        }
    }

    public void ApplyPartToOwner(BodyPart part)
    {
        part.UpdateStatModifiers();

        if (Entity.TryGetComponent<FeaturesContainer>(out var features))
        {
            foreach (Feature feat in part.ProvidedFeatures)
            {
                features.AddFeature(feat);
            }
        }

        foreach (Item item in part.Items)
        {
            //TODO: Handle items
        }
    }

    public void IndexPart(BodyPart part)
    {
        partsCache.Add(part);
        foreach (string slot in part.EquipmentSlots)
        {
            if (!equipmentSlots.ContainsKey(slot))
                equipmentSlots[slot] = new HashSet<BodyPart>();
            equipmentSlots[slot].Add(part);
        }
        if (!partsByName.ContainsKey(part.Name))
            partsByName[part.Name] = new HashSet<BodyPart>();
        partsByName[part.Name].Add(part);
        partsCovered[part] = new HashSet<EquipmentProperty>();
        if (!partsByGroup.ContainsKey(part.Group))
            partsByGroup[part.Group] = new HashSet<BodyPart>();
        partsByGroup[part.Group].Add(part);
        foreach (var tag in part.Tags)
        {
            if (!partsByTag.ContainsKey(tag))
                partsByTag[tag] = new HashSet<BodyPart>();
            partsByTag[tag].Add(part);
        }
        foreach (var injury in part.Injuries)
        {
            injuriesCache[injury] = part;
        }
        if (part.IsAlive)
            ApplyPartToOwner(part);
        
        foreach (var child in part.Children)
            IndexPart(child);
    }

    public void UnapplyPartToOwner(BodyPart part)
    {
        part.RemoveStatModifiers();
        if (Entity.TryGetComponent<FeaturesContainer>(out var features))
        {
            foreach (Feature feat in part.ProvidedFeatures)
            {
                features.RemoveFeature(feat);
            }

        }
        foreach (Item item in part.Items)
        {
            //TODO: Handle items
        }
    }

    public void UnindexPart(BodyPart part)
    {
        partsCache.Remove(part);
        foreach (string slot in part.EquipmentSlots)
        {
            if (equipmentSlots.TryGetValue(slot, out HashSet<BodyPart>? partsSet))
                partsSet.Remove(part);
        }
        partsCovered.Remove(part);
        if (partsByName.TryGetValue(part.Name, out HashSet<BodyPart>? partsSetByName))
        {
            partsSetByName.Remove(part);
            if (partsSetByName.Count == 0)
                partsByName.Remove(part.Name);
        }
        if (partsByGroup.TryGetValue(part.Group, out HashSet<BodyPart>? partsSetByGroup))
            partsSetByGroup.Remove(part);
        foreach (var tag in part.Tags)
        {
            if (partsByTag.TryGetValue(tag, out HashSet<BodyPart>? partsSetByTag))
            {
                partsSetByTag.Remove(part);
                if (partsSetByTag.Count == 0)
                    partsByTag.Remove(tag);
            }
        }
        foreach (var injury in part.Injuries)
            injuriesCache.Remove(injury);
        UnapplyPartToOwner(part);
        foreach (var child in part.Children)
            UnindexPart(child);
    }

    public void HandleEvent(BodyPartDiedEvent ev)
    {
        if (SidedLogic.Instance.IsClient())
            return;
        ev.Part.UpdateStatModifiers();
        List<BodyPart> toProcess = new() { ev.Part };
        foreach (BodyPart child in ev.Part.Children)
        {
            child.UpdateStatModifiers();
            toProcess.Add(child);
        }

        foreach (BodyPart part in toProcess)
        {
            UnapplyPartToOwner(part);
        }
    }

    public void OnEquipped(BodyPart part, EquipmentProperty ep)
    {
        partsCovered[part].Add(ep);
        foreach (string partName in ep.Coverage)
        {
            if (!partsByName.TryGetValue(partName, out HashSet<BodyPart>? partsSet))
                continue;
            foreach (BodyPart coveredPart in partsSet)
                partsCovered[coveredPart].Add(ep);
        }
    }
    public void OnUnequip(BodyPart part, EquipmentProperty ep)
    {
        partsCovered[part].Remove(ep);
        
        foreach (string partName in ep.Coverage)
        {
            if (!partsByName.TryGetValue(partName, out HashSet<BodyPart>? partsSet))
                continue;
            foreach (BodyPart coveredPart in partsSet)
                partsCovered[coveredPart].Remove(ep);
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Name);
        ((ITaggable)this).SaveTags(stream);
        stream.WriteByte((byte)Stats.Length);
        foreach (var statDef in Stats)
            statDef.ToBytes(stream);
        SaveComponentRef<BodyPart>(stream, Root);
    }

    public override void Destroy()
    {
        base.Destroy();

        foreach (var part in Parts)
        {
            UnindexPart(part);
            part.Entity.Destroy();
        }
    }

    public IEnumerable<BodyPart> GetPartsThatCanEquip(string slot)
    {
        return equipmentSlots.TryGetValue(slot, out HashSet<BodyPart>? partsSet) ? partsSet : Array.Empty<BodyPart>();
    }

    public IEnumerable<BodyPart> GetPartsWithSlot(string slot)
    {
        return GetPartsThatCanEquip(slot);
    }

    public IEnumerable<BodyPart> GetPartsOnGroup(string group)
    {
        if (partsByGroup.TryGetValue(group, out HashSet<BodyPart>? onGroup))
            return onGroup;
        return Array.Empty<BodyPart>();
    }

    public IEnumerable<BodyPart> GetPartsWithTag(string tag)
    {
        if (partsByTag.TryGetValue(tag, out HashSet<BodyPart>? onTag))
            return onTag;
        return Array.Empty<BodyPart>();
    }

    public IEnumerable<BodyPart> GetPartsWithName(string name)
    {
        if (partsByName.TryGetValue(name, out HashSet<BodyPart>? withName))
            return withName;
        return Array.Empty<BodyPart>();
    }

    public float GetLocalStat(string group, string stat, out List<StatModifier> modifiers, out float baseUsed, float? baseValue = null)
    {
        List<StatModifier> statMods = new();
        var bodyStat = statsCache.GetValueOrDefault(stat);

        //Base value is either passed as parameter, or 0
        var baseVal = baseValue ?? 0;
        var stats = Entity.Stats;

        // If the BodyStat is local, "copy" stat from the body to use as base, since it acts like "global" modifiers for the local stats
        // This means that if it isn't a local stat, the local stat will be calculated alone isolated from the body stat
        if (bodyStat != null && bodyStat.IsLocal && stats != null)
        {
            var statInstance = stats.GetStat(stat);
            if (statInstance != null)
            {
                statMods.AddRange(statInstance.GetModifiers());
            }

            // If base value is not provided, use the BodyStat's
            // Again, this only happens for local stats, if this isn't a local stat, the base value will be 0 if not provided
            if (baseValue == null)
            {
                if (statInstance != null)
                    baseVal = statInstance.BaseValue;
                else
                    baseVal = bodyStat.Definition.BaseValue;
            }
        }
        
        // Add the actual local modifiers from this group of body parts
        foreach (BodyPart part in GetPartsOnGroup(group))
        {
            if (!part.ProvidedStats.TryGetValue(stat, out BodyPart.BodyPartStat[]? partStat))
                continue;
            statMods.AddRange(partStat.Where(mod => mod.isLocal).Select(mod => mod.CalculateFor(part)));
        }

        //Apply dependencies for this BodyStat if any
        if (stats != null)
        {
            foreach (var dep in bodyStat?.Dependencies ?? Array.Empty<BodyStat.StatDependency>())
            {
                var depStat = stats.GetStat(dep.StatName);
                if (depStat != null && dep.ModifierValue != null && dep.ModifierType != null)
                {
                    Context.Variables[0] = depStat.FinalValue;
                    var modValue = dep.ModifierValue.Eval(Context);
                    var modType = dep.ModifierType.Eval(Context);
                    statMods.Add(new StatModifier(dep.ModifierId, modValue, modType)
                    {
                        DisplayName = "Dependency on " + depStat.Name
                    });
                }
            }
        }

        // Finally, apply group effectiveness if defined for this BodyStat
        if (bodyStat != null && bodyStat.GroupEffectiveness.TryGetValue(group, out float effectiveness))
            statMods.Add(new StatModifier( "body_part_group_effectiveness", effectiveness - 1, StatModifierType.Multiplier)
            {
                DisplayName = "Group Effectiveness"
            });

        // Return the final calculated stat value after applying modifiers to the base value
        modifiers = statMods;
        baseUsed = baseVal;
        return Stat.ApplyModifiers(statMods, baseVal);
    }
    public float GetLocalStat(string group, string stat, float? baseValue = null)
    {
        return GetLocalStat(group, stat, out var _ignored, out var _ignored2, baseValue);
    }

    public IEnumerable<EquipmentProperty> GetCoveringEquipment(BodyPart bp)
    {
        return partsCovered[bp];
    }

    public bool IsEquipped(Item item)
    {
        var ep = item.Entity.EquipmentProperty;
        if (ep == null)
            return false;
        return GetPartsWithSlot(ep.Slot).Any(part => part.GetEquippedItem(ep.Slot) == item);
    }

    public BodyPart? GetPartByPath(String path)
    {
        string[] split = path.Split('/');
        BodyPart current = Root;
        foreach (string partName in split)
        {
            if (partName == Root.Name)
                continue;
            BodyPart? next = current.Children.FirstOrDefault(part => part.Name.Equals(partName));
            if (next == null)
                return null;
            current = next;
        }
        return current;
    }

    public void HandleEvent(BodyPartInjuryAddedEvent ev)
    {
        injuriesCache[ev.Injury] = ev.Part;
    }

    public void HandleEvent(BodyPartInjuryRemovedEvent ev)
    {
        injuriesCache.Remove(ev.Injury);
    }

    public void HandleEvent(BodyPartInjuryChangedEvent ev)
    {
        injuriesCache.Remove(ev.OldInjury);
        injuriesCache[ev.NewInjury] = ev.Part;
    }

    public void HandleEvent(ItemHeldEvent componentEvent)
    {
        var statsComp = Entity.Stats;
        var featsComp = Entity.Features;
        var item = componentEvent.Item;
        if (statsComp != null)
        {
            foreach (var entry in item.StatModifiers)
            {
                Stat? stat = statsComp.GetStat(entry.Key);
                if (stat == null)
                    continue;
                foreach (StatModifier mod in entry.Value)
                {
                    stat.SetModifier(mod);
                }
            }

        }
        if (featsComp != null)
        {
            foreach (Feature feat in item.ProvidedFeatures)
            {
                featsComp.AddFeature(feat);
            }
        }
    }

    public void HandleEvent(ItemUnheldEvent componentEvent)
    {
        var statsComp = Entity.Stats;
        var featsComp = Entity.Features;
        var item = componentEvent.Item;
        if (statsComp != null)
        {
            foreach (var entry in item.StatModifiers)
            {
                Stat? stat = statsComp.GetStat(entry.Key);
                if (stat == null)
                    continue;
                foreach (StatModifier mod in entry.Value)
                {
                    stat.RemoveModifier(mod);
                }
            }
        }
        if (featsComp != null)
        {
            foreach (Feature feat in item.ProvidedFeatures)
            {
                featsComp.RemoveFeature(feat);
            }
        }
    }

    public void HandleEvent(ItemEquippedEvent componentEvent)
    {
    }

    public void HandleEvent(ItemUnequippedEvent componentEvent)
    {
    }

    public IEnumerable<Skill> GetSkillsFor(SkillExecutor executor)
    {
        foreach (var part in Parts)
        {
            foreach (uint id in Component.SkillProviderIDs)
            {
                if (part.Entity.GetComponent(id) is not ISkillProvider provider) continue;

                foreach (Skill skill in provider.GetSkillsFor(executor))
                {
                    yield return skill;
                }
            }
        }
    }

    public void HandleEvent(StatsContainerEvent ev)
    {
        var bodyStat = statsCache.GetValueOrDefault(ev.StatEvent.Stat.Id);
        var stat = ev.StatEvent.Stat;
        var statVal = stat.FinalValue;
        if (bodyStat != null)
        {
            if (bodyStat.OnChange != null)
            {
                Context.Variables[0] = statVal;
                bodyStat.OnChange.Eval(Context);
            }
            if (bodyStat.Thresholds != null && bodyStat.Thresholds.Length > 0)
            {
                Context.Variables[0] = statVal;
                foreach (var threshold in bodyStat.Thresholds)
                {

                    if (threshold.Condition.Eval(Context))
                    {
                        threshold.Effect.Eval(Context);
                    }
                }
            }
            if (bodyStat.Vital && statVal <= stat.MinValue)
            {
                IsAlive = false;
            }

        }
    }
}
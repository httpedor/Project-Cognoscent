using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg.Inventory;

namespace Rpg;

using StatDependency = (string stat, Either<string, float> val, Func<float, float, (float, StatModifierType)>? compiled);
using StatThreshold = (float min, float max, Action<StatThresholdGlobals>? onEnter, Action<StatThresholdGlobals>? onLeave);
//TODO: Implement stat thresholds. Planning to use it to create "asfixiation" status when respiratory stat is too low

public class StatThresholdGlobals
{
    public required Body body;
    public required Creature creature;
    public float value;
    public required Stat stat;
}
public class StatDepCodeGlobals
{
    public float x;
    public float y;
}
public class Body : ISerializable
{
    public event Action<BodyPart, Injury>? OnInjuryAdded;
    public event Action<BodyPart, Injury>? OnInjuryRemoved;
    private readonly Dictionary<string, HashSet<BodyPart>> equipmentSlots = new();
    private readonly Dictionary<BodyPart, HashSet<EquipmentProperty>> partsCovered = new();
    private readonly Dictionary<string, HashSet<BodyPart>> partsByName = new();
    private readonly Dictionary<string, HashSet<BodyPart>> partsByGroup = new();
    private readonly HashSet<BodyPart> parts = new();
    // Consolidated stat information: definition + runtime relationships (dependencies, regen, max dependency)
    public sealed class StatEntry
    {
        public Stat Def;
        public StatDependency[]? Dependencies;
        public StatThreshold[]? Thresholds;
        // regen can be either a constant float or a dependency stat name
        public Either<string, float>? Regen;
        // if non-null, this stat's MaxValue should follow the named stat's FinalValue
        public string? MaxDependencyName;
        public bool Vital = false;
        public Dictionary<string, float> GroupEffectiveness = new();

        public StatEntry(Stat def)
        {
            Def = def;
        }
    }

    internal readonly Dictionary<string, StatEntry> stats = new();
    internal readonly List<Feature> features = new();
    public IEnumerable<BodyPart> PartsWithEquipSlots => equipmentSlots.Values.SelectMany(x => x);

    public Creature? Owner
    {
        get;
        set
        {
            if (field != null)
            {
                foreach (var part in Parts)
                    UnapplyPartToOwner(part);
                if (!SidedLogic.Instance.IsClient())
                {
                    foreach (var feature in features)
                    {
                        field.RemoveFeature(feature);
                    }
                    
                    // clear any events/hooks we attached to the owner's stats
                    foreach (var depInfo in stats.Where(kv => kv.Value.Dependencies != null))
                    {
                        string statName = depInfo.Key;
                        var stat = field.GetStat(statName)!;
                        stat.ClearEvents();
                    }
                }
            }

            if (value != null)
            {
                foreach (var part in Parts)
                    ApplyPartToOwner(part);
                if (!SidedLogic.Instance.IsClient())
                {
                    foreach (var feature in features)
                    {
                        value.AddFeature(feature);
                    }
                    // create stats on the owner from our stat entries
                    foreach (var entry in stats.Values)
                    {
                        var created = value.CreateStat(entry.Def.Clone());
                        if (value is Creature creature && entry.Vital)
                        {
                            created.ValueChanged += (old, newVal) =>
                            {
                                if (newVal <= 0 && creature.Alive)
                                    creature.Kill($"{created.Id} dropped to 0");
                            };
                        }
                    }

                    // wire up dependencies
                    foreach ((string statName, var entry) in stats.Where(kv => kv.Value.Dependencies != null))
                    {
                        var stat = value.GetStat(statName)!;
                        foreach (var dependency in entry.Dependencies!)
                        {
                            var depStat = value.GetStat(dependency.stat);
                            if (depStat == null)
                                continue;
                            depStat.ValueChanged += (old, newVal) =>
                            {
                                string modId = depStat.Name;
                                if (dependency.val.IsLeft)
                                {
                                    var res = dependency.compiled!(newVal, stat.FinalValue);
                                    stat.AddModifier(new StatModifier(modId, res.Item1, res.Item2));
                                }
                                else
                                {
                                    stat.AddModifier(new StatModifier(modId, -((1-((newVal - depStat.MinValue) / (depStat.MaxValue - depStat.MinValue))) * dependency.val.Right), StatModifierType.Percent));
                                }
                            };
                        }
                    }
                }
            }

            field = value;
        }
    }

    public bool IsReady
    {
        get;
        private set;
    }

    public bool IsHumanoid
    {
        get;
        private set;
    }

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
            if (field != null)
            {
                OnPartRemoved(Root);
            }
            field = value;
            foreach (BodyPart child in field.AllChildren)
                OnPartAdded(child);
        }
    }

    public IEnumerable<BodyPart> Parts => parts;

    public static Func<float, float, (float, StatModifierType)> Compile(string code)
    {
        var script = CSharpScript.Create<(float, StatModifierType)>(code,
            ScriptOptions.Default.WithReferences(typeof(StatModifierType).Assembly, typeof(Math).Assembly)
                .WithImports("Rpg", "System.Math"), typeof(StatDepCodeGlobals)).CreateDelegate();
        return (x, y) => script(new StatDepCodeGlobals{x = x, y=y}).Result;
    }

    public Body(string name, BodyPart root, bool isHumanoid = false)
    {
        IsHumanoid = isHumanoid;
        Name = name;
        Root = root;
        IsReady = true;
    }

    public Body(Stream stream)
    {
        Name = stream.ReadString();
        IsHumanoid = stream.ReadByte() != 0;
        int count = stream.ReadByte();
        // read stat definitions
        for (int i = 0; i < count; i++)
        {
            var stat = new Stat(stream);
            stats[stat.Id] = new StatEntry(stat);
        }

        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string stat = stream.ReadString();
            int depCount = stream.ReadByte();
            var deps = new StatDependency[depCount];
            for (int j = 0; j < depCount; j++)
            {
                var depStat = stream.ReadString();
                Either<string, float> val;
                Func<float, float, (float, StatModifierType)>? compiled = null;
                if (stream.ReadBoolean())
                    val = new Either<string, float>(stream.ReadFloat());
                else
                {
                    var code = stream.ReadString();
                    val = new Either<string, float>(code);
                    if (!SidedLogic.Instance.IsClient())
                    {
                        compiled = Compile(code);
                    }
                }
 
                deps[j] = (depStat, val, compiled);
            }
            if (stats.TryGetValue(stat, out StatEntry? entry))
                entry.Dependencies = deps;
            else
                stats[stat] = new StatEntry(new Stat(stat, 0)) { Dependencies = deps };
        }
        Root = new BodyPart(stream, this);
        IsReady = true;
    }

    public void Tick()
    {
        if (Owner == null)
            return;

        foreach (var kv in stats)
        {
            var statName = kv.Key;
            var entry = kv.Value;
            var stat = Owner.GetStat(statName);
            if (stat == null) continue;

            if (!string.IsNullOrEmpty(entry.MaxDependencyName))
            {
                var depStat = Owner.GetStat(entry.MaxDependencyName);
                if (depStat != null)
                    stat.MaxValue = depStat.FinalValue;
            }

            if (entry.Regen != null)
            {
                var regenInfo = entry.Regen;
                float regenAmount = regenInfo.IsRight ? regenInfo.Right : Owner.GetStat(regenInfo.Left!)?.FinalValue ?? 0;
                stat.BaseValue = Math.Clamp(stat.BaseValue + (regenAmount * (1/50f)), stat.MinValue, stat.MaxValue);
            }
        }
    }

    public void ApplyPartToOwner(BodyPart part)
    {
        part.UpdateStatModifiers();

        if (Owner != null)
        {
            foreach (Feature feat in part.FeaturesForOwner)
            {
                Owner.AddFeature(feat);
            }
        }

        foreach (Item item in part.Items)
        {
            var prop = item.GetProperty<EquipmentProperty>();
            if (prop == null)
                OnHeld(part, item);
            else
                OnEquipped(part, prop);
        }
    }

    public void OnPartAdded(BodyPart part)
    {
        parts.Add(part);
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
        ApplyPartToOwner(part);
    }

    public void UnapplyPartToOwner(BodyPart part)
    {
        part.RemoveStatModifiers();
        if (Owner != null)
        {
            foreach (Feature feat in part.FeaturesForOwner)
            {
                Owner.RemoveFeature(feat);
            }

            if (part.GetEquippedItem(EquipmentSlot.Hold) != null)
            {
                
            }
        }
        
        foreach (Item item in part.Items)
        {
            var prop = item.GetProperty<EquipmentProperty>();
            if (prop == null)
                OnUnheld(part, item);
            else
                OnUnequip(part, prop);
        }
    }

    public void OnPartRemoved(BodyPart part)
    {
        parts.Remove(part);
        foreach (string slot in part.EquipmentSlots)
            equipmentSlots[slot].Remove(part);
        partsCovered.Remove(part);
        partsByName[part.Name].Remove(part);
        if (partsByName[part.Name].Count == 0)
            partsByName.Remove(part.Name);
        partsByGroup[part.Group].Remove(part);
        UnapplyPartToOwner(part);
        foreach (var child in part.Children)
            OnPartRemoved(child);
    }

    public void OnPartDie(BodyPart part)
    {
        foreach (BodyPart child in part.Children)
        {
            part.UpdateStatModifiers();
            OnPartDie(child);
        }

        foreach (Feature feat in part.Features)
        {
            Owner?.RemoveFeature(feat);
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

    public void OnHeld(BodyPart part, Item item)
    {
        if (Owner != null)
        {
            foreach (var entry in item.StatModifiers)
            {
                Stat? stat = Owner.GetStat(entry.Key);
                if (stat == null)
                    continue;
                foreach (StatModifier mod in entry.Value)
                {
                    stat.SetModifier(mod);
                }
            }

            foreach (Feature feat in item.Features)
            {
                Owner.AddFeature(feat);
            }
        }
    }

    public void OnUnheld(BodyPart part, Item item)
    {
        if (Owner != null)
        {
            foreach (var entry in item.StatModifiers)
            {
                Stat? stat = Owner.GetStat(entry.Key);
                if (stat == null)
                    continue;
                foreach (StatModifier mod in entry.Value)
                {
                    stat.RemoveModifier(mod);
                }
            }
            foreach (Feature feat in item.Features)
            {
                Owner.RemoveFeature(feat);
            }
        }
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteByte(IsHumanoid ? (byte)1 : (byte)0);
        // preserve original wire format: write the stat definitions first, then dependencies
        stream.WriteByte((byte)stats.Count);
        foreach (var statDef in stats)
            statDef.Value.Def.ToBytes(stream);
        var depsList = stats.Where(kv => kv.Value.Dependencies != null).ToArray();
        stream.WriteByte((byte)depsList.Length);
        foreach (var depInfo in depsList)
        {
            stream.WriteString(depInfo.Key);
            stream.WriteByte((byte)depInfo.Value.Dependencies!.Length);
            foreach (var dep in depInfo.Value.Dependencies)
            {
                stream.WriteString(dep.stat);
                stream.WriteBoolean(dep.val.IsRight);
                if (dep.val.IsRight)
                    stream.WriteFloat(dep.val.Right);
                else
                    stream.WriteString(dep.val.Left);
            }
        }
        Root.ToBytes(stream);
    }

    public BodyPart? GetBodyPart(string path)
    {
        return Root.GetChildByPath(path);
    }

    public BodyPart? GetBodyPartByName(string name)
    {
        if (partsByName.TryGetValue(name, out HashSet<BodyPart>? partsSet))
            return partsSet.First();
        return null;
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

    public float GetStatByGroup(string group, string stat, float? baseValue = null, bool onlySelfStats = false)
    {
        var baseVal = baseValue ?? Owner?.GetStat(stat)?.BaseValue ?? 0;
        List<StatModifier> statMods = new();
        if (stats.TryGetValue(stat, out StatEntry? statEntry) && statEntry.GroupEffectiveness.TryGetValue(group, out float effectiveness))
            baseVal *= effectiveness;
        foreach (BodyPart part in GetPartsOnGroup(group))
        {
            if (!part.Stats.TryGetValue(stat, out BodyPart.BodyPartStat[]? partStat))
                continue;
            statMods.AddRange(partStat.Where(mod => !onlySelfStats || !mod.appliesToOwner).Select(mod => mod.CalculateFor(part)));
        }
        return Stat.ApplyModifiers(statMods, baseVal);
    }

    public IEnumerable<EquipmentProperty> GetCoveringEquipment(BodyPart bp)
    {
        return partsCovered[bp];
    }

    public bool IsEquipped(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            return false;
        return GetPartsWithSlot(ep.Slot).Any(part => part.GetEquippedItem(ep.Slot) == item);
    }
    
    // helper to access stat definitions map similar to previous API
    private IEnumerable<Stat> StatDefinitions => stats.Values.Select(e => e.Def);
 
     public void _invokeInjuryEvent(BodyPart bp, Injury inj, bool added)
     {
         if (added)
             OnInjuryAdded?.Invoke(bp, inj);
         else
             OnInjuryRemoved?.Invoke(bp, inj);
     }
 }

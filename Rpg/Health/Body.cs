using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg.Inventory;

namespace Rpg;

using StatDependency = (string stat, Either<string, float> val, Func<float, float, (float, StatModifierType)>? compiled);

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
    private readonly Dictionary<string, Stat> statDefs = new();
    // STAT -> (Dependency, (dependencyVal, statVal) -> (ModVal, ModType))
    private readonly Dictionary<string, StatDependency[]> statDependencies = new();
    private readonly List<Feature> features = new();
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
                    //TODO: Unapply stats and statDependencies to creature
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
                    foreach (var stat in statDefs.Values)
                    {
                        value.CreateStat(stat.Clone());
                    }

                    foreach (var depInfo in statDependencies)
                    {
                        string statName = depInfo.Key;
                        var stat = value.GetStat(statName)!;
                        foreach (var dependency in depInfo.Value)
                        {
                            var depStat = value.GetStat(dependency.stat);
                            if (depStat == null)
                                continue;
                            depStat.ValueChanged += (old, newVal) =>
                            {
                                string modId = dependency.stat + "_dep";
                                if (dependency.val.IsLeft)
                                {
                                    var res = dependency.compiled(newVal, stat.FinalValue);
                                    stat.AddModifier(new StatModifier(modId, res.Item1, res.Item2));
                                }
                                else
                                {
                                    stat.AddModifier(new StatModifier(modId, -((1-(newVal / (depStat.MaxValue - depStat.MinValue))) * dependency.val.Right), StatModifierType.Percent));
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

    private static Func<float, float, (float, StatModifierType)> Compile(string code)
    {
        var script = CSharpScript.Create<(float, StatModifierType)>(code,
            ScriptOptions.Default.WithReferences(typeof(StatModifierType).Assembly, typeof(Math).Assembly)
                .WithImports("Rpg", "System.Math"), typeof(StatDepCodeGlobals)).CreateDelegate();
        return (x, y) => script(new StatDepCodeGlobals{x = x, y=y}).Result;
    }

    private Body(string name, BodyPart root, bool isHumanoid = false)
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
        for (int i = 0; i < count; i++)
        {
            var stat = new Stat(stream);
            statDefs[stat.Id] = stat;
        }

        count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string stat = stream.ReadString();
            int depCount = stream.ReadByte();
            statDependencies[stat] = new StatDependency[depCount];
            for (int j = 0; j < depCount; j++)
            {
                var depStat = stream.ReadString();
                Either<string, float> val;
                Func<float, float, (float, StatModifierType)>? compiled = null;
                if (stream.ReadBoolean())
                    val = new Either<string, float>(stream.ReadFloat());
                else
                {
                    val = new Either<string, float>(stream.ReadString());
                    if (!SidedLogic.Instance.IsClient())
                    {
                        compiled = Compile(val.Left);
                    }
                }

                statDependencies[stat][j] = (depStat, val, compiled);
            }
        }
        Root = new BodyPart(stream, this);
        IsReady = true;
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
            if (!partsByName.TryGetValue(partName, out HashSet<BodyPart>? parts))
                continue;
            foreach (BodyPart coveredPart in parts)
                partsCovered[coveredPart].Add(ep);
        }
    }
    public void OnUnequip(BodyPart part, EquipmentProperty ep)
    {
        partsCovered[part].Remove(ep);
        
        foreach (string partName in ep.Coverage)
        {
            if (!partsByName.TryGetValue(partName, out HashSet<BodyPart>? parts))
                continue;
            foreach (BodyPart coveredPart in parts)
                partsCovered[coveredPart].Remove(ep);
        }
    }

    public void OnHeld(BodyPart part, Item item)
    {
        /*var luaProp = item.GetProperty<LuaItemProperty>();
        if (luaProp != null)
        {
            //TODO: Execute lua code
        }*/

        if (Owner != null)
        {
            foreach (var entry in item.StatModifiers)
            {
                Stat stat = Owner.GetStat(entry.Key);
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
        /*var luaProp = item.GetProperty<LuaItemProperty>();
        if (luaProp != null)
        {
            //TODO: Run lua code
        }*/

        if (Owner != null)
        {
            foreach (var entry in item.StatModifiers)
            {
                Stat stat = Owner.GetStat(entry.Key);
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
        stream.WriteByte((byte)statDefs.Count);
        foreach (var statDef in statDefs)
            statDef.Value.ToBytes(stream);
        stream.WriteByte((byte)statDependencies.Count);
        foreach (var depInfo in statDependencies)
        {
            stream.WriteString(depInfo.Key);
            stream.WriteByte((byte)depInfo.Value.Length);
            foreach (var dep in depInfo.Value)
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
        if (partsByName.TryGetValue(name, out HashSet<BodyPart>? parts))
            return parts.First();
        return null;
    }

    public IEnumerable<BodyPart> GetPartsThatCanEquip(string slot)
    {
        return equipmentSlots.TryGetValue(slot, out HashSet<BodyPart>? parts) ? parts : [];
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

    public float GetStatByGroup(string group, string stat, float baseValue = 0, bool onlySelfStats = false)
    {
        List<StatModifier> stats = new();
        foreach (BodyPart part in GetPartsOnGroup(group))
        {
            if (!part.Stats.TryGetValue(stat, out BodyPart.BodyPartStat[]? partStat))
                continue;
            stats.AddRange(partStat.Where(mod => !onlySelfStats || !mod.appliesToOwner).Select(mod => mod.CalculateFor(part)));
        }

        return Stat.ApplyModifiers(stats, baseValue);
    }

    public IEnumerable<EquipmentProperty> GetCoveringEquipment(BodyPart bp)
    {
        return partsCovered[bp];
    }
    
    public static Body? NewBody(JsonObject json)
    {
        try
        {
            BodyPart? root = BodyPart.NewBody(json["root"]!.AsObject());
            if (root == null)
                return null;
            var ret = new Body(json["name"]!.ToString(), root,
                json.ContainsKey("humanoid") && json["humanoid"]!.GetValue<bool>());
            if (json.TryGetPropertyValue("features", out JsonNode? featuresNode))
            {
                foreach (JsonNode? featureJson in featuresNode.AsArray())
                {
                    string name = featureJson.GetValue<string>();
                    Feature? feature = Compendium.GetEntryObject<Feature>(name);
                    if (feature == null)
                        Console.WriteLine("Warning: Invalid creature feature in JSON: " + name);
                    else
                        ret.features.Add(feature);
                }
            }
            JsonObject? stats = json["stats"]?.AsObject();
            if (stats != null)
            {
                foreach (var pair in stats)
                {
                    JsonObject statObj = pair.Value.AsObject();
                    string statName = pair.Key;
                    float baseVal = statObj["base"]?.GetValue<float>() ?? 0;
                    float maxVal  = statObj["max"]?.GetValue<float>() ?? float.MaxValue;
                    float minVal = statObj["min"]?.GetValue<float>() ?? 0;
                    bool overCap = statObj["overCap"]?.GetValue<bool>() ?? true;
                    bool underCap = statObj["underCap"]?.GetValue<bool>() ?? true;
                    ret.statDefs[statName] = new Stat(statName, baseVal, maxVal, minVal, overCap, underCap);
                    if (statObj.ContainsKey("dependsOn"))
                    {
                        ret.statDependencies[statName] = new StatDependency[statObj["dependsOn"]!.AsObject().Count];
                        int i = 0;
                        foreach (var depPair in statObj["dependsOn"]!.AsObject())
                        {
                            string depName = depPair.Key;
                            var depVal = depPair.Value!;
                            Either<string, float> val;
                            if (depPair.Value!.GetValueKind() == JsonValueKind.Number)
                                val = new Either<string, float>(depVal.GetValue<float>());
                            else
                                val = new Either<string, float>(depVal.GetValue<string>());

                            if (SidedLogic.Instance.IsClient() || val.IsRight)
                                ret.statDependencies[statName][i] = (depName, val, null);
                            else
                                ret.statDependencies[statName][i] = (depName, val, Compile(val.Left));
                            i++;
                        }
                    }
                }

                foreach (var deps in ret.statDependencies)
                {
                    foreach (var dep in deps.Value)
                    {
                        if (!ret.statDefs.ContainsKey(dep.stat))
                        {
                            Console.WriteLine($"Warning: stat {deps.Key} depends on stat {dep.stat} but it wasn't defined in this body!");
                        }
                    }
                }
            }

            return ret;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while creating body from json: " + json);
            Console.WriteLine(e);
            return null;
        }
    }

    public void _invokeInjuryEvent(BodyPart bp, Injury inj, bool added)
    {
        if (added)
            OnInjuryAdded?.Invoke(bp, inj);
        else
            OnInjuryRemoved?.Invoke(bp, inj);
    }
}

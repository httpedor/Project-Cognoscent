using System.Net;
using System.Text.Json.Nodes;

namespace Rpg;

public class SkillTreeEntryRef : ISerializable
{
    public string Name;
    public CreatureRef Creature;
    public SkillTreeEntry? Entry => Creature.Creature?.SkillTree?.GetEntry(Name);

    public SkillTreeEntryRef(SkillTreeEntry ste)
    {
        Name = ste.Name;
        Creature = new CreatureRef(ste.Tree.Owner);
    }

    public SkillTreeEntryRef(Stream stream)
    {
        Name = stream.ReadString();
        Creature = new CreatureRef(stream);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        Creature.ToBytes(stream);
    }
}
public class SkillTreeEntry : ISerializable, ISkillSource
{
    public SkillTree Tree;
    public string Name { get; } = "";
    public bool Enabled;
    public readonly string Icon = "";
    public readonly string Description = "";
    public readonly string Category;
    public readonly List<Skill> Skills = new();
    IEnumerable<Skill> ISkillSource.Skills => Skills;
    public readonly List<Feature> Features = new();
    public readonly List<string> Dependencies = new();

    public SkillTreeEntry(string name, string description, string category)
    {
        Name = name;
        Description = description;
        Category = category;
    }

    public SkillTreeEntry(JsonObject json, string category)
    {
        Category = category;
        if (json.ContainsKey("feature"))
        {
            string featureName = json["feature"]!.GetValue<string>();
            var feature = Compendium.GetEntry<Feature>(featureName);
            if (feature == null)
                Console.WriteLine("Invalid feature name: " + featureName);
            else
            {
                Name = feature.GetName();
                Description = feature.GetDescription();
                Icon = feature.GetIconName();
                Features.Add(feature);
            }
        }
        if (json.ContainsKey("skill"))
        {
            string skillName = json["skill"]!.GetValue<string>();
            var skill = Compendium.GetEntry<Skill>(skillName);
            if (skill == null)
                Console.WriteLine("Invalid skill name: " + skillName);
            else
            {
                Name = skill.GetName();
                Description = skill.GetDescription();
                Icon = skill.GetIconName();
                Skills.Add(skill);
            }
        }
        Name = json["name"]?.GetValue<string>() ?? Name;
        Description = json["description"]?.GetValue<string>() ?? Description;
        Icon = json["icon"]?.GetValue<string>() ?? Icon;
        Enabled =  json["enabled"]?.GetValue<bool>() ?? false;
        WithFeatures(json["features"]?.AsArray().Select(feat => feat.GetValue<string>()).ToArray());
        WithSkills(json["skills"]?.AsArray().Select(skill => skill.GetValue<string>()).ToArray());
        WithDependencies(json["dependencies"]?.AsArray().Select(dep => dep.GetValue<string>()).ToArray());
    }
    public SkillTreeEntry(Stream stream)
    {
        Name = stream.ReadString();
        Description = stream.ReadLongString();
        Category = stream.ReadString();
        Icon = stream.ReadString();
        Enabled = stream.ReadBoolean();
        byte count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            Skills.Add(Skill.FromBytes(stream));
        }

        count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            Features.Add(Feature.FromBytes(stream));
        }
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteLongString(Description);
        stream.WriteString(Category);
        stream.WriteString(Icon);
        stream.WriteBoolean(Enabled);
        stream.WriteByte((byte)Skills.Count);
        foreach (var skill in Skills)
            skill.ToBytes(stream);
        stream.WriteByte((byte)Features.Count);
        foreach (var feat in Features)
            feat.ToBytes(stream);
    }

    public SkillTreeEntry WithSkills(params Skill[] skills)
    {
        Skills.AddRange(skills);
        return this;
    }

    public SkillTreeEntry WithSkills(params string[] skills)
    {
        if (skills == null)
            return this;
        foreach (var skillName in skills)
        {
            var skill = Compendium.GetEntry<Skill>(skillName);
            if (skill == null)
            {
                Console.WriteLine("Invalid skill name: " + skillName);
                continue;
            }
            Skills.Add(skill);
        }

        return this;
    }

    public SkillTreeEntry WithFeatures(params Feature[] features)
    {
        Features.AddRange(features);
        return this;
    }

    public SkillTreeEntry WithFeatures(params string[] features)
    {
        if (features == null)
            return this;
        foreach  (var featureName in features)
        {
            var feature = Compendium.GetEntry<Feature>(featureName);
            if (feature == null)
            {
                Console.WriteLine("Invalid feature name: " + featureName);
                continue;
            }
            Features.Add(feature);
        }

        return this;
    }

    public SkillTreeEntry WithDependencies(params string[] dependencies)
    {
        if (dependencies == null)
            return this;
        Dependencies.AddRange(dependencies);
        return this;
    }

    public void Enable()
    {
        Tree.EnableEntry(Name);
    }

    public void Disable()
    {
        Tree.DisableEntry(Name);
    }

    public bool CanEnable => Dependencies.All(dep => Tree.IsEnabled(dep));
}
public class SkillTree : ISerializable
{
    public Creature Owner;
    private Dictionary<string, SkillTreeEntry> entries = new();
    private Dictionary<string, SkillTreeEntry> enabledEntries = new();
    private Dictionary<string, List<SkillTreeEntry>> entriesByCategory = new ();
    public IEnumerable<SkillTreeEntry> Entries => entries.Values;
    public IEnumerable<SkillTreeEntry> EnabledEntries => enabledEntries.Values;

    public SkillTree(Creature creature)
    {
        Owner = creature;
    }

    public SkillTree(JsonObject json)
    {
        foreach (var entry in json)
        {
            string category = entry.Key;
            if (category == "icon")
                continue;
            JsonArray entries = entry.Value.AsArray();
            WithEntries(entries.Select(entryJson => new SkillTreeEntry(entryJson.AsObject(), category)).ToArray());
        }
    }

    public SkillTree(JsonObject json, Creature owner) : this(json)
    {
        Owner = owner;
    }

    public SkillTree(Stream stream)
    {
        ushort count = stream.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            var entry = new SkillTreeEntry(stream);
            entry.Tree = this;
            entries[entry.Name] = entry;
            if (!entriesByCategory.ContainsKey(entry.Category))
                entriesByCategory.Add(entry.Category, []);
            entriesByCategory[entry.Category].Add(entry);
            if (entry.Enabled)
                enabledEntries[entry.Name] = entry;
        }
    }

    public SkillTreeEntry? GetEntry(string name)
    {
        return entries.GetValueOrDefault(name);
    }

    public bool IsEnabled(string name)
    {
        return enabledEntries.ContainsKey(name);
    }

    public IEnumerable<SkillTreeEntry> GetEntriesInCategory(string category)
    {
        return entriesByCategory.GetValueOrDefault(category, new List<SkillTreeEntry>());
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteUInt16((ushort)entries.Count);
        foreach (var entry in entries.Values)
        {
            entry.ToBytes(stream);
        }
    }

    public SkillTree WithEntries(params SkillTreeEntry[] entries)
    {
        foreach (var entry in entries)
        {
            entry.Tree = this;
            this.entries[entry.Name] = entry;
            if (!entriesByCategory.ContainsKey(entry.Category))
                entriesByCategory.Add(entry.Category, []);
            entriesByCategory[entry.Category].Add(entry);
        }
        return this;
    }

    public SkillTreeEntry? EnableEntry(string name)
    {
        var entry = entries.GetValueOrDefault(name);
        if (entry == null)
            return entry;

        entry.Enabled = true;
        enabledEntries[name] = entry;

        foreach (var feat in entry.Features)
            Owner.AddFeature(feat);
        
        return entry;
    }

    public SkillTreeEntry? DisableEntry(string name)
    {
        var entry = enabledEntries.GetValueOrDefault(name);
        if (entry == null)
            return entry;
        entry.Enabled = false;
        enabledEntries.Remove(name);
        foreach (var feat in entry.Features)
            Owner.RemoveFeature(feat);
        return entry;
    }
}
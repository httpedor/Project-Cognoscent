using System.Text.Json;
using Rpg.Features;
using Rpg.Skills;

namespace Rpg.Entities.Components;

public class SkillTreeEntryRef : ISerializable
{
    public string Name;
    public ComponentRef<SkillTree> SkillTree;
    public SkillTreeEntry? Entry => SkillTree.Component?.GetEntry(Name);

    public SkillTreeEntryRef(SkillTreeEntry ste)
    {
        Name = ste.Name;
        SkillTree = new ComponentRef<SkillTree>(ste.Tree);
    }

    public SkillTreeEntryRef(Stream stream)
    {
        Name = stream.ReadString();
        SkillTree = new ComponentRef<SkillTree>(stream);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        SkillTree.ToBytes(stream);
    }
}
public class SkillTreeEntry : ISerializable
{
    public SkillTree Tree;
    public string Name { get; } = "";
    public bool Enabled;
    public readonly string Icon = "";
    public readonly string Description = "";
    public readonly string Category;
    public readonly List<Skill> Skills = new();
    public readonly List<Feature> Features = new();
    public readonly List<string> Dependencies = new();

    public SkillTreeEntry(string name, string description, string category)
    {
        Name = name;
        Description = description;
        Category = category;
    }

    public SkillTreeEntry(JsonElement json, string category)
    {
        Category = category;
        if (json.TryGetProperty("feature", out JsonElement featureElement))
        {
            string featureName = featureElement.GetString()!;
            var feature = Compendium.GetEntry<Feature>(featureName);
            if (feature == null)
                Console.WriteLine("Invalid feature name: " + featureName);
            else
            {
                Name = feature.Name;
                Description = feature.Description;
                Icon = feature.Icon;
                Features.Add(feature);
            }
        }
        if (json.TryGetProperty("skill", out JsonElement skillElement))
        {
            string skillName = skillElement.GetString()!;
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
        Name = json.GetProperty("name").GetString() ?? Name;
        Description = json.GetProperty("description").GetString() ?? Description;
        Icon = json.GetProperty("icon").GetString() ?? Icon;
        Enabled =  json.GetProperty("enabled").GetBoolean();
        WithFeatures(json.GetProperty("features").EnumerateArray().Select(feat => feat.GetString()!).ToArray());
        WithSkills(json.GetProperty("skills").EnumerateArray().Select(skill => skill.GetString()!).ToArray());
        WithDependencies(json.GetProperty("dependencies").EnumerateArray().Select(dep => dep.GetString()!).ToArray());
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
public partial class SkillTree : Component, ISerializable
{
    private Dictionary<string, SkillTreeEntry> entries = new();
    private Dictionary<string, SkillTreeEntry> enabledEntries = new();
    private Dictionary<string, List<SkillTreeEntry>> entriesByCategory = new ();
    public IEnumerable<SkillTreeEntry> Entries => entries.Values;
    public IEnumerable<SkillTreeEntry> EnabledEntries => enabledEntries.Values;

    public SkillTree()
    {
    }

    public SkillTree(JsonElement json)
    {
        foreach (var entry in json.EnumerateObject())
        {
            string category = entry.Name;
            if (category == "icon")
                continue;
            WithEntries(entry.Value.EnumerateArray().Select(entryJson => new SkillTreeEntry(entryJson, category)).ToArray());
        }
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
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
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

        if (Entity.TryGetComponent<FeaturesContainer>(out var features))
        {
            foreach (var feat in entry.Features)
                features.AddFeature(feat);
        }
        
        return entry;
    }

    public SkillTreeEntry? DisableEntry(string name)
    {
        var entry = enabledEntries.GetValueOrDefault(name);
        if (entry == null)
            return entry;
        entry.Enabled = false;
        enabledEntries.Remove(name);
        if (Entity.TryGetComponent<FeaturesContainer>(out var features))
            foreach (var feat in entry.Features)
                features.RemoveFeature(feat);
        return entry;
    }
}
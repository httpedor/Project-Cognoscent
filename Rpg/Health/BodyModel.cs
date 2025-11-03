using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpg;

using StatDependency = (string stat, Either<string, float> val, Func<float, float, (float, StatModifierType)>? compiled);

public class StatModifierJson
{
    [JsonPropertyName("stat")] public string? Stat { get; set; }
    [JsonPropertyName("atFull")] public JsonElement AtFull { get; set; }
    [JsonPropertyName("atZero")] public JsonElement? AtZero { get; set; }
    [JsonPropertyName("standaloneHPOnly")] public bool StandaloneHPOnly { get; set; }
    [JsonPropertyName("operation")] public string? Operation { get; set; }
}

public class DamageModifierJson
{
    [JsonPropertyName("damageType")] public string? DamageType { get; set; }
    [JsonPropertyName("value")] public JsonElement Value { get; set; }
    [JsonPropertyName("operation")] public string? Operation { get; set; }
}

public class BodyPartJson
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("group")] public string? Group { get; set; }
    [JsonPropertyName("maxHealth")] public JsonElement MaxHealth { get; set; }
    [JsonPropertyName("area")] public JsonElement Area { get; set; }
    [JsonPropertyName("painMultiplier")] public float PainMultiplier { get; set; }
    [JsonPropertyName("slots")] public List<string>? Slots { get; set; }
    [JsonPropertyName("stats")] public List<StatModifierJson>? Stats { get; set; }
    [JsonPropertyName("damageModifiers")] public List<DamageModifierJson>? DamageModifiers { get; set; }
    [JsonPropertyName("flags")] public List<string>? Flags { get; set; }
    [JsonPropertyName("skills")] public List<string>? Skills { get; set; }
    [JsonPropertyName("selfFeatures")] public List<string>? SelfFeatures { get; set; }
    [JsonPropertyName("features")] public List<string>? Features { get; set; }
    [JsonPropertyName("children")] public List<BodyPartJson>? Children { get; set; }
    [JsonPropertyName("metadata")] public JsonElement? Metadata { get; set; }
}

public class StatJson
{
    [JsonPropertyName("base")] public JsonElement Base { get; set; }
    [JsonPropertyName("max")] public JsonElement? Max { get; set; }
    [JsonPropertyName("min")] public JsonElement? Min { get; set; }
    [JsonPropertyName("overCap")] public bool OverCap { get; set; } = true;
    [JsonPropertyName("underCap")] public bool UnderCap { get; set; } = true;
    [JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
    [JsonPropertyName("vital")] public bool Vital { get; set; }
    [JsonPropertyName("regen")] public JsonElement? Regen { get; set; }
    [JsonPropertyName("dependsOn")] public Dictionary<string, JsonElement>? DependsOn { get; set; }
    [JsonPropertyName("groupEffectiveness")] public Dictionary<string, float>? GroupEffectiveness { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("onChange")] public string? OnChange { get; set; }
}

public class BodyJson
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("humanoid")] public bool IsHumanoid { get; set; }
    [JsonPropertyName("features")] public List<string>? Features { get; set; }
    [JsonPropertyName("stats")] public Dictionary<string, StatJson>? Stats { get; set; }
    [JsonPropertyName("root")] public BodyPartJson? Root { get; set; }
}

public class BodyPartModel
{
    public sealed class PartStatModifierConfig
    {
        public string StatName = string.Empty;
        public JsonElement? AtFullJson;
        public JsonElement? AtZeroJson;
        public bool StandaloneHpOnly;
        public StatModifierType Operation = StatModifierType.Flat;
        public bool ApplyToOwner = true;
    }

    public sealed class DamageModifierConfig
    {
        public DamageType Type = default!;
        public JsonElement? ValueJson;
        public StatModifierType Operation = StatModifierType.Percent;
    }

    public string Name;
    public string Group;
    public JsonElement? MaxHealthJson;
    public JsonElement? AreaJson;
    public float PainMultiplier;
    public Skill[] Skills = Array.Empty<Skill>();
    public byte Flags;
    public List<BodyPartModel> Children = new();
    public List<string> EquipmentSlots = new();
    public List<Injury> Injuries = new();
    public List<Feature> OwnerFeatures = new();
    public List<Feature> SelfFeatures = new();
    // By stat name, list of modifiers defined for that part
    public Dictionary<string, List<PartStatModifierConfig>> Stats = new();
    public Dictionary<DamageType, List<DamageModifierConfig>> DamageModifiers = new();
    public JsonElement? Metadata;

    public BodyPartModel(JsonElement json)
    {
        var jsonModel = json.Deserialize<BodyPartJson>();
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyPartJson");

        Name = jsonModel.Name ?? "unnamed";
        Group = jsonModel.Group ?? "default";
        MaxHealthJson = jsonModel.MaxHealth;
        AreaJson = jsonModel.Area;
        PainMultiplier = jsonModel.PainMultiplier;
        EquipmentSlots = jsonModel.Slots ?? new();

        if (jsonModel.Stats != null)
        {
            foreach (var stat in jsonModel.Stats)
            {
                if (string.IsNullOrWhiteSpace(stat.Stat)) continue;

                var cfg = new PartStatModifierConfig
                {
                    StatName = stat.Stat!,
                    AtFullJson = stat.AtFull,
                    AtZeroJson = stat.AtZero ?? JsonDocument.Parse("0").RootElement,
                    StandaloneHpOnly = stat.StandaloneHPOnly,
                    Operation = JsonHelpers.ParseOp(stat.Operation, StatModifierType.Flat),
                    ApplyToOwner = true
                };

                if (!Stats.TryGetValue(cfg.StatName, out var list))
                {
                    list = new List<PartStatModifierConfig>();
                    Stats[cfg.StatName] = list;
                }
                list.Add(cfg);
            }
        }

        if (jsonModel.DamageModifiers != null)
        {
            foreach (var dmg in jsonModel.DamageModifiers)
            {
                if (string.IsNullOrWhiteSpace(dmg.DamageType)) continue;
                if (DamageType.FromName(dmg.DamageType) is not DamageType dt)
                {
                    Logger.LogWarning("Invalid damage type: " + dmg.DamageType);
                    continue;
                }

                var cfg = new DamageModifierConfig
                {
                    Type = dt,
                    ValueJson = dmg.Value,
                    Operation = JsonHelpers.ParseOp(dmg.Operation, StatModifierType.Percent)
                };

                if (!DamageModifiers.TryGetValue(dt, out var list))
                {
                    list = new List<DamageModifierConfig>();
                    DamageModifiers[dt] = list;
                }
                list.Add(cfg);
            }
        }

        if (jsonModel.Flags != null)
        {
            foreach (var flag in jsonModel.Flags)
            {
                switch (flag)
                {
                    case "HasBone": Flags |= BodyPart.Flag.HasBone; break;
                    case "Hard": Flags |= BodyPart.Flag.Hard; break;
                    case "Internal": Flags |= BodyPart.Flag.Internal; break;
                    case "Overlaps": Flags |= BodyPart.Flag.Overlaps; break;
                }
            }
        }

        if (jsonModel.Skills != null)
        {
            var skills = new List<Skill>();
            foreach (var skillName in jsonModel.Skills)
            {
                var skill = Compendium.GetEntry<Skill>(skillName);
                if (skill != null)
                    skills.Add(skill);
                else
                    Logger.LogWarning("Invalid skill name in BodyPart JSON: " + skillName);
            }
            Skills = skills.ToArray();
        }

        if (jsonModel.SelfFeatures != null)
        {
            var features = new List<Feature>();
            foreach (var name in jsonModel.SelfFeatures)
            {
                var feature = Compendium.GetEntry<Feature>(name);
                if (feature == null)
                    Logger.LogWarning("Invalid feature in JSON: " + name);
                else
                    features.Add(feature);
            }
            SelfFeatures = features;
        }

        if (jsonModel.Features != null)
        {
            foreach (var name in jsonModel.Features)
            {
                var feature = Compendium.GetEntry<Feature>(name);
                if (feature == null)
                    Logger.LogWarning("Invalid creature feature in JSON: " + name);
                else
                    OwnerFeatures.Add(feature);
            }
            Metadata = jsonModel.Metadata;
        }

        if (jsonModel.Children != null)
        {
            foreach (var childJson in jsonModel.Children)
            {
                var childElement = JsonDocument.Parse(JsonSerializer.Serialize(childJson)).RootElement;
                var child = new BodyPartModel(childElement);
                Children.Add(child);
            }
        }

        Metadata = jsonModel.Metadata;
    }

    public BodyPart Build(Body? body = null)
    {
        int maxHealth = MaxHealthJson != null ? JsonModel.GetInt(MaxHealthJson.Value) : 1;
        float sizePercentage = AreaJson != null ? JsonModel.GetFloat(AreaJson.Value) * 100f : 0;

        var builder = new BodyPart.Builder(Name)
            .Group(Group)
            .Health(maxHealth)
            .Size(sizePercentage)
            .Pain(PainMultiplier)
            .Skills(Skills)
            .Flags(Flags)
            .Equipment(EquipmentSlots.ToArray())
            .OwnerFeatures(OwnerFeatures.ToArray())
            .Features(SelfFeatures.ToArray());

        foreach (var child in Children)
            builder.Child(child.Build(body));

        foreach (var statEntry in Stats)
        {
            foreach (var cfg in statEntry.Value)
            {
                float atFull = cfg.AtFullJson != null ? JsonModel.GetFloat(cfg.AtFullJson.Value) : 0;
                float atZero = cfg.AtZeroJson != null ? JsonModel.GetFloat(cfg.AtZeroJson.Value) : 0;
                builder.Stat(cfg.StatName, atFull, atZero, cfg.Operation, cfg.StandaloneHpOnly, cfg.ApplyToOwner);
            }
        }

        foreach (var dmgEntry in DamageModifiers)
        {
            foreach (var cfg in dmgEntry.Value)
            {
                float value = cfg.ValueJson != null ? JsonModel.GetFloat(cfg.ValueJson.Value) : 0;
                builder.DamageModifier(cfg.Type, value, cfg.Operation);
            }
        }

        var ret = builder.Build();

        // Apply metadata if any (future extension point)
        if (Metadata != null)
        {
            // ret.CustomDataFromJson(Metadata);
        }

        ret.Body = body;
        return ret;
    }
}

public class BodyModel
{
    public sealed class StatConfig
    {
        public JsonElement? BaseJson;
        public JsonElement? MaxJson;
        public JsonElement? MinJson;
        public bool OverCap = true;
        public bool UnderCap = true;
        public string[] Aliases = Array.Empty<string>();
        public bool Vital;
        public string? MaxDependencyName;
        public string? MinDependencyName;
        public JsonElement? RegenJson;
        public List<DependencyConfig>? DependsOn;
        public Dictionary<string, float> GroupEffectiveness = new();
        public string? Name;
        public string? OnChange;
    }

    public sealed class DependencyConfig
    {
        public string DepName = string.Empty;
        public JsonElement? ValJson;
    }

    public string Name = null!;
    public bool IsHumanoid;
    public List<Feature> Features = new();
    public Dictionary<string, StatConfig> Stats = new();
    public BodyPartModel Root = null!;

    public BodyModel(JsonElement json)
    {
        var jsonModel = json.Deserialize<BodyJson>();
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyJson");

        Name = jsonModel.Name ?? "unnamed";
        IsHumanoid = jsonModel.IsHumanoid;
        if (jsonModel.Root == null) throw new Exception("Root is required");
        var rootElement = JsonDocument.Parse(JsonSerializer.Serialize(jsonModel.Root)).RootElement;
        Root = new BodyPartModel(rootElement);

        if (jsonModel.Features != null)
        {
            foreach (var name in jsonModel.Features)
            {
                var feature = Compendium.GetEntry<Feature>(name);
                if (feature == null)
                    Logger.LogWarning("Invalid creature feature in JSON: " + name);
                else
                    Features.Add(feature);
            }
        }

        if (jsonModel.Stats != null)
        {
            foreach (var (statName, statJson) in jsonModel.Stats)
            {
                var baseJson = statJson.Base;
                var maxJson = statJson.Max;
                var minJson = statJson.Min;
                var regenJson = statJson.Regen;

                string? maxDep = null;
                if (maxJson.HasValue && maxJson.Value.ValueKind == JsonValueKind.String)
                    maxDep = maxJson.Value.GetString();
                string? minDep = null;
                if (minJson.HasValue && minJson.Value.ValueKind == JsonValueKind.String)
                    minDep = minJson.Value.GetString();

                List<DependencyConfig>? dependsOn = null;
                if (statJson.DependsOn != null)
                {
                    dependsOn = new List<DependencyConfig>();
                    foreach (var (depName, valElement) in statJson.DependsOn)
                    {
                        dependsOn.Add(new DependencyConfig
                        {
                            DepName = depName,
                            ValJson = valElement
                        });
                    }
                }

                var groupEffectiveness = new Dictionary<string, float>();
                if (statJson.GroupEffectiveness != null)
                {
                    foreach (var (group, value) in statJson.GroupEffectiveness)
                        groupEffectiveness[group] = value;
                }

                Stats[statName] = new StatConfig
                {
                    BaseJson = baseJson,
                    MaxJson = maxJson,
                    MinJson = minJson,
                    OverCap = statJson.OverCap,
                    UnderCap = statJson.UnderCap,
                    Aliases = statJson.Aliases?.ToArray() ?? Array.Empty<string>(),
                    Vital = statJson.Vital,
                    MaxDependencyName = maxDep,
                    MinDependencyName = minDep,
                    RegenJson = regenJson,
                    DependsOn = dependsOn,
                    GroupEffectiveness = groupEffectiveness,
                    Name = statJson.Name,
                    OnChange = statJson.OnChange
                };
            }
        }
    }

    public Body Build()
    {
        BodyPart rootPart = Root.Build();
        var body = new Body(Name, rootPart, IsHumanoid);
        foreach (var feature in Features)
            body.features.Add(feature);

        foreach (var (statName, cfg) in Stats)
        {
            float baseVal = cfg.BaseJson != null ? JsonModel.GetFloat(cfg.BaseJson.Value) : 0;
            float maxVal = float.MaxValue;
            if (cfg.MaxJson?.ValueKind == JsonValueKind.Number)
                maxVal = JsonModel.GetFloat(cfg.MaxJson.Value);
            float minVal = 0;
            if (cfg.MinJson?.ValueKind == JsonValueKind.Number)
                minVal = JsonModel.GetFloat(cfg.MinJson.Value);

            var stat = new Stat(statName, baseVal, maxVal, minVal, cfg.OverCap, cfg.UnderCap)
            {
                Aliases = cfg.Aliases,
                Name = cfg.Name ?? statName
            };
            var entry = new Body.StatEntry(stat)
            {
                Vital = cfg.Vital,
                MaxDependencyName = cfg.MaxDependencyName,
            };

            if (cfg.MinDependencyName != null)
            {
                // Represent min dependency as a dependency that applies a Capmin modifier
                entry.Dependencies = new StatDependency[]
                {
                    (statName + "_min_dep", new Either<string, float>(cfg.MinDependencyName), (x, _) => (x, StatModifierType.Capmin))
                };
            }

            if (cfg.RegenJson != null)
            {
                if (cfg.RegenJson.Value.ValueKind == JsonValueKind.String)
                    entry.Regen = cfg.RegenJson.Value.GetString()!;
                else if (cfg.RegenJson.Value.ValueKind == JsonValueKind.Number)
                    entry.Regen = JsonModel.GetFloat(cfg.RegenJson.Value);
                else
                    Logger.LogWarning("Invalid regen value for stat " + statName);
            }

            if (cfg.DependsOn != null)
            {
                var deps = new List<StatDependency>();
                foreach (var dep in cfg.DependsOn)
                {
                    Either<string, float> val;
                    if (dep.ValJson?.ValueKind == JsonValueKind.Number)
                        val = new Either<string, float>(JsonModel.GetFloat(dep.ValJson.Value));
                    else
                        val = new Either<string, float>(dep.ValJson!.Value.GetString()!);

                    if (SidedLogic.Instance.IsClient() || val.IsRight)
                        deps.Add((dep.DepName, val, null));
                    else
                        deps.Add((dep.DepName, val, Body.CompileDep(val.Left)));
                }
                if (deps.Count > 0)
                    entry.Dependencies = deps.ToArray();
            }
            if (cfg.OnChange != null && !SidedLogic.Instance.IsClient())
            {
                entry.OnChange = Body.CompileOnChange(cfg.OnChange);
            }

            entry.GroupEffectiveness = cfg.GroupEffectiveness;
            body.stats[statName] = entry;
        }

        foreach (var deps in body.stats.Where(kv => kv.Value.Dependencies != null))
        {
            foreach (var dep in deps.Value.Dependencies!)
            {
                if (!body.stats.ContainsKey(dep.stat))
                    Logger.LogWarning($"Stat {deps.Key} depends on stat {dep.stat} but it wasn't defined in this body!");
            }
        }

        return body;
    }
}
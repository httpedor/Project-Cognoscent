using System.Text.Json;
using System.Text.Json.Nodes;
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
    [JsonPropertyName("metadata")] public JsonObject? Metadata { get; set; }
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
    // Small, self-documenting config types to replace raw tuples
    public sealed class PartStatModifierConfig
    {
        public string StatName { get; init; } = string.Empty;
        public JsonNode? AtFullJson { get; init; }
        public JsonNode? AtZeroJson { get; init; }
        public bool StandaloneHpOnly { get; init; }
        public StatModifierType Operation { get; init; } = StatModifierType.Flat;
        public bool ApplyToOwner { get; init; } = true;
    }

    public sealed class DamageModifierConfig
    {
        public DamageType Type { get; init; } = default!;
        public JsonNode? ValueJson { get; init; }
        public StatModifierType Operation { get; init; } = StatModifierType.Percent;
    }

    private static class JsonHelpers
    {
        public static JsonNode? ToNode(JsonElement el) => JsonNode.Parse(el.GetRawText());
        public static JsonNode? ToNodeOrNull(JsonElement? el) => el.HasValue ? JsonNode.Parse(el.Value.GetRawText()) : null;
        public static StatModifierType ParseOp(string? op, StatModifierType fallback) => Enum.TryParse<StatModifierType>(op ?? string.Empty, true, out var v) ? v : fallback;
    }

    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = "default";
    public JsonNode? MaxHealthJson { get; set; }
    public JsonNode? AreaJson { get; set; }
    public float PainMultiplier { get; set; }
    public Skill[] Skills { get; set; } = Array.Empty<Skill>();
    public byte Flags { get; set; }
    public List<BodyPartModel> Children { get; set; } = new();
    public List<string> EquipmentSlots { get; set; } = new();
    public List<Injury> Injuries { get; set; } = new();
    public List<Feature> OwnerFeatures { get; set; } = new();
    public List<Feature> SelfFeatures { get; set; } = new();
    // By stat name, list of modifiers defined for that part
    public Dictionary<string, List<PartStatModifierConfig>> Stats { get; set; } = new();
    public Dictionary<DamageType, List<DamageModifierConfig>> DamageModifiers { get; set; } = new();
    public JsonObject? Metadata { get; set; }

    public BodyPartModel(JsonObject json)
    {
        var jsonModel = JsonSerializer.Deserialize<BodyPartJson>(json.ToString());
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyPartJson");

        Name = jsonModel.Name ?? "unnamed";
        Group = jsonModel.Group ?? "default";
        MaxHealthJson = JsonHelpers.ToNode(jsonModel.MaxHealth);
        AreaJson = JsonHelpers.ToNode(jsonModel.Area);
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
                    AtFullJson = JsonHelpers.ToNode(stat.AtFull),
                    AtZeroJson = stat.AtZero.HasValue ? JsonHelpers.ToNode(stat.AtZero.Value) : JsonValue.Create(0f),
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
                    ValueJson = JsonHelpers.ToNode(dmg.Value),
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
        }

        if (jsonModel.Children != null)
        {
            foreach (var childJson in jsonModel.Children)
            {
                var childJsonObj = JsonNode.Parse(JsonSerializer.Serialize(childJson)) as JsonObject;
                if (childJsonObj != null)
                {
                    var child = new BodyPartModel(childJsonObj);
                    Children.Add(child);
                }
                else
                {
                    Logger.LogWarning("Invalid child body part JSON structure under " + Name);
                }
            }
        }

        Metadata = jsonModel.Metadata;
    }

    public BodyPart Build(Body? body = null)
    {
        int maxHealth = MaxHealthJson != null ? JsonModel.GetInt((JsonValue)MaxHealthJson) : 1;
        float sizePercentage = AreaJson != null ? JsonModel.GetFloat((JsonValue)AreaJson) * 100f : 0;

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
                float atFull = cfg.AtFullJson != null ? JsonModel.GetFloat((JsonValue)cfg.AtFullJson) : 0;
                float atZero = cfg.AtZeroJson != null ? JsonModel.GetFloat((JsonValue)cfg.AtZeroJson) : 0;
                builder.Stat(cfg.StatName, atFull, atZero, cfg.Operation, cfg.StandaloneHpOnly, cfg.ApplyToOwner);
            }
        }

        foreach (var dmgEntry in DamageModifiers)
        {
            foreach (var cfg in dmgEntry.Value)
            {
                float value = cfg.ValueJson != null ? JsonModel.GetFloat((JsonValue)cfg.ValueJson) : 0;
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
    // Strongly-typed config to replace the long Stats tuple
    public sealed class StatConfig
    {
        public JsonNode? BaseJson { get; init; }
        public JsonNode? MaxJson { get; init; }
        public JsonNode? MinJson { get; init; }
        public bool OverCap { get; init; } = true;
        public bool UnderCap { get; init; } = true;
        public string[] Aliases { get; init; } = Array.Empty<string>();
        public bool Vital { get; init; }
        public string? MaxDependencyName { get; init; }
        public string? MinDependencyName { get; init; }
        public JsonNode? RegenJson { get; init; }
        public List<DependencyConfig>? DependsOn { get; init; }
        public Dictionary<string, float> GroupEffectiveness { get; init; } = new();
    }

    public sealed class DependencyConfig
    {
        public string DepName { get; init; } = string.Empty;
        public JsonNode? ValJson { get; init; }
    }

    private static class JsonHelpers
    {
        public static JsonNode? ToNode(JsonElement el) => JsonNode.Parse(el.GetRawText());
        public static JsonNode? ToNodeOrNull(JsonElement? el) => el.HasValue ? JsonNode.Parse(el.Value.GetRawText()) : null;
    }

    public string Name { get; set; } = null!;
    public bool IsHumanoid { get; set; }
    public List<Feature> Features { get; set; } = new();
    public Dictionary<string, StatConfig> Stats { get; set; } = new();
    public BodyPartModel Root { get; set; } = null!;

    public BodyModel(JsonObject json)
    {
        var jsonModel = JsonSerializer.Deserialize<BodyJson>(json.ToString());
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyJson");

        Name = jsonModel.Name ?? "unnamed";
        IsHumanoid = jsonModel.IsHumanoid;
        if (jsonModel.Root == null) throw new Exception("Root is required");
        var rootJson = JsonNode.Parse(JsonSerializer.Serialize(jsonModel.Root));
        if (rootJson is not JsonObject rootObj) throw new Exception("Root must be object");
        Root = new BodyPartModel(rootObj);

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
                var baseJson = JsonHelpers.ToNode(statJson.Base);
                var maxJson = JsonHelpers.ToNodeOrNull(statJson.Max);
                var minJson = JsonHelpers.ToNodeOrNull(statJson.Min);
                var regenJson = statJson.Regen.HasValue ? JsonHelpers.ToNode(statJson.Regen.Value) : null;

                string? maxDep = null;
                if (maxJson != null && maxJson.GetValueKind() == JsonValueKind.String)
                    maxDep = maxJson.GetValue<string>();
                string? minDep = null;
                if (minJson != null && minJson.GetValueKind() == JsonValueKind.String)
                    minDep = minJson.GetValue<string>();

                List<DependencyConfig>? dependsOn = null;
                if (statJson.DependsOn != null)
                {
                    dependsOn = new List<DependencyConfig>();
                    foreach (var (depName, valElement) in statJson.DependsOn)
                    {
                        dependsOn.Add(new DependencyConfig
                        {
                            DepName = depName,
                            ValJson = JsonHelpers.ToNode(valElement)
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
                    GroupEffectiveness = groupEffectiveness
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
            float baseVal = cfg.BaseJson != null ? JsonModel.GetFloat((JsonValue)cfg.BaseJson) : 0;
            float maxVal = float.MaxValue;
            if (cfg.MaxJson?.GetValueKind() == JsonValueKind.Number)
                maxVal = JsonModel.GetFloat((JsonValue)cfg.MaxJson);
            float minVal = 0;
            if (cfg.MinJson?.GetValueKind() == JsonValueKind.Number)
                minVal = JsonModel.GetFloat((JsonValue)cfg.MinJson);

            var stat = new Stat(statName, baseVal, maxVal, minVal, cfg.OverCap, cfg.UnderCap)
            {
                Aliases = cfg.Aliases
            };
            var entry = new Body.StatEntry(stat)
            {
                Vital = cfg.Vital,
                MaxDependencyName = cfg.MaxDependencyName
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
                if (cfg.RegenJson.GetValueKind() == JsonValueKind.String)
                    entry.Regen = cfg.RegenJson.GetValue<string>();
                else if (cfg.RegenJson.GetValueKind() == JsonValueKind.Number)
                    entry.Regen = JsonModel.GetFloat((JsonValue)cfg.RegenJson);
                else
                    Logger.LogWarning("Invalid regen value for stat " + statName);
            }

            if (cfg.DependsOn != null)
            {
                var deps = new List<StatDependency>();
                foreach (var dep in cfg.DependsOn)
                {
                    Either<string, float> val;
                    if (dep.ValJson?.GetValueKind() == JsonValueKind.Number)
                        val = new Either<string, float>(JsonModel.GetFloat((JsonValue)dep.ValJson));
                    else
                        val = new Either<string, float>(dep.ValJson!.GetValue<string>());

                    if (SidedLogic.Instance.IsClient() || val.IsRight)
                        deps.Add((dep.DepName, val, null));
                    else
                        deps.Add((dep.DepName, val, Body.Compile(val.Left)));
                }
                if (deps.Count > 0)
                    entry.Dependencies = deps.ToArray();
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
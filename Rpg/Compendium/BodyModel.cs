using System.Text.Json;
using System.Text.Json.Serialization;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Features;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg;

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
    [JsonPropertyName("providedStats")] public List<StatModifierJson>? ProvidedStats { get; set; }
    [JsonPropertyName("damageModifiers")] public List<DamageModifierJson>? DamageModifiers { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("skills")] public List<string>? Skills { get; set; }
    [JsonPropertyName("selfFeatures")] public List<string>? SelfFeatures { get; set; }
    [JsonPropertyName("features")] public List<string>? Features { get; set; }
    [JsonPropertyName("children")] public List<BodyPartJson>? Children { get; set; }
    [JsonPropertyName("stats")] public Dictionary<string, JsonElement>? Stats { get; set; }
    [JsonPropertyName("metadata")] public JsonElement? Metadata { get; set; }
}

public class StatJson
{
    [JsonPropertyName("base")] public JsonElement? Base;
    [JsonPropertyName("max")] public JsonElement? Max;
    [JsonPropertyName("min")] public JsonElement? Min;
    [JsonPropertyName("overCap")] public bool OverCap = true;
    [JsonPropertyName("underCap")] public bool UnderCap = true;
    [JsonPropertyName("aliases")] public List<string>? Aliases;
    [JsonPropertyName("vital")] public bool Vital;
    [JsonPropertyName("regen")] public JsonElement? Regen;
    [JsonPropertyName("dependsOn")] public Dictionary<string, JsonElement>? DependsOn;
    [JsonPropertyName("groupEffectiveness")] public Dictionary<string, float>? GroupEffectiveness;
    [JsonPropertyName("name")] public string? Name;
    [JsonPropertyName("onChange")] public JsonElement? OnChange;
    [JsonPropertyName("thresholds")] public List<JsonElement>? Thresholds;
    [JsonPropertyName("local")] public bool IsLocal;
}

public class BodyJson
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("humanoid")] public bool IsHumanoid { get; set; }
    [JsonPropertyName("features")] public List<string>? Features { get; set; }
    [JsonPropertyName("stats")] public Dictionary<string, StatJson>? Stats { get; set; }
    [JsonPropertyName("root")] public BodyPartJson? Root { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
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
    }

    public sealed class DamageModifierConfig
    {
        public DamageType Type = default!;
        public JsonElement? ValueJson;
        public StatModifierType Operation = StatModifierType.Percent;
    }

    /// <summary>
    /// The name of this body part.
    /// </summary>
    public string Name;
    /// <summary>
    /// The group this body part belongs to (e.g., "head", "torso").
    /// </summary>
    public string Group;
    /// <summary>
    /// The maximum health of this body part.
    /// </summary>
    public JsonElement? MaxHealthJson;
    public float PainMultiplier;
    /// <summary>
    /// Skills that this body part provides.
    /// </summary>
    public Skill[] Skills = Array.Empty<Skill>();
    /// <summary>
    /// Child body parts attached to this part.
    /// </summary>
    public List<BodyPartModel> Children = new();
    /// <summary>
    /// Which equipment slots this body part has.
    /// </summary>
    public List<string> EquipmentSlots = new();
    /// <summary>
    /// What injuries this body part has sustained.
    /// </summary>
    public List<Injury> Injuries = new();
    /// <summary>
    /// Features that apply to the owner when this part is present.
    /// </summary>
    public List<Feature> OwnerFeatures = new();
    /// <summary>
    /// Features that apply to this body part itself.
    /// </summary>
    public List<Feature> SelfFeatures = new();
    /// <summary>
    /// Stats provided by this body part.
    /// </summary>
    public Dictionary<string, List<PartStatModifierConfig>> ProvidedStats = new();
    /// <summary>
    /// Base stats of this body part.
    /// </summary>
    public Dictionary<string, float> Stats = new();
    /// <summary>
    /// Damage modifiers applied by this body part.
    /// </summary>
    public Dictionary<DamageType, List<DamageModifierConfig>> DamageModifiers = new();
    /// <summary>
    /// Tags associated with this body part.
    /// </summary>
    public List<string> Tags = new();
    /// <summary>
    /// Additional metadata for this body part.
    /// </summary>
    public JsonElement? Metadata;

    public BodyPartModel(JsonElement json)
    {
        var jsonModel = json.Deserialize<BodyPartJson>();
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyPartJson");

        Name = jsonModel.Name ?? "unnamed";
        Group = jsonModel.Group ?? "default";
        MaxHealthJson = jsonModel.MaxHealth;
        PainMultiplier = jsonModel.PainMultiplier;
        EquipmentSlots = jsonModel.Slots ?? new();

        if (jsonModel.ProvidedStats != null)
        {
            foreach (var stat in jsonModel.ProvidedStats)
            {
                if (string.IsNullOrWhiteSpace(stat.Stat)) continue;

                var cfg = new PartStatModifierConfig
                {
                    StatName = stat.Stat!,
                    AtFullJson = stat.AtFull,
                    AtZeroJson = stat.AtZero ?? JsonDocument.Parse("0").RootElement,
                    StandaloneHpOnly = stat.StandaloneHPOnly,
                    Operation = JsonHelpers.ParseOp(stat.Operation, StatModifierType.Flat),
                };

                if (!ProvidedStats.TryGetValue(cfg.StatName, out var list))
                {
                    list = new List<PartStatModifierConfig>();
                    ProvidedStats[cfg.StatName] = list;
                }
                list.Add(cfg);
            }
        }
        if (jsonModel.Stats != null)
        {
            foreach (var (statName, statValue) in jsonModel.Stats)
            {
                Stats[statName] = JsonHelpers.GetFloat(statValue);
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

        if (jsonModel.Tags != null)
        {
            foreach (var tag in jsonModel.Tags)
            {
                Tags.Add(tag.ToLowerInvariant());
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

    public EntityWith<BodyPart> Build(BodyModel body)
    {
        int maxHealth = MaxHealthJson != null ? JsonHelpers.GetInt(MaxHealthJson.Value) : 1;

        // Build children first
        var childParts = new List<BodyPart>(Children.Count);
        foreach (var child in Children)
            childParts.Add(child.Build(body).Component);

        // Construct the BodyPart entity
        Entity bpEntity = new Entity(Name);
        var statsComponent = new StatsContainer()
        {
            Entity = bpEntity
        };
        statsComponent.CreateStat(new Stat(StatIds.MaxHealth, maxHealth));
        bpEntity.AddComponent(statsComponent);

        var featuresComponent = new FeaturesContainer()
        {
            Entity = bpEntity
        };
        bpEntity.AddComponent(featuresComponent);

        var ret = new BodyPart(
            Group,
            Skills,
            OwnerFeatures.ToArray(),
            EquipmentSlots.ToArray(),
            Injuries.ToArray(),
            Tags.ToArray(),
            childParts.ToArray()
        )
        {
            Entity = bpEntity,
        };
        bpEntity.AddComponent(ret);

        // Apply per-part stat modifiers
        foreach (var provided in ProvidedStats)
        {
            var bodyStat = body.Stats.GetValueOrDefault(provided.Key);
            var applyToOwner = bodyStat == null || !bodyStat.IsLocal;
            var mods = new List<BodyPart.BodyPartStat>(provided.Value.Count);
            foreach (var cfg in provided.Value)
            {
                float atFull = cfg.AtFullJson != null ? JsonHelpers.GetFloat(cfg.AtFullJson.Value) : 0;
                float atZero = cfg.AtZeroJson != null ? JsonHelpers.GetFloat(cfg.AtZeroJson.Value) : 0;
                mods.Add(new BodyPart.BodyPartStat(atFull, atZero, cfg.Operation, cfg.StandaloneHpOnly, applyToOwner));
            }
            if (mods.Count > 0)
                ret.ProvidedStats[provided.Key] = mods.ToArray();
        }
        foreach (var statEntry in Stats)
        {
            statsComponent.CreateStat(new Stat(statEntry.Key, statEntry.Value, 0, 100, false, false));
        }

        // Apply damage modifiers (previous builder path did not persist these into the instance)
        foreach (var dmgEntry in DamageModifiers)
        {
            var mods = new List<StatModifier>(dmgEntry.Value.Count);
            foreach (var cfg in dmgEntry.Value)
            {
                float value = cfg.ValueJson != null ? JsonHelpers.GetFloat(cfg.ValueJson.Value) : 0;
                mods.Add(new StatModifier($"{Name}-{dmgEntry.Key.Name}-mod", value, cfg.Operation));
            }
            if (mods.Count > 0)
                ret.DamageModifiers[dmgEntry.Key] = mods.ToArray();
        }

        // Add self-applied features to the part
        foreach (var feat in SelfFeatures)
            featuresComponent.AddFeature(feat);

        return new EntityWith<BodyPart>(bpEntity, ret);
    }
}

public class BodyModel
{
    public class StatConfig
    {
        public Expr<float>? BaseVal;
        public Expr<float>? MaxVal;
        public Expr<float>? MinVal;
        public bool OverCap = true;
        public bool UnderCap = true;
        public string[] Aliases = Array.Empty<string>();
        public bool Vital;
        public string? MaxDependencyName;
        public string? MinDependencyName;
        public JsonElement? RegenJson;
        public List<BodyStat.StatDependency>? DependsOn;
        public Dictionary<string, float> GroupEffectiveness = new();
        public string? Name;
        public JsonElement? OnChange;
        public List<BodyStat.StatThreshold> Thresholds = new();
        public bool IsLocal;
    }
    private JsonElement originalJson;


    public string Name = null!;
    public List<string> Tags = new();
    public List<Feature> Features = new();
    public Dictionary<string, StatConfig> Stats = new();
    public BodyPartModel Root = null!;

    public BodyModel(JsonElement json)
    {
        var jsonModel = json.Deserialize<BodyJson>();
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyJson");

        Name = jsonModel.Name ?? "unnamed";
        Tags = jsonModel.Tags ?? new List<string>();
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

                List<BodyStat.StatDependency>? dependsOn = null;
                if (statJson.DependsOn != null)
                {
                    dependsOn = new List<BodyStat.StatDependency>();
                    foreach (var (depName, valElement) in statJson.DependsOn)
                    {
                        
                        if (valElement.ValueKind == JsonValueKind.Number)
                        {
                            dependsOn.Add(new BodyStat.StatDependency(
                                depName,
                                ExpressionCompiler.CompileNumber(valElement),
                                new EnumExpr<StatModifierType>(new StringLiteralExpr("Percent"))
                            ));
                        }
                        else if (valElement.ValueKind == JsonValueKind.Object)
                        {
                            var numberEl = valElement.GetPropertyOrNull("value");
                            if (!numberEl.HasValue)
                            {
                                Logger.LogWarning($"Invalid dependsOn entry for stat {statName}: missing 'value' property");
                                continue;
                            }
                            var typeEl = valElement.GetPropertyOrNull("type");
                            if (!typeEl.HasValue)
                            {
                                Logger.LogWarning($"Invalid dependsOn entry for stat {statName}: 'type' property must be a string");
                                continue;
                            }
                            dependsOn.Add(new BodyStat.StatDependency(
                                depName,
                                ExpressionCompiler.CompileNumber(numberEl.Value),
                                ExpressionCompiler.CompileEnum<StatModifierType>(typeEl.Value)
                            ));
                        }
                        else
                            Logger.LogWarning($"Invalid dependsOn value for stat {statName} and dependency {depName}: must be a number or an object");
                    }
                }

                var groupEffectiveness = new Dictionary<string, float>();
                if (statJson.GroupEffectiveness != null)
                {
                    foreach (var (group, value) in statJson.GroupEffectiveness)
                        groupEffectiveness[group] = value;
                }

                List<BodyStat.StatThreshold> thresholds = new();
                if (statJson.Thresholds != null)
                {
                    foreach (var thresholdEl in statJson.Thresholds)
                    {
                        if (thresholdEl.ValueKind != JsonValueKind.Object)
                        {
                            Logger.LogWarning($"Invalid threshold entry for stat {statName}: must be an object");
                            continue;
                        }
                        var conditionEl = thresholdEl.GetPropertyOrNull("condition");
                        var effectEl = thresholdEl.GetPropertyOrNull("effect");
                        var unapplyEl = thresholdEl.GetPropertyOrNull("reverse");
                        if (!conditionEl.HasValue || !effectEl.HasValue)
                        {
                            Logger.LogWarning($"Invalid threshold entry for stat {statName}: missing 'condition' or 'effect' property");
                            continue;
                        }
                        Expr<bool> condition = ExpressionCompiler.CompileCondition(conditionEl.Value);
                        EffectExpr effect;
                        EffectExpr? unapplyEffect = null;

                        if (effectEl.Value.ValueKind == JsonValueKind.String && Compendium.EntryExists<Feature>(effectEl.Value.GetString()!))
                            effect = new AddFeatureEffect(new CompendiumEntryExpr<Feature>(effectEl.Value.GetString()!), new TargetSelectorExpr());
                        else
                            effect = ExpressionCompiler.CompileEffect(effectEl.Value);

                        if (unapplyEl.HasValue && unapplyEl.Value.ValueKind == JsonValueKind.String && Compendium.EntryExists<Feature>(unapplyEl.Value.GetString()!))
                            unapplyEffect = new RemoveFeatureEffect(new CompendiumEntryExpr<Feature>(unapplyEl.Value.GetString()!), new TargetSelectorExpr());
                        else if (unapplyEl.HasValue)
                            unapplyEffect = ExpressionCompiler.CompileEffect(unapplyEl.Value);

                        thresholds.Add(new BodyStat.StatThreshold(condition, effect, unapplyEffect));
                    }
                }
                Stats[statName] = new StatConfig
                {
                    BaseVal = baseJson.HasValue ? ExpressionCompiler.CompileNumber(baseJson.Value) : null,
                    MaxVal = maxJson.HasValue ? ExpressionCompiler.CompileNumber(maxJson.Value) : null,
                    MinVal = minJson.HasValue ? ExpressionCompiler.CompileNumber(minJson.Value) : null,
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
                    OnChange = statJson.OnChange,
                    Thresholds = thresholds,
                    IsLocal = statJson.IsLocal
                };
            }
        }
    }

    public JsonElement GetOriginalJson()
    {
        return originalJson;
    }

    public EntityWith<Body> Build()
    {
        BodyPart rootPart = Root.Build(this).Component;
        var entity = new Entity(Name);
        entity.AddComponent(new StatsContainer() { Entity = entity });
        entity.AddComponent(new FeaturesContainer() { Entity = entity });


        List<BodyStat> statsList = new();
        foreach (var (statName, cfg) in Stats)
        {
            float baseVal = cfg.BaseVal != null ? cfg.BaseVal.Eval() : 0;
            float maxVal = float.MaxValue;
            if (cfg.MaxVal != null)
                maxVal = cfg.MaxVal.Eval();
            float minVal = 0;
            if (cfg.MinVal != null)
                minVal = cfg.MinVal.Eval();

            var stat = new Stat(statName, baseVal, maxVal, minVal, cfg.OverCap, cfg.UnderCap)
            {
                Aliases = cfg.Aliases,
                Name = cfg.Name ?? statName
            };
            var entry = new BodyStat(stat)
            {
                Vital = cfg.Vital,
                MaxDependencyName = cfg.MaxDependencyName,
                MinDependencyName = cfg.MinDependencyName,
                IsLocal = cfg.IsLocal,
                Thresholds = cfg.Thresholds.ToArray()
            };

            if (cfg.RegenJson != null)
            {
                entry.Regen = ExpressionCompiler.CompileNumber(cfg.RegenJson.Value);
            }

            if (cfg.DependsOn != null && cfg.DependsOn.Count > 0)
                entry.Dependencies = cfg.DependsOn.ToArray();
            if (cfg.OnChange.HasValue)
                entry.OnChange = ExpressionCompiler.CompileEffect(cfg.OnChange.Value);

            entry.GroupEffectiveness = cfg.GroupEffectiveness;
            statsList.Add(entry);
        }

        var body = new Body(Name, rootPart, this)
        {
            Entity = entity,
            Features = Features.ToArray(),
            Stats = statsList.ToArray(),
            Tags = new HashSet<string>(Tags)
        };
        entity.AddComponent(body);

        foreach (var deps in body.Stats.Where(stat => stat.Dependencies != null))
        {
            foreach (var dep in deps.Dependencies!)
            {
                if (!body.Stats.Any(s => s.Definition.Name == dep.StatName))
                    Logger.LogWarning($"Stat {deps.Definition.Name} depends on stat {dep.StatName} but it wasn't defined in this body!");
            }
        }

        return new EntityWith<Body>(entity, body);
    }
}
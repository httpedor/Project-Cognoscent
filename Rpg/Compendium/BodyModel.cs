using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    [JsonPropertyName("area")] public JsonElement Area { get; set; }
    [JsonPropertyName("painMultiplier")] public float PainMultiplier { get; set; }
    [JsonPropertyName("healthMultiplier")] public JsonElement? HealthMultiplier { get; set; }
    [JsonPropertyName("sensitivity")] public JsonElement? Sensitivity { get; set; }
    [JsonPropertyName("providedStats")] public List<StatModifierJson>? ProvidedStats { get; set; }
    [JsonPropertyName("slots")] public List<string>? Slots { get; set; }
    [JsonPropertyName("damageModifiers")] public List<DamageModifierJson>? DamageModifiers { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("skills")] public List<string>? Skills { get; set; }
    [JsonPropertyName("selfFeatures")] public List<string>? SelfFeatures { get; set; }
    [JsonPropertyName("features")] public List<string>? Features { get; set; }
    [JsonPropertyName("children")] public List<BodyPartJson>? Children { get; set; }
    [JsonPropertyName("stats")] public Dictionary<string, JsonElement>? Stats { get; set; }
    [JsonPropertyName("metadata")] public JsonElement? Metadata { get; set; }
    [JsonPropertyName("layersProfile")] public JsonElement? LayersProfile { get; set; }
    [JsonPropertyName("condition")] public JsonElement? Condition { get; set; }
    [JsonPropertyName("groupVital")] public JsonElement? VitalForGroup { get; set; }
}

public class StatJson
{
    [JsonPropertyName("base")] public JsonElement? Base { get; set; }
    [JsonPropertyName("max")] public JsonElement? Max { get; set; }
    [JsonPropertyName("min")] public JsonElement? Min { get; set; }
    [JsonPropertyName("overCap")] public bool OverCap { get; set; } = true;
    [JsonPropertyName("underCap")] public bool UnderCap { get; set; } = true;
    [JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
    [JsonPropertyName("vital")] public bool Vital { get; set; } = false;
    [JsonPropertyName("local")] public bool Local { get; set; } = false; //FIXME: For some unknown reason, this isn't being deserialized properly
    [JsonPropertyName("regen")] public JsonElement? Regen { get; set; }
    [JsonPropertyName("dependsOn")] public Dictionary<string, JsonElement>? DependsOn { get; set; }
    [JsonPropertyName("groupEffectiveness")] public Dictionary<string, float>? GroupEffectiveness { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("onChange")] public JsonElement? OnChange { get; set; }
    [JsonPropertyName("thresholds")] public List<JsonElement>? Thresholds { get; set; }
}
public class BodyLayerJson
{
    [JsonPropertyName("bloodFlow")] public JsonElement? BloodFlow { get; set; }
    [JsonPropertyName("maxHealth")] public JsonElement? MaxHealth { get; set; }
    [JsonPropertyName("regen")] public JsonElement? Regen { get; set; }
    [JsonPropertyName("onHeal")] public JsonElement? OnHeal { get; set; }
    [JsonPropertyName("pain")] public JsonElement? Pain { get; set; }
    [JsonPropertyName("vital")] public JsonElement? Vital { get; set; }
    [JsonPropertyName("bypass")] public JsonElement? Bypass { get; set; }
    [JsonPropertyName("damageModifiers")] public JsonElement? DamageModifiers { get; set; }
    [JsonPropertyName("resistance")] public JsonElement? Resistance { get; set; }
    [JsonPropertyName("injuries")] public List<JsonElement>? Injuries { get; set; }
    [JsonPropertyName("providedStats")] public List<StatModifierJson>? ProvidedStats { get; set; }
    [JsonPropertyName("surfaceArea")] public JsonElement? SurfaceArea { get; set; }
}
public class BodyLayerProfileJson
{
    [JsonPropertyName("extends")] public string? Extends { get; set; }
    [JsonPropertyName("order")] public List<string>? Order { get; set; }
    [JsonPropertyName("layers")] public required Dictionary<string, BodyLayerJson> Layers { get; set; }
}
public class BodyJson
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("postures")] public List<JsonElement>? Postures { get; set; }
    [JsonPropertyName("restingPosture")] public JsonElement? RestingPosture { get; set; }
    [JsonPropertyName("features")] public List<string>? Features { get; set; }
    [JsonPropertyName("height")] public JsonElement? Height { get; set; }
    [JsonPropertyName("stats")] public Dictionary<string, StatJson>? Stats { get; set; }
    [JsonPropertyName("root")] public BodyPartJson? Root { get; set; }
    [JsonPropertyName("tags")] public List<JsonElement>? Tags { get; set; }
    [JsonPropertyName("layerProfiles")] public Dictionary<string, BodyLayerProfileJson>? LayerProfiles { get; set; }
    [JsonPropertyName("sexualDimorphism")] public bool SexualDimorphism { get; set; }
    [JsonPropertyName("onBuild")] public List<JsonElement>? OnBuild { get; set; }
    [JsonPropertyName("preBuild")] public List<JsonElement>? PreBuild { get; set; }
    [JsonPropertyName("onHealthChange")] public List<JsonElement>? OnHealthChange { get; set; }
}

public class BodyLayerModel
{
    public string Name = "";
    /// <summary>
    /// Evaluated at build time to determine the blood flow of this layer.
    /// </summary>
    public Expr<float> BloodFlow = new ConstNumberExpr(0);
    /// <summary>
    /// Evaluated at build time to determine the max health of this layer.
    /// </summary>
    public Expr<float> MaxHealth = new ConstNumberExpr(10);
    /// <summary>
    /// How much an injury's severity is reduced per second due to natural healing. Carried onto the built object.
    /// </summary>
    public Expr<float> Regen = new ConstNumberExpr(0);
    /// <summary>
    /// Executed when an injury is naturally healed. Carried onto the built object.
    /// </summary>
    public EffectExpr? OnHeal;
    /// <summary>
    /// Pain multiplier for injuries on this layer. Carried onto the built object.
    /// </summary>
    public Expr<float> Pain = new ConstNumberExpr(1);
    /// <summary>
    /// Whether this layer getting to 0 health instantly disables the body part. Carried onto the built object.
    /// </summary>
    public Expr<bool> Vital = new ConstConditionExpr(false);
    /// <summary>
    /// Whether this layer is bypassed by a damage instance. Carried onto the built object.
    /// </summary>
    public Expr<bool> Bypass = new ConstConditionExpr(false);
    /// <summary>
    /// Damage modifiers: condition -> modifier. Carried onto the built object.
    /// </summary>
    public Dictionary<Expr<bool>, StatModifier> DamageModifiers = new();
    /// <summary>
    /// Penetration resistance: condition -> threshold. Carried onto the built object.
    /// </summary>
    public Dictionary<Expr<bool>, Expr<float>> PenetrationResistance = new();
    /// <summary>
    /// Stats provided by this layer, keyed by stat name. Resolved at build time.
    /// </summary>
    public Dictionary<string, List<BodyPartModel.PartStatModifierConfig>> ProvidedStatsRaw = new();
    /// <summary>
    /// Pre-baked injuries to apply when the layer is built. Each entry has an optional condition.
    /// </summary>
    public List<(Expr<bool>? Condition, InjuryModel Injury)> InjuryModels = new();
    /// <summary>
    /// How much of the body part's surface area this layer covers. This is used to calculate how likely it is for an attack to hit this layer, and is used to calculate bypass chance. This is a value from 0 to 1, where 1 means the layer covers the entire body part and 0 means it doesn't cover any of it. Evaluated at build time and carried onto the built object.
    /// </summary>
    public Expr<float> SurfaceArea = new ConstNumberExpr(1);

    /// <summary>
    /// Parse a BodyLayerModel from a resolved JsonElement for a single layer.
    /// </summary>
    public static BodyLayerModel FromJson(string name, JsonElement json)
    {
        var model = new BodyLayerModel { Name = name };
        var layerJson = json.Deserialize<BodyLayerJson>();
        if (layerJson == null) return model;

        model.BloodFlow = ExpressionCompiler.CompileOr(layerJson.BloodFlow, model.BloodFlow);
        model.MaxHealth = ExpressionCompiler.CompileOr(layerJson.MaxHealth, model.MaxHealth);
        model.Regen = ExpressionCompiler.CompileOr(layerJson.Regen, model.Regen);
        if (layerJson.OnHeal.HasValue)
            model.OnHeal = ExpressionCompiler.CompileEffect(layerJson.OnHeal.Value);
        model.Pain = ExpressionCompiler.CompileOr(layerJson.Pain, model.Pain);
        model.Vital = ExpressionCompiler.CompileOr(layerJson.Vital, model.Vital);
        model.Bypass = ExpressionCompiler.CompileOr(layerJson.Bypass, model.Bypass);
        model.SurfaceArea = ExpressionCompiler.CompileOr(layerJson.SurfaceArea, model.SurfaceArea);

        // Parse damage modifiers. Simple format: { "damageTypeName": NumberExpr }, condition defaults to
        // VarIsEntryConditionExpr on var 1 (the DamageType). Full format:
        // [{ "condition": ConditionExpr, "operation": EnumExpr, "value": NumberExpr }].
        // Modifiers need a float at build time, so values are evaluated eagerly.
        if (layerJson.DamageModifiers.HasValue)
        {
            model.DamageModifiers = JsonHelpers.ParseConditionedEntries(
                layerJson.DamageModifiers.Value,
                dmgTypeName => new VarIsEntryConditionExpr(new StringLiteralExpr(dmgTypeName), 1),
                (dmgTypeName, valEl) => new StatModifier(
                    $"layer-{name}-{dmgTypeName}-dmgmod",
                    ExpressionCompiler.Compile<float>(valEl).Eval(),
                    StatModifierType.Percent),
                entry =>
                {
                    var valEl = entry.GetPropertyOrNull("value");
                    if (!valEl.HasValue) return null;
                    var opEl = entry.GetPropertyOrNull("operation");
                    var op = opEl.HasValue
                        ? JsonHelpers.ParseOp(opEl.Value.GetString(), StatModifierType.Percent)
                        : StatModifierType.Percent;
                    return new StatModifier($"layer-{name}-dmgmod", ExpressionCompiler.Compile<float>(valEl.Value).Eval(), op);
                });
        }

        // Parse injuries
        if (layerJson.Injuries != null)
        {
            foreach (var injEl in layerJson.Injuries)
            {
                if (injEl.ValueKind != JsonValueKind.Object) continue;
                try
                {
                    Expr<bool>? condition = null;
                    if (injEl.TryGetProperty("condition", out var condEl))
                        condition = ExpressionCompiler.Compile<bool>(condEl);
                    var injuryModel = new InjuryModel(injEl);
                    model.InjuryModels.Add((condition, injuryModel));
                }
                catch (Exception e)
                {
                    Logger.LogError($"[BodyLayerModel] Could not parse injury on layer '{name}': {e}");
                }
            }
        }

        // Parse providedStats
        model.ProvidedStatsRaw = BodyPartModel.PartStatModifierConfig.ParseList(layerJson.ProvidedStats);

        // Parse penetration resistance. Simple format: { "damageTypeName": NumberExpr }, scaled by the
        // layer's current health. Full format: [{ "condition": ConditionExpr, "value": NumberExpr }].
        if (layerJson.Resistance.HasValue)
        {
            model.PenetrationResistance = JsonHelpers.ParseConditionedEntries(
                layerJson.Resistance.Value,
                dmgTypeName => new VarIsEntryConditionExpr(new StringLiteralExpr(dmgTypeName), 1),
                (dmgTypeName, valEl) => (Expr<float>)new MulExpr(
                    ExpressionCompiler.Compile<float>(valEl),
                    new LayerHealthExpr(new CastExpr<BodyPart?>(new EntityToComponentExpr<BodyPart>(new CallerEntityExpr())), new StringLiteralExpr(name))),
                entry =>
                {
                    var valEl = entry.GetPropertyOrNull("value");
                    return valEl.HasValue ? ExpressionCompiler.Compile<float>(valEl.Value) : null;
                });
        }

        return model;
    }

    /// <summary>
    /// Build a BodyLayer instance from this model, evaluating build-time expressions.
    /// </summary>
    public BodyLayer Build(BodyPart part, EvalContext ctx, Dictionary<string, BodyModel.StatConfig> bodyStats, Expr<float> healthMultiplier, Expr<float> sensitivity)
    {
        var layer = new BodyLayer(part)
        {
            Name = Name,
            BloodFlow = BloodFlow.Eval(),
            MaxHealth = MaxHealth.Eval() * healthMultiplier.Eval(ctx),
            RegenerationRate = Regen,
            OnHeal = OnHeal ?? new NoEffectExpr(),
            PainMultiplier = new MulExpr(Pain, sensitivity),
            KillsOnZeroHealth = Vital,
            BypassLayer = Bypass,
            DamageModifiers = new(DamageModifiers),
            PenetrationResistance = new(PenetrationResistance),
        };

        // Resolve providedStats from raw configs
        foreach (var (statName, mods) in BodyPartModel.PartStatModifierConfig.ResolveAll(ProvidedStatsRaw, bodyStats))
            layer.ProvidedStats[statName] = mods;

        // Evaluate pre-baked injuries
        foreach (var (condition, injuryModel) in InjuryModels)
        {
            if (condition != null && !condition.Eval(ctx)) continue;
            layer.Injuries.Add(injuryModel.Eval(ctx));
        }

        return layer;
    }
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

        /// <summary>
        /// Parse a list of <see cref="StatModifierJson"/> into per-stat config lists,
        /// skipping entries without a stat name. Shared by body parts and layers.
        /// </summary>
        public static Dictionary<string, List<PartStatModifierConfig>> ParseList(List<StatModifierJson>? providedStats)
        {
            var result = new Dictionary<string, List<PartStatModifierConfig>>();
            if (providedStats == null) return result;
            foreach (var stat in providedStats)
            {
                if (string.IsNullOrWhiteSpace(stat.Stat)) continue;
                var cfg = new PartStatModifierConfig
                {
                    StatName = stat.Stat!,
                    AtFullJson = stat.AtFull,
                    AtZeroJson = stat.AtZero,
                    StandaloneHpOnly = stat.StandaloneHPOnly,
                    Operation = JsonHelpers.ParseOp(stat.Operation, StatModifierType.Flat),
                };
                if (!result.TryGetValue(cfg.StatName, out var list))
                    result[cfg.StatName] = list = new List<PartStatModifierConfig>();
                list.Add(cfg);
            }
            return result;
        }

        /// <summary>
        /// Resolve parsed configs into <see cref="BodyProvidedStat"/> arrays at build time.
        /// A stat is applied to the owner unless the body declares it as a local stat.
        /// </summary>
        public static Dictionary<string, BodyProvidedStat[]> ResolveAll(
            Dictionary<string, List<PartStatModifierConfig>> configs,
            Dictionary<string, BodyModel.StatConfig> bodyStats)
        {
            var result = new Dictionary<string, BodyProvidedStat[]>(configs.Count);
            foreach (var (statName, cfgs) in configs)
            {
                var bodyStat = bodyStats.GetValueOrDefault(statName);
                var applyToOwner = bodyStat == null || !bodyStat.IsLocal;
                var mods = new BodyProvidedStat[cfgs.Count];
                for (int i = 0; i < cfgs.Count; i++)
                {
                    var cfg = cfgs[i];
                    float atFull = cfg.AtFullJson != null ? JsonHelpers.GetFloat(cfg.AtFullJson.Value) : 0;
                    float atZero = cfg.AtZeroJson != null ? JsonHelpers.GetFloat(cfg.AtZeroJson.Value) : 0;
                    mods[i] = new BodyProvidedStat(atFull, atZero, cfg.Operation, cfg.StandaloneHpOnly, applyToOwner);
                }
                result[statName] = mods;
            }
            return result;
        }
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
    public float PainMultiplier;
    public Expr<float> HealthMultiplier = new ConstNumberExpr(1);
    public Expr<float> Sensitivity = new ConstNumberExpr(1);
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
    /// Features that apply to the owner when this part is present.
    /// </summary>
    public List<Feature> OwnerFeatures = new();
    /// <summary>
    /// Features that apply to this body part itself.
    /// </summary>
    public List<Feature> SelfFeatures = new();
    /// <summary>
    /// Stats provided by this body part (not tied to layers).
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
    /// <summary>
    /// Name of the layer profile to use, or null if no profile.
    /// </summary>
    public string? LayersProfileName;
    /// <summary>
    /// Inline layer profile definition parsed from the body part JSON.
    /// If set, this is registered as an anonymous profile during ResolveLayers.
    /// </summary>
    public BodyLayerProfileJson? InlineLayerProfile;
    /// <summary>
    /// The resolved layer models for this body part, ordered outermost to innermost.
    /// </summary>
    public List<BodyLayerModel> LayerModels = new();
    /// <summary>
    /// A condition that determines whether this body part is present on the built Body. Evaluated at build time.
    /// </summary>
    public Expr<bool> Condition = new ConstConditionExpr(true);
    public Expr<bool> VitalForGroup = new ConstConditionExpr(true);
    

    public BodyPartModel(BodyPartJson jsonModel)
    {
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyPartJson");

        Name = jsonModel.Name ?? "unnamed";
        Group = jsonModel.Group ?? "default";
        PainMultiplier = jsonModel.PainMultiplier;
        HealthMultiplier = ExpressionCompiler.CompileOr(jsonModel.HealthMultiplier, HealthMultiplier);
        Sensitivity = ExpressionCompiler.CompileOr(jsonModel.Sensitivity, Sensitivity);
        EquipmentSlots = jsonModel.Slots ?? new();
        Condition = ExpressionCompiler.CompileOr(jsonModel.Condition, Condition);
        VitalForGroup = ExpressionCompiler.CompileOr(jsonModel.VitalForGroup, VitalForGroup);
        // parse part-level provided stats
        ProvidedStats = PartStatModifierConfig.ParseList(jsonModel.ProvidedStats);

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

        Skills = Compendium.ResolveEntries<Skill>(jsonModel.Skills, "skill name in BodyPart JSON").ToArray();
        SelfFeatures = Compendium.ResolveEntries<Feature>(jsonModel.SelfFeatures, "feature in JSON");
        OwnerFeatures = Compendium.ResolveEntries<Feature>(jsonModel.Features, "creature feature in JSON");

        // Parse layer profile reference
        if (jsonModel.LayersProfile.HasValue)
        {
            var lpEl = jsonModel.LayersProfile.Value;
            if (lpEl.ValueKind == JsonValueKind.String)
            {
                // Just a profile name reference
                LayersProfileName = lpEl.GetString();
            }
            else if (lpEl.ValueKind == JsonValueKind.Object)
            {
                // Inline profile definition — parsed as a BodyLayerProfileJson.
                // Its "extends" property references a named profile parent,
                // and its "layers" merge on top via the normal profile inheritance.
                InlineLayerProfile = lpEl.Deserialize<BodyLayerProfileJson>();
            }
        }

        if (jsonModel.Children != null)
        {
            foreach (var childJson in jsonModel.Children)
            {
                var child = new BodyPartModel(childJson);
                Children.Add(child);
            }
        }

        Metadata = jsonModel.Metadata;
    }
    public BodyPartModel(JsonElement json) : this(json.Deserialize<BodyPartJson>() ?? throw new Exception("Failed to deserialize BodyPartJson"))
    {
    }

    /// <summary>
    /// Resolve layers from the profile registry + per-part overrides.
    /// Must be called after BodyModel has parsed all layerProfiles.
    /// </summary>
    public void ResolveLayers(Dictionary<string, ResolvedLayerProfile> profiles)
    {
        if (LayersProfileName == null && InlineLayerProfile == null)
        {
            // No explicit profile — fall back to "default" if one exists
            if (profiles.ContainsKey("default"))
                LayersProfileName = "default";
            else
            {
                foreach (var child in Children)
                    child.ResolveLayers(profiles);
                return;
            }
        }

        ResolvedLayerProfile? resolved = null;

        if (InlineLayerProfile != null)
        {
            // Resolve the inline profile through the same inheritance path as named
            // profiles (its "extends" is looked up against the already-resolved set).
            resolved = ResolveOneProfile(InlineLayerProfile, profiles);
        }
        else if (LayersProfileName != null)
        {
            if (!profiles.TryGetValue(LayersProfileName, out resolved))
                Logger.LogWarning($"Layer profile '{LayersProfileName}' not found for body part '{Name}'");
        }

        if (resolved != null)
        {
            LayerModels.Clear();
            foreach (var layerName in resolved.Order)
            {
                if (!resolved.Layers.TryGetValue(layerName, out var layerObj)) continue;
                var layerElement = JsonDocument.Parse(layerObj.ToJsonString()).RootElement;
                LayerModels.Add(BodyLayerModel.FromJson(layerName, layerElement));
            }
        }

        // Recurse into children
        foreach (var child in Children)
            child.ResolveLayers(profiles);
    }

    /// <summary>
    /// Resolve a single BodyLayerProfileJson using already-resolved profiles for inheritance.
    /// </summary>
    internal static ResolvedLayerProfile ResolveOneProfile(
        BodyLayerProfileJson raw,
        Dictionary<string, ResolvedLayerProfile> resolvedProfiles)
    {
        var profile = new ResolvedLayerProfile();

        // Inherit from parent if "extends" is set
        if (!string.IsNullOrWhiteSpace(raw.Extends))
        {
            if (resolvedProfiles.TryGetValue(raw.Extends, out var parent))
            {
                profile.Order = new(parent.Order);
                foreach (var (name, obj) in parent.Layers)
                    profile.Layers[name] = (JsonObject)obj.DeepClone();
            }
            else
            {
                Logger.LogWarning($"Layer profile parent '{raw.Extends}' not found");
            }
        }

        // Apply this profile's own order
        if (raw.Order != null)
            profile.Order = new(raw.Order);
        else if (profile.Order.Count == 0)
            profile.Order = new(raw.Layers.Keys);

        // Merge this profile's layers on top
        foreach (var (layerName, layerDef) in raw.Layers)
        {
            var layerObj = JsonNode.Parse(JsonSerializer.Serialize(layerDef))?.AsObject() ?? new JsonObject();
            if (profile.Layers.TryGetValue(layerName, out var existingLayer))
                profile.Layers[layerName] = JsonHelpers.Merge(existingLayer, layerObj);
            else
                profile.Layers[layerName] = layerObj;
        }

        return profile;
    }

    public EntityWith<BodyPart> Build(BodyModel body, EvalContext? ctx = null)
    {
        if (ctx == null)
            ctx = new EvalContext();
        // Build children first
        var childParts = new List<BodyPart>(Children.Count);
        foreach (var child in Children)
        {
            if (child.Condition.Eval(ctx))
                childParts.Add(child.Build(body, ctx).Component);
        }

        // Construct the BodyPart entity
        Entity bpEntity = new Entity(Name);
        bpEntity.AddComponent(new CustomDataComponent()
        {
            Entity = bpEntity
        });
        var statsComponent = new StatsContainer()
        {
            Entity = bpEntity
        };
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
            Tags.ToArray(),
            childParts.ToArray()
        )
        {
            Entity = bpEntity,
            VitalForGroup = VitalForGroup.Eval(ctx)
        };
        bpEntity.AddComponent(ret);

        // apply part-level provided stats to the component
        foreach (var (statName, mods) in PartStatModifierConfig.ResolveAll(ProvidedStats, body.Stats))
            ret.ProvidedStats[statName] = mods;

        // Build layers
        var builtLayers = new BodyLayer[LayerModels.Count];
        var layerNameIndex = new Dictionary<string, int>(LayerModels.Count);
        for (int i = 0; i < LayerModels.Count; i++)
        {
            var layerModel = LayerModels[i];
            builtLayers[i] = layerModel.Build(ret, ctx, body.Stats, HealthMultiplier, Sensitivity);
            layerNameIndex[layerModel.Name] = i;
        }
        ret.layers = builtLayers;
        ret.layersByName = layerNameIndex;

        foreach (var statEntry in Stats)
        {
            statsComponent.CreateStat(new Stat(statEntry.Key, statEntry.Value, 0, 100, false, false));
        }

        // Add self-applied features to the part
        foreach (var feat in SelfFeatures)
            featuresComponent.AddFeature(feat);

        return new EntityWith<BodyPart>(bpEntity, ret);
    }
}

/// <summary>
/// A fully resolved layer profile: the final merged JSON objects for each layer, in order.
/// </summary>
public class ResolvedLayerProfile
{
    public List<string> Order = new();
    public Dictionary<string, JsonObject> Layers = new();
}

public class BodyModel
{
    public class StatConfig
    {
        public Expr<float>? BaseVal;
        public Expr<float>? MaxVal;
        public Expr<float>? MinVal;
        public bool OverCap = false;
        public bool UnderCap = false;
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


    public string Id;
    public string Name = null!;
    public List<Expr<string>> Tags = new();
    public List<Feature> Features = new();
    public Expr<float> Height;
    public List<BodyPosture> AvailablePostures = new();
    public BodyPosture RestingPosture;
    public Dictionary<string, StatConfig> Stats = new();
    public Dictionary<string, ResolvedLayerProfile> LayerProfiles = new();
    public List<EffectExpr> PreBuild = new();
    public List<EffectExpr> OnBuild = new();
    public List<EffectExpr> OnHealthChange = new();
    public bool SexualDimorphism = false;
    public BodyPartModel Root = null!;

    public BodyModel(string id, JsonElement json)
    {
        Id = id;
        originalJson = json;
        var jsonModel = json.Deserialize<BodyJson>()
            ?? throw new Exception("Failed to deserialize BodyJson");

        ParseHeader(jsonModel);
        ParsePostures(jsonModel);
        OnBuild = CompileEffectList(jsonModel.OnBuild, "onBuild");
        PreBuild = CompileEffectList(jsonModel.PreBuild, "preBuild");
        OnHealthChange = CompileEffectList(jsonModel.OnHealthChange, "onHealthChange");
        LayerProfiles = ResolveLayerProfiles(jsonModel.LayerProfiles);

        if (jsonModel.Root == null) throw new Exception("Root is required");
        Root = new BodyPartModel(jsonModel.Root);
        // Resolve layers for all body parts now that profiles are available.
        Root.ResolveLayers(LayerProfiles);

        Features = Compendium.ResolveEntries<Feature>(jsonModel.Features, "creature feature in JSON");
        ParseStats(jsonModel);
    }

    private void ParseHeader(BodyJson jsonModel)
    {
        Name = jsonModel.Name ?? "unnamed";
        Tags = jsonModel.Tags?.Select(ExpressionCompiler.Compile<string>).ToList() ?? new List<Expr<string>>();
        SexualDimorphism = jsonModel.SexualDimorphism;
        Height = ExpressionCompiler.CompileOr(jsonModel.Height, new ConstNumberExpr(1.5f));
        if (SexualDimorphism)
        {
            // All bodies with sexual dimorphism get a tag based on the sex of the built creature.
            Tags.Add(new ConditionalExpr<string>(
                new EqualConditionExpr(new VarExpr<bool>(0), new ConstConditionExpr(true)),
                new StringLiteralExpr("female"),
                new StringLiteralExpr("male")
            ));
        }
    }

    private void ParsePostures(BodyJson jsonModel)
    {
        if (jsonModel.Postures != null)
        {
            foreach (var postureEl in jsonModel.Postures)
            {
                var postureId = ExpressionCompiler.Compile<string>(postureEl).Eval();
                var entry = Compendium.GetEntry<BodyPosture>(postureId);
                if (entry == null)
                    Logger.LogWarning($"Invalid body posture in JSON: {postureId}");
                else
                    AvailablePostures.Add(entry);
            }
        }

        if (!jsonModel.RestingPosture.HasValue)
            throw new Exception("Resting posture is required for BodyModel");
        var restingId = ExpressionCompiler.Compile<string>(jsonModel.RestingPosture.Value).Eval();
        RestingPosture = Compendium.GetEntry<BodyPosture>(restingId)
            ?? throw new Exception($"Invalid resting posture in JSON: {restingId}");
    }

    /// <summary>Compile an optional list of effect expressions, logging (but not throwing on) failures.</summary>
    private static List<EffectExpr> CompileEffectList(List<JsonElement>? elements, string label)
    {
        var result = new List<EffectExpr>();
        if (elements == null) return result;
        foreach (var el in elements)
        {
            try { result.Add(ExpressionCompiler.CompileEffect(el)); }
            catch (Exception e) { Logger.LogError($"[BodyModel] Could not parse {label} effect: {e}"); }
        }
        return result;
    }

    /// <summary>Resolve layer profiles, following "extends" chains topologically.</summary>
    private static Dictionary<string, ResolvedLayerProfile> ResolveLayerProfiles(Dictionary<string, BodyLayerProfileJson>? rawInput)
    {
        var resolved = new Dictionary<string, ResolvedLayerProfile>();
        if (rawInput == null) return resolved;
        var rawProfiles = new Dictionary<string, BodyLayerProfileJson>(rawInput);

        ResolvedLayerProfile Resolve(string profileName)
        {
            if (resolved.TryGetValue(profileName, out var existing))
                return existing;
            if (!rawProfiles.TryGetValue(profileName, out var raw))
            {
                Logger.LogWarning($"Layer profile '{profileName}' not found");
                return new ResolvedLayerProfile();
            }

            // Resolve parent first if needed
            if (!string.IsNullOrWhiteSpace(raw.Extends))
                Resolve(raw.Extends);

            var profile = BodyPartModel.ResolveOneProfile(raw, resolved);
            resolved[profileName] = profile;
            return profile;
        }

        foreach (var profileName in rawProfiles.Keys)
            Resolve(profileName);
        return resolved;
    }

    private void ParseStats(BodyJson jsonModel)
    {
        if (jsonModel.Stats == null) return;
        foreach (var (statName, statJson) in jsonModel.Stats)
            Stats[statName] = ParseStat(statName, statJson);
    }

    private static StatConfig ParseStat(string statName, StatJson statJson)
    {
        string? maxDep = statJson.Max is { ValueKind: JsonValueKind.String } max ? max.GetString() : null;
        string? minDep = statJson.Min is { ValueKind: JsonValueKind.String } min ? min.GetString() : null;

        var groupEffectiveness = statJson.GroupEffectiveness != null
            ? new Dictionary<string, float>(statJson.GroupEffectiveness)
            : new Dictionary<string, float>();

        return new StatConfig
        {
            BaseVal = statJson.Base.HasValue ? ExpressionCompiler.Compile<float>(statJson.Base.Value) : null,
            MaxVal = statJson.Max.HasValue ? ExpressionCompiler.Compile<float>(statJson.Max.Value) : null,
            MinVal = statJson.Min.HasValue ? ExpressionCompiler.Compile<float>(statJson.Min.Value) : null,
            OverCap = statJson.OverCap,
            UnderCap = statJson.UnderCap,
            Aliases = statJson.Aliases?.ToArray() ?? Array.Empty<string>(),
            Vital = statJson.Vital,
            MaxDependencyName = maxDep,
            MinDependencyName = minDep,
            RegenJson = statJson.Regen,
            DependsOn = ParseDependencies(statName, statJson.DependsOn),
            GroupEffectiveness = groupEffectiveness,
            Name = statJson.Name,
            OnChange = statJson.OnChange,
            Thresholds = ParseThresholds(statName, statJson.Thresholds),
            IsLocal = statJson.Local
        };
    }

    private static List<BodyStat.StatDependency>? ParseDependencies(string statName, Dictionary<string, JsonElement>? dependsOn)
    {
        if (dependsOn == null) return null;
        var result = new List<BodyStat.StatDependency>();
        foreach (var (depName, valElement) in dependsOn)
        {
            if (valElement.ValueKind == JsonValueKind.Number)
            {
                // Weighted dependency shorthand, expands to:
                // -((1 - ((newVal - depStat.MinValue) / (depStat.MaxValue - depStat.MinValue))) * weight)
                Expr<float> expr = new MulExpr(
                    new SubExpr(
                        new ConstNumberExpr(1),
                        new DivExpr(
                            new SubExpr(
                                new VarExpr<float>(0),
                                new StatMinExpr(depName, new CallerEntityExpr())
                            ),
                            new SubExpr(
                                new StatMaxExpr(depName, new CallerEntityExpr()),
                                new StatMinExpr(depName, new CallerEntityExpr())
                            )
                        )
                    ),
                    new ConstNumberExpr((float)valElement.GetDouble()),
                    new ConstNumberExpr(-1)
                );
                result.Add(new BodyStat.StatDependency(depName, expr, new EnumExpr<StatModifierType>(StatModifierType.Percent)));
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
                result.Add(new BodyStat.StatDependency(
                    depName,
                    ExpressionCompiler.Compile<float>(numberEl.Value),
                    ExpressionCompiler.Compile<StatModifierType>(typeEl.Value)
                ));
            }
            else
                Logger.LogWarning($"Invalid dependsOn value for stat {statName} and dependency {depName}: must be a number or an object");
        }
        return result;
    }

    private static List<BodyStat.StatThreshold> ParseThresholds(string statName, List<JsonElement>? thresholdsJson)
    {
        var thresholds = new List<BodyStat.StatThreshold>();
        if (thresholdsJson == null) return thresholds;
        foreach (var thresholdEl in thresholdsJson)
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

            Expr<bool> condition = ExpressionCompiler.Compile<bool>(conditionEl.Value);
            EffectExpr effect = CompileThresholdEffect(effectEl.Value, isReverse: false);
            EffectExpr? unapplyEffect = unapplyEl.HasValue ? CompileThresholdEffect(unapplyEl.Value, isReverse: true) : null;

            thresholds.Add(new BodyStat.StatThreshold(condition, effect, unapplyEffect));
        }
        return thresholds;
    }

    /// <summary>A threshold effect may be a feature id (add on apply, remove on reverse) or a full effect expression.</summary>
    private static EffectExpr CompileThresholdEffect(JsonElement el, bool isReverse)
    {
        if (el.ValueKind == JsonValueKind.String && Compendium.EntryExists<Feature>(el.GetString()!))
        {
            var feature = new CompendiumEntryExpr<Feature>(el.GetString()!);
            return isReverse
                ? new RemoveFeatureEffect(feature, new TargetEntityExpr())
                : new AddFeatureEffect(feature, new TargetEntityExpr());
        }
        return ExpressionCompiler.CompileEffect(el);
    }

    public JsonElement GetOriginalJson()
    {
        return originalJson;
    }

    public EntityWith<Body> Build()
    {
        bool isFemale = SexualDimorphism && new Random().Next(2) == 0;
        var variables = new object[16];
        variables[0] = isFemale;
        EvalContext EvalContext = new()
        {
            Variables = variables
        };
        foreach (var effect in PreBuild)
            effect.Eval(EvalContext);
        foreach (var effect in OnBuild)
            effect.Eval(EvalContext);
        BodyPart rootPart = Root.Build(this, EvalContext).Component;
        var entity = new Entity(Name);
        entity.AddComponent(new CustomDataComponent() { Entity = entity });
        var stats = new StatsContainer() { Entity = entity };
        stats.CreateStat(new Stat("height", Height.Eval(EvalContext)));
        entity.AddComponent(stats);
        entity.AddComponent(new FeaturesContainer() { Entity = entity });


        List<BodyStat> statsList = new();
        foreach (var (statName, cfg) in Stats)
        {
            float baseVal = cfg.BaseVal != null ? cfg.BaseVal.Eval(EvalContext) : 0;
            float maxVal = float.MaxValue;
            if (cfg.MaxVal != null)
                maxVal = cfg.MaxVal.Eval();
            float minVal = 0;
            if (cfg.MinVal != null)
                minVal = cfg.MinVal.Eval();

            var stat = new Stat(statName, baseVal, minVal, maxVal, cfg.OverCap, cfg.UnderCap)
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
                entry.Regen = ExpressionCompiler.Compile<float>(cfg.RegenJson.Value);
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
            Tags = [.. Tags.Select(t => t.Eval(EvalContext)), isFemale ? "female" : "male"],
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
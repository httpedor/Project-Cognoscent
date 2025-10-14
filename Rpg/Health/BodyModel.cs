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
    public string Name { get; set; }
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
    public Dictionary<string, List<(JsonNode? AtFullJson, JsonNode? AtZeroJson, bool Sho, StatModifierType Op, bool ApplyToOwner)>> Stats { get; set; } = new();
    public Dictionary<DamageType, List<(JsonNode? ValueJson, StatModifierType Operation)>> DamageModifiers { get; set; } = new();
    public JsonObject? Metadata { get; set; }

    public BodyPartModel(JsonObject json)
    {
        var jsonModel = JsonSerializer.Deserialize<BodyPartJson>(json.ToString());
        if (jsonModel == null) throw new Exception("Failed to deserialize BodyPartJson");

        Name = jsonModel.Name ?? "unnamed";
        Group = jsonModel.Group ?? "default";
        MaxHealthJson = JsonNode.Parse(jsonModel.MaxHealth.GetRawText());
        AreaJson = JsonNode.Parse(jsonModel.Area.GetRawText());
        PainMultiplier = jsonModel.PainMultiplier;
        EquipmentSlots = jsonModel.Slots ?? new();

        if (jsonModel.Stats != null)
        {
            foreach (var stat in jsonModel.Stats)
            {
                if (stat.Stat == null) continue;
                string statName = stat.Stat;
                JsonNode? atFullJson = JsonNode.Parse(stat.AtFull.GetRawText());
                JsonNode? atZeroJson = stat.AtZero.HasValue ? JsonNode.Parse(stat.AtZero.Value.GetRawText()) : JsonValue.Create(0f);
                bool sho = stat.StandaloneHPOnly;
                StatModifierType op = Enum.TryParse<StatModifierType>(stat.Operation ?? "Flat", true, out var operation) ? operation : StatModifierType.Flat;

                if (!Stats.ContainsKey(statName))
                    Stats[statName] = new List<(JsonNode?, JsonNode?, bool, StatModifierType, bool)>();
                Stats[statName].Add((atFullJson, atZeroJson, sho, op, true));
            }
        }

        if (jsonModel.DamageModifiers != null)
        {
            foreach (var dmg in jsonModel.DamageModifiers)
            {
                if (dmg.DamageType == null) continue;
                if (DamageType.FromName(dmg.DamageType) is not DamageType dt)
                {
                    Console.WriteLine("Warning: Invalid damage type: " + dmg.DamageType);
                    continue;
                }
                JsonNode? valueJson = JsonNode.Parse(dmg.Value.GetRawText());
                StatModifierType operation = string.IsNullOrEmpty(dmg.Operation) ? StatModifierType.Percent : Enum.Parse<StatModifierType>(dmg.Operation);

                if (!DamageModifiers.ContainsKey(dt))
                    DamageModifiers[dt] = new List<(JsonNode?, StatModifierType)>();
                DamageModifiers[dt].Add((valueJson, operation));
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
                    Console.WriteLine("Warning: Invalid skill name in BodyPart JSON: " + skillName);
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
                    Console.WriteLine("Warning: Invalid feature in JSON: " + name);
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
                    Console.WriteLine("Warning: Invalid creature feature in JSON: " + name);
                else
                    OwnerFeatures.Add(feature);
            }
        }

        if (jsonModel.Children != null)
        {
            foreach (var childJson in jsonModel.Children)
            {
                var childJsonObj = JsonNode.Parse(JsonSerializer.Serialize(childJson)).AsObject()!;
                var child = new BodyPartModel(childJsonObj);
                Children.Add(child);
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
        {
            builder.Child(child.Build(body));
        }

        foreach (var statEntry in Stats)
        {
            foreach (var (atFullJson, atZeroJson, sho, op, applyToOwner) in statEntry.Value)
            {
                float atFull = atFullJson != null ? JsonModel.GetFloat((JsonValue)atFullJson) : 0;
                float atZero = atZeroJson != null ? JsonModel.GetFloat((JsonValue)atZeroJson) : 0;
                builder.Stat(statEntry.Key, atFull, atZero, op, sho, applyToOwner);
            }
        }

        foreach (var dmgEntry in DamageModifiers)
        {
            foreach (var (valueJson, operation) in dmgEntry.Value)
            {
                float value = valueJson != null ? JsonModel.GetFloat((JsonValue)valueJson) : 0;
                builder.DamageModifier(dmgEntry.Key, value, operation);
            }
        }

        var ret = builder.Build();

        // Apply metadata if any
        if (Metadata != null)
        {
            // Assuming CustomDataFromJson exists, but since it's not shown, perhaps implement or skip
            // ret.CustomDataFromJson(Metadata);
        }

        ret.Body = body;
        return ret;
    }
}

public class BodyModel
{
    public string Name { get; set; } = null!;
    public bool IsHumanoid { get; set; }
    public List<Feature> Features { get; set; } = new();
    public Dictionary<string, (JsonNode? BaseJson, JsonNode? MaxJson, JsonNode? MinJson, bool OverCap, bool UnderCap, string[] Aliases, bool Vital, string? MaxDependencyName, string? MinDependencyName, JsonNode? RegenJson, List<(string DepName, JsonNode? ValJson)>? DependsOn)> Stats { get; set; } = new();
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
                    Console.WriteLine("Warning: Invalid creature feature in JSON: " + name);
                else
                    Features.Add(feature);
            }
        }

        if (jsonModel.Stats != null)
        {
            foreach (var (statName, statJson) in jsonModel.Stats)
            {
                JsonNode? baseJson = JsonNode.Parse(statJson.Base.GetRawText());
                JsonNode? maxJson = statJson.Max.HasValue ? JsonNode.Parse(statJson.Max.Value.GetRawText()) : null;
                JsonNode? minJson = statJson.Min.HasValue ? JsonNode.Parse(statJson.Min.Value.GetRawText()) : null;
                bool overCap = statJson.OverCap;
                bool underCap = statJson.UnderCap;
                string[] aliases = statJson.Aliases?.ToArray() ?? Array.Empty<string>();
                bool vital = statJson.Vital;
                string? maxDependencyName = null;
                if (maxJson != null && maxJson.GetValueKind() == JsonValueKind.String)
                    maxDependencyName = maxJson.GetValue<string>();
                string? minDependencyName = null;
                if (minJson != null && minJson.GetValueKind() == JsonValueKind.String)
                    minDependencyName = minJson.GetValue<string>();
                JsonNode? regenJson = statJson.Regen.HasValue ? JsonNode.Parse(statJson.Regen.Value.GetRawText()) : null;
                List<(string DepName, JsonNode? ValJson)>? dependsOn = null;
                if (statJson.DependsOn != null)
                {
                    dependsOn = new List<(string, JsonNode?)>();
                    foreach (var (depName, valElement) in statJson.DependsOn)
                    {
                        JsonNode? valJson = JsonNode.Parse(valElement.GetRawText());
                        dependsOn.Add((depName, valJson));
                    }
                }

                Stats[statName] = (baseJson, maxJson, minJson, overCap, underCap, aliases, vital, maxDependencyName, minDependencyName, regenJson, dependsOn);
            }
        }
    }

    public Body Build()
    {
        BodyPart rootPart = Root.Build();
        var body = new Body(Name, rootPart, IsHumanoid);
        foreach (var feature in Features)
        {
            body.features.Add(feature);
        }
        foreach (var statEntry in Stats)
        {
            var (baseJson, maxJson, minJson, overCap, underCap, aliases, vital, maxDependencyName, minDependencyName, regenJson, dependsOn) = statEntry.Value;
            float baseVal = baseJson != null ? JsonModel.GetFloat((JsonValue)baseJson) : 0;
            float maxVal = float.MaxValue;
            if (maxJson?.GetValueKind() == JsonValueKind.Number)
                maxVal = JsonModel.GetFloat((JsonValue)maxJson);
            float minVal = 0;
            if (minJson?.GetValueKind() == JsonValueKind.Number)
                minVal = JsonModel.GetFloat((JsonValue)minJson);
            var stat = new Stat(statEntry.Key, baseVal, maxVal, minVal, overCap, underCap)
            {
                Aliases = aliases
            };
            var entry = new Body.StatEntry(stat);
            if (vital)
                entry.Vital = true;
            if (maxDependencyName != null)
                entry.MaxDependencyName = maxDependencyName;
            if (minDependencyName != null)
            {
                // represent min dependency as a dependency that applies a Capmin modifier
                entry.Dependencies = new StatDependency[] { (statEntry.Key + "_min_dep", new Either<string, float>(minDependencyName), (Func<float, float, (float, StatModifierType)>?)((x, _) => (x, StatModifierType.Capmin))) };
            }
            if (regenJson != null)
            {
                if (regenJson.GetValueKind() == JsonValueKind.String)
                    entry.Regen = regenJson.GetValue<string>();
                else if (regenJson.GetValueKind() == JsonValueKind.Number)
                    entry.Regen = JsonModel.GetFloat((JsonValue)regenJson);
                else
                    Console.WriteLine("Warning: Invalid regen value for stat " + statEntry.Key);
            }
            if (dependsOn != null)
            {
                var deps = new List<StatDependency>();
                foreach (var (depName, valJson) in dependsOn)
                {
                    Either<string, float> val;
                    if (valJson?.GetValueKind() == JsonValueKind.Number)
                        val = new Either<string, float>(JsonModel.GetFloat((JsonValue)valJson));
                    else
                        val = new Either<string, float>(valJson!.GetValue<string>());
                    if (SidedLogic.Instance.IsClient() || val.IsRight)
                        deps.Add((depName, val, null));
                    else
                        deps.Add((depName, val, Body.Compile(val.Left)));
                }
                if (deps.Count > 0)
                    entry.Dependencies = deps.ToArray();
            }
            body.stats[statEntry.Key] = entry;
        }

        foreach (var deps in body.stats.Where(kv => kv.Value.Dependencies != null))
        {
            foreach (var dep in deps.Value.Dependencies!)
            {
                if (!body.stats.ContainsKey(dep.stat))
                {
                    Console.WriteLine($"Warning: stat {deps.Key} depends on stat {dep.stat} but it wasn't defined in this body!");
                }
            }
        }

        return body;
    }
}
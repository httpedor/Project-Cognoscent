using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities.Components.Health;
using Rpg.Scripting;

namespace Rpg.Health;

using CreationEntry = (Expr<bool> condition, InjuryModel injuryModel, float interval);

//TODO: Injury treatments. For example, bandaged, cooled, disinfected, etc.
// Each injury type can then interpret these treatments differently.
// E.g: A burn might need to be cooled to heal faster, or bandaged to reduce infection chance. Or a cut might need to be bandaged to reduce bleeding.
public class InjuryType : ISerializable, ITaggable
{
    public readonly string Id;
    /// <summary>
    /// Pain value per severity
    /// </summary>
    public readonly Expr<float> Pain;
    /// <summary>
    /// Bleed value per severity
    /// </summary>
    public readonly Expr<float> BleedingRate;
    /// <summary>
    /// Minimum overkill percentage to kill the part
    /// </summary>
    public readonly Expr<float> OverkillPercentMin;
    /// <summary>
    /// Overkill percentage that makes sure the part will be destroyed
    /// </summary>
    public readonly Expr<float> OverkillPercentMax;
    /// <summary>
    /// This means the part is not workign, E.g: Broken, Missing, Bloodless
    /// </summary>
    public readonly Expr<bool> Instakill;
    /// <summary>
    /// The rate at which the severity of this injury lowers every second.
    /// </summary>
    public readonly Expr<float> NaturalHeal;
    /// <summary>
    /// Every <c>interval</c> seconds, the <c>injury</c> is added if <c>condition</c> is true.
    /// If the function returns null, no injury is created.
    /// </summary>
    public readonly ImmutableArray<CreationEntry> InjuryCreations = [];
    /// <summary>
    /// Every <c>interval</c> seconds, the <c>injury</c> substitutes the current injury if <c>condition</c> is true.
    /// If the function returns null, no conversion occurs.
    /// </summary>
    public readonly ImmutableArray<CreationEntry> InjuryConversions = [];
    public readonly string Name;
    /// <summary>
    /// This is used in the last damage applied to the part befored it died is this
    /// </summary>
    public readonly string DestructionTranslation;
    public HashSet<string> Tags = new();

    HashSet<string> ITaggable.Tags { get => Tags; set => Tags = value; }

    public InjuryType(string id, JsonElement json)
    {
        Id = id;
        Name = json.GetProperty("name").GetString()!;
        DestructionTranslation = json.GetProperty("destruction").GetString()!;

        Pain = ExpressionCompiler.Compile<float>(json.GetProperty("pain"));
        BleedingRate = ExpressionCompiler.Compile<float>(json.GetProperty("bleed"));
        OverkillPercentMin = ExpressionCompiler.Compile<float>(json.GetProperty("overkillMin"));
        OverkillPercentMax = ExpressionCompiler.Compile<float>(json.GetProperty("overkillMax"));
        NaturalHeal = json.TryGetProperty("heal", out var healElement) ? ExpressionCompiler.Compile<float>(healElement) : new ConstNumberExpr(0);
        Instakill = json.TryGetProperty("instakill", out var instakillElement) ? ExpressionCompiler.Compile<bool>(instakillElement) : new ConstConditionExpr(false);

        if (json.TryGetProperty("creations", out var creationsEl) && creationsEl.ValueKind == JsonValueKind.Array)
        {
            List<CreationEntry> creations = new();
            foreach (var node in creationsEl.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    Logger.LogWarning("[InjuryType] Invalid injury creation entry in InjuryType " + Id);
                    continue;
                }

                try
                {
                    float interval = node.GetProperty("interval").GetSingle();
                    creations.Add((ExpressionCompiler.Compile<bool>(node.GetProperty("condition")),
                        new InjuryModel(node.GetProperty("injury")),
                        interval));
                }
                catch (Exception e)
                {
                    Logger.LogError("[InjuryType] Could not compile injury creation entry in InjuryType " + Id + ": " + e);
                }
            }
            InjuryCreations = creations.ToImmutableArray();
        }

        if (json.TryGetProperty("conversions", out var convEl) && convEl.ValueKind == JsonValueKind.Array)
        {
            List<CreationEntry> conversions = new();

            foreach (var node in convEl.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    Logger.LogWarning("[InjuryType] Invalid injury conversion entry in InjuryType " + Id);
                    continue;
                }

                try
                {
                    float interval = node.GetProperty("interval").GetSingle();
                    conversions.Add((ExpressionCompiler.Compile<bool>(node.GetProperty("condition")),
                        new InjuryModel(node.GetProperty("injury")),
                        interval));
                }
                catch (Exception e)
                {
                    Logger.LogError("[InjuryType] Could not compile injury conversion entry in InjuryType " + Id + ": " + e);
                }
            }
            InjuryConversions = conversions.ToImmutableArray();
        }

        if (json.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
            this.LoadTags(tagsArr);
    }

    public void ToBytes(Stream stream)
    {
        new CompendiumEntryRef<InjuryType>(Id).ToBytes(stream);
    }

    public static IEnumerable<InjuryType> GetInjuryTypes()
    {
        return Compendium.GetEntries<InjuryType>();
    }
    public static InjuryType? ByName(string translation)
    {
        return Compendium.FindEntry<InjuryType>(it => it.Name == translation);
    }
    public static InjuryType FromBytes(Stream stream)
    {
        return new CompendiumEntryRef<InjuryType>(stream).Get()!;
    }

    public override Int32 GetHashCode()
    {
        return Id.GetHashCode();
    }
}
public class InjuryModel
{
    public CompendiumEntryExpr<InjuryType> Type;
    public Expr<float> Severity;
    public InjuryModel(JsonElement json)
    {
        if (!json.TryGetProperty("type", out var typeEl))
            throw new Exception("InjuryModel deserialization requires a 'type' property.");
        Type = new CompendiumEntryExpr<InjuryType>(ExpressionCompiler.Compile<string>(typeEl));
        if (!json.TryGetProperty("severity", out var severityEl))
            throw new Exception("InjuryModel deserialization requires a 'severity' property.");
        Severity = ExpressionCompiler.Compile<float>(severityEl);
    }

    public InjuryModel(Stream stream)
    {
        Type = new CompendiumEntryExpr<InjuryType>(stream);
        Severity = BaseExpr.Deserialize<Expr<float>>(stream);
    }

    public void ToBytes(Stream stream)
    {
        Type.ToBytes(stream);
        Severity.ToBytes(stream);
    }

    public Injury Eval(EvalContext ctx)
    {
        var type = Type.Eval(ctx);
        if (type == null)
        {
            throw new Exception("InjuryModel evaluation resulted in null InjuryType.");
        }
        return new Injury(type, Severity.Eval(ctx));
    }
}
public struct Injury : ISerializable
{
    public InjuryType Type;
    public double Severity;


    public Injury(InjuryType type, double severity)
    {
        Type = type;
        Severity = severity;
    }

    public Injury(JsonElement json)
    {
        if (!json.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            throw new Exception("Injury deserialization requires a 'type' property of type string.");
        string typeId = typeEl.GetString()!;
        Type = Compendium.GetEntry<InjuryType>(typeId) ?? throw new Exception("InjuryType '" + typeId + "' not found in Injury deserialization.");
        if (!json.TryGetProperty("severity", out var severityEl) || severityEl.ValueKind != JsonValueKind.Number)
            throw new Exception("Injury deserialization requires a 'severity' property of type number.");
        Severity = severityEl.GetDouble();
    }

    public Injury(Stream stream)
    {
        Type = InjuryType.FromBytes(stream);
        Severity = stream.ReadDouble();
    }

    public void ToBytes(Stream stream)
    {
        Type.ToBytes(stream);
        stream.WriteDouble(Severity);
    }

    public override Boolean Equals(Object? obj)
    {
        return obj is Injury condition &&
               Type.Name == condition.Type.Name &&
               Math.Abs(Severity - condition.Severity) < 0.0001;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Type?.Name?.GetHashCode() ?? 0);
            hash = hash * 31 + Math.Round(Severity, 4).GetHashCode();
            return hash;
        }
    }

}
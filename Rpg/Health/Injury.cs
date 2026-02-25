using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities.Components.Health;
using Rpg.Scripting;

namespace Rpg.Health;

using CreationEntry = (Expr<bool> condition, Injury injury, float interval);

//TODO: Injury treatments. For example, bandaged, cooled, disinfected, etc.
// Each injury type can then interpret these treatments differently.
// E.g: A burn might need to be cooled to heal faster, or bandaged to reduce infection chance. Or a cut might need to be bandaged to reduce bleeding.
public class InjuryType : ISerializable
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

    public InjuryType(string id, JsonElement json)
    {
        Id = id;
        Name = json.GetProperty("name").GetString()!;
        DestructionTranslation = json.GetProperty("destruction").GetString()!;

        Pain = ExpressionCompiler.CompileNumber(json.GetProperty("pain"));
        BleedingRate = ExpressionCompiler.CompileNumber(json.GetProperty("bleed"));
        OverkillPercentMin = ExpressionCompiler.CompileNumber(json.GetProperty("overkillMin"));
        OverkillPercentMax = ExpressionCompiler.CompileNumber(json.GetProperty("overkillMax"));
        NaturalHeal = json.TryGetProperty("heal", out var healElement) ? ExpressionCompiler.CompileNumber(healElement) : new ConstNumberExpr(0);
        Instakill = json.TryGetProperty("instakill", out var instakillElement) ? ExpressionCompiler.CompileCondition(instakillElement) : new ConstConditionExpr(false);

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
                    creations.Add((ExpressionCompiler.CompileCondition(node.GetProperty("condition")),
                        new Injury(node.GetProperty("injury")),
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
                    conversions.Add((ExpressionCompiler.CompileCondition(node.GetProperty("condition")),
                        new Injury(node.GetProperty("injury")),
                        interval));
                }
                catch (Exception e)
                {
                    Logger.LogError("[InjuryType] Could not compile injury conversion entry in InjuryType " + Id + ": " + e);
                }
            }
            InjuryConversions = conversions.ToImmutableArray();
        }
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
        string typeId = json.GetProperty("type").GetString()!;
        Type = Compendium.GetEntry<InjuryType>(typeId) ?? throw new Exception("InjuryType '" + typeId + "' not found in Injury deserialization.");
        Severity = json.GetProperty("severity").GetDouble();
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
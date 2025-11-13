using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Rpg;

using ConversionEntry = (Func<Injury, BodyPart, Injury?> conversionFunc, float interval);
using CreationEntry = (Func<Injury, BodyPart, Injury?> creationFunc, float interval);

//TODO: Injury treatments. For example, bandaged, cooled, disinfected, etc.
// Each injury type can then interpret these treatments differently.
// E.g: A burn might need to be cooled to heal faster, or bandaged to reduce infection chance. Or a cut might need to be bandaged to reduce bleeding.
public class InjuryType : ISerializable
{
    private class CodeContext
    {
        Injury injury;
        BodyPart part;
        Dictionary<string, InjuryType> injuryTypes;
        public CodeContext(Injury injury, BodyPart part)
        {
            this.injury = injury;
            this.part = part;

            injuryTypes = Compendium.GetEntries<InjuryType>().ToDictionary(it => it.Id, it => it);
        }

        public float rand(float min = 0f, float max = 1f)
        {
            Random rng = new();
            return (float)(rng.NextDouble() * (max - min) + min);
        }
    }
    public readonly string Id;
    /// <summary>
    /// Pain value per severity
    /// </summary>
    public readonly float Pain;
    /// <summary>
    /// Bleed value per severity
    /// </summary>
    public readonly float BleedingRate;
    /// <summary>
    /// Minimum overkill percentage to kill the part
    /// </summary>
    public readonly float OverkillPercentMin;
    /// <summary>
    /// Overkill percentage that makes sure the part will be destroyed
    /// </summary>
    public readonly float OverkillPercentMax;
    /// <summary>
    /// This means the part is not workign, E.g: Broken, Missing, Bloodless
    /// </summary>
    public readonly bool Instakill;
    /// <summary>
    /// The rate at which the severity of this injury lowers every second.
    /// </summary>
    public readonly float NaturalHeal;
    /// <summary>
    /// Every <c>interval</c> seconds, the <c>creationFunc</c> is called to possibly create another injury.
    /// If the function returns null, no injury is created.
    /// </summary>
    public readonly ImmutableArray<CreationEntry> InjuryCreations = [];
    /// <summary>
    /// Every <c>interval</c> seconds, the <c>conversionFunc</c> is called to possibly convert this injury into another.
    /// If the function returns null, no conversion occurs.
    /// </summary>
    public readonly ImmutableArray<ConversionEntry> InjuryConversions = [];
    public readonly string Name;
    /// <summary>
    /// This is used in the last damage applied to the part befored it died is this
    /// </summary>
    public readonly string DestructionTranslation;
    private InjuryType(string id, float pain, float bleed, float opmin, float opmax, string translation, string destructionTranslation, float heal = 0, bool instakill = false)
    {
        Id = id;
        Pain = pain;
        BleedingRate = bleed;
        OverkillPercentMax = opmax;
        OverkillPercentMin = opmin;
        NaturalHeal = heal;
        Instakill = instakill;
        Name = translation;
        DestructionTranslation = destructionTranslation;
    }

    public InjuryType(string id, JsonObject json) : this(
        id,
        json["pain"]!.GetValue<float>(),
        json["bleed"]!.GetValue<float>(),
        json["overkillMin"]!.GetValue<float>(),
        json["overkillMax"]!.GetValue<float>(),
        json["name"]!.GetValue<string>(),
        json["destruction"]!.GetValue<string>(),
        json.ContainsKey("heal") ? json["heal"]!.GetValue<float>() : 0,
        json.ContainsKey("instakill") ? json["instakill"]!.GetValue<bool>() : false
    )
    {
        if (json["creations"] is JsonArray addArr)
        {
            List<CreationEntry> creations = new();
            foreach (var node in addArr)
            {
                if (node is not JsonObject o)
                {
                    Logger.LogWarning("[InjuryType] Invalid injury creation entry in InjuryType " + Id);
                    continue;
                }
                float? interval = o["interval"]?.GetValue<float>();
                string? code = o["code"]?.GetValue<string>();
                if (interval == null || string.IsNullOrWhiteSpace(code))
                {
                    Logger.LogWarning("[InjuryType] Invalid injury creation entry in InjuryType " + Id);
                    continue;
                }
                if (SidedLogic.Instance.IsClient())
                    continue;
                try
                {
                    var func = Scripting.Compile<CodeContext, Injury?>(code);
                    creations.Add(((injury, part) => func(new CodeContext(injury, part)), interval.Value));
                }
                catch (Exception e)
                {
                    Logger.LogError("[InjuryType] Could not compile injury creation script in InjuryType " + Id + ": " + e);
                }
            }
        }

        if (json["conversions"] is JsonArray convArr)
        {
            List<ConversionEntry> conversions = new();

            foreach (var node in convArr)
            {
                if (node is not JsonObject o)
                {
                    Logger.LogWarning("[InjuryType] Invalid injury conversion entry in InjuryType " + Id);
                    continue;
                }
                float? interval = o["interval"]?.GetValue<float>();
                string? code = o["code"]?.GetValue<string>();
                if (interval == null || string.IsNullOrWhiteSpace(code))
                {
                    Logger.LogWarning("[InjuryType] Invalid injury conversion entry in InjuryType " + Id);
                    continue;
                }
                if (SidedLogic.Instance.IsClient())
                    continue;
                try
                {
                    var func = Scripting.Compile<CodeContext, Injury?>(code);
                    conversions.Add(((injury, part) => func(new CodeContext(injury, part)), interval.Value));
                }
                catch (Exception e)
                {
                    Logger.LogError("[InjuryType] Could not compile injury conversion script in InjuryType " + Id + ": " + e);
                }
            }
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
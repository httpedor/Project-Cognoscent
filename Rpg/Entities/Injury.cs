
using Rpg;

public class InjuryType : ISerializable
{
    private static List<InjuryType> registeredTypes = new List<InjuryType>();
    private static Dictionary<string, InjuryType> perTranslation = new();
    public static readonly InjuryType Infection = register(new InjuryType(0, 0, 0, 0, "Infecção", "Morte", 1));
    public static readonly InjuryType Burn = register(new InjuryType(1.875f, 0, 40, 100, "Queimadura", "Queimado", 2));
    public static readonly InjuryType Bruise = register(new InjuryType(1.25f, 0, 40, 100, "Hematoma", "Estilhaçado", 1.25f));
    public static readonly InjuryType Cut = register(new InjuryType(1.25f, 0.06f, 0, 10, "Corte", "Cortado Fora", 1).AddInjuryCreationChance(new Injury(Infection, 1), 15));
    public static readonly InjuryType Stab = register(new InjuryType(1.25f, 0.06f, 40, 100, "Perfuração", "Perfurado").AddInjuryCreationChance(new Injury(Infection, 1), 15));
    public static readonly InjuryType Crack = register(new InjuryType(1, 0, 40, 100, "Rachadura", "Quebrado", 0.5f));

    /// <summary>
    /// Pain value per damage
    /// </summary>
    public float Pain;
    /// <summary>
    /// Bleed value per damage
    /// </summary>
    public float BleedingRate;
    /// <summary>
    /// Minimum overkill percentage to kill the part
    /// </summary>
    public float OverkillPercentMin;
    /// <summary>
    /// Overkill percentage that makes sure the part will be destroyed
    /// </summary>
    public float OverkillPercentMax;
    /// <summary>
    /// This means the part is not workign, E.g: Broken, Missing, Bloodless
    /// </summary>
    public bool Instakill;
    /// <summary>
    /// The rate at which the severity of this injury lowers
    /// </summary>
    public float NaturalHeal;
    /// <summary>
    /// The chance of this injury converting into others
    /// </summary>
    public Dictionary<Injury, float> ConversionChances = new();
    /// <summary>
    /// The chance of this injury creating others
    /// </summary>
    public Dictionary<Injury, float> AddChances = new();
    /// <summary>
    /// The severity needed for this injury to turn into another type.
    /// </summary>
    public Dictionary<InjuryType, float> ConversionSeverity = new();
    public string Translation;
    /// <summary>
    /// This is used in the last damage applied to the part befored it died is this
    /// </summary>
    public string DestructionTranslation;
    public InjuryType(float pain, float bleed, float opmin, float opmax, string translation, string destructionTranslation, float heal = -1, bool instakill = false)
    {
        Pain = pain;
        BleedingRate = bleed;
        OverkillPercentMax = opmax;
        OverkillPercentMin = opmin;
        NaturalHeal = heal;
        Instakill = instakill;
        Translation = translation;
        DestructionTranslation = destructionTranslation;
    }

    public InjuryType(Stream stream)
    {
        Translation = stream.ReadString();
        DestructionTranslation = stream.ReadString();
        Pain = stream.ReadFloat();
        BleedingRate = stream.ReadFloat();
        OverkillPercentMax = stream.ReadFloat();
        OverkillPercentMin = stream.ReadFloat();
        NaturalHeal = stream.ReadFloat();
        Instakill = stream.ReadByte() == 1;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Translation);
        stream.WriteString(DestructionTranslation);
        stream.WriteFloat(Pain);
        stream.WriteFloat(BleedingRate);
        stream.WriteFloat(OverkillPercentMax);
        stream.WriteFloat(OverkillPercentMin);
        stream.WriteFloat(NaturalHeal);
        stream.WriteByte(Instakill ? (byte)1 : (byte)0);
    }

    private InjuryType AddInjuryConversionChance(Injury injury, float chance)
    {
        ConversionChances[injury] = chance;
        return this;
    }
    private InjuryType AddInjuryConversionSeverity(InjuryType type, float severity)
    {
        ConversionSeverity[type] = severity;
        return this;
    }
    private InjuryType AddInjuryCreationChance(Injury injury, float chance)
    {
        AddChances[injury] = chance;
        return this;
    }

    private static InjuryType register(InjuryType type)
    {
        registeredTypes.Add(type);
        perTranslation[type.Translation] = type;
        return type;
    }

    public static IEnumerable<InjuryType> GetInjuryTypes()
    {
        return registeredTypes;
    }
    public static InjuryType? GetTypeByTranslation(string translation)
    {
        return perTranslation[translation];
    }
}
public class Injury : ISerializable
{
    public InjuryType Type;
    public float Severity;


    public Injury(InjuryType type, float severity)
    {
        Type = type;
        Severity = severity;
    }

    public Injury(Stream stream)
    {
        Type = new InjuryType(stream);
        Severity = stream.ReadFloat();
    }

    public void ToBytes(Stream stream)
    {
        Type.ToBytes(stream);
        stream.WriteFloat(Severity);
    }

    public override Boolean Equals(Object? obj)
    {
        return obj is Injury condition &&
               Type.Translation == condition.Type.Translation &&
               Severity == condition.Severity;
    }

}
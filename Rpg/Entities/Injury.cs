
using Rpg;

public class InjuryType : ISerializable
{
    private static List<InjuryType> registeredTypes = new List<InjuryType>();
    private static Dictionary<string, InjuryType> perName = new();
    public static readonly InjuryType Generic = register(new InjuryType(0, 0, 0, 0, "Generico", "Genericado"));
    public static readonly InjuryType Infection = register(new InjuryType(0, 0, 0, 0, "Infecção", "Necrosado", 1));
    public static readonly InjuryType Burn = register(new InjuryType(1.875f, 0, 40, 100, "Queimadura", "Queimado", 2).AddInjuryCreationChance(new Injury(Infection, 1), 8));
    public static readonly InjuryType Bruise = register(new InjuryType(1.25f, 0, 40, 100, "Hematoma", "Estilhaçado", 1.25f));
    public static readonly InjuryType Cut = register(new InjuryType(1.25f, 0.06f, 0, 10, "Corte", "Cortado Fora", 1).AddInjuryCreationChance(new Injury(Infection, 1), 15));
    public static readonly InjuryType Stab = register(new InjuryType(1.25f, 0.06f, 40, 100, "Perfuração", "Perfurado", 1).AddInjuryCreationChance(new Injury(Infection, 1), 15));
    public static readonly InjuryType Crack = register(new InjuryType(1, 0, 40, 100, "Rachadura", "Quebrado", 0.5f));

    public readonly byte Id;
    /// <summary>
    /// Pain value per damage
    /// </summary>
    public readonly float Pain;
    /// <summary>
    /// Bleed value per damage
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
    /// The rate at which the severity of this injury lowers
    /// </summary>
    public readonly float NaturalHeal;
    /// <summary>
    /// The chance of this injury converting into others
    /// </summary>
    public readonly Dictionary<Injury, float> ConversionChances = new();
    /// <summary>
    /// The chance of this injury creating others
    /// </summary>
    public readonly Dictionary<Injury, float> AddChances = new();
    /// <summary>
    /// The severity needed for this injury to turn into another type.
    /// </summary>
    public readonly Dictionary<InjuryType, float> ConversionSeverity = new();
    public readonly string Name;
    /// <summary>
    /// This is used in the last damage applied to the part befored it died is this
    /// </summary>
    public readonly string DestructionTranslation;
    private InjuryType(float pain, float bleed, float opmin, float opmax, string translation, string destructionTranslation, float heal = 0, bool instakill = false)
    {
        Id = (Byte)registeredTypes.Count;
        Pain = pain;
        BleedingRate = bleed;
        OverkillPercentMax = opmax;
        OverkillPercentMin = opmin;
        NaturalHeal = heal;
        Instakill = instakill;
        Name = translation;
        DestructionTranslation = destructionTranslation;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteByte(Id);
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
        perName[type.Name] = type;
        return type;
    }

    public static IEnumerable<InjuryType> GetInjuryTypes()
    {
        return registeredTypes;
    }
    public static InjuryType? ByName(string translation)
    {
        return perName[translation];
    }
    public static InjuryType? ById(int id)
    {
        if (registeredTypes.Count <= id || id < 0)
            return null;
        return registeredTypes[id];
    }
    public static InjuryType FromBytes(Stream stream)
    {
        return ById(stream.ReadByte());
    }

    public override Int32 GetHashCode()
    {
        return Id;
    }
}
public class Injury : ISerializable
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

}
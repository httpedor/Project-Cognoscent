
using System.Drawing;

namespace Rpg;

public class DamageType : ISerializable
{
    private static readonly List<DamageType> dts = new();
    private static readonly Dictionary<string, DamageType> dtsByName = new();
    public static readonly DamageType Physical = new DamageType("Físico", InjuryType.Generic, InjuryType.Generic, null).WithColor(System.Drawing.Color.Gray);
    public static readonly DamageType Sharp = new DamageType("Afiado", InjuryType.Cut, InjuryType.Crack, Physical).HardMod(-0.1f);
    public static readonly DamageType Slash = new DamageType("Corte", InjuryType.Cut, InjuryType.Crack, Sharp).HardMod(-0.1f);
    public static readonly DamageType Pierce = new DamageType("Perfuração", InjuryType.Stab, InjuryType.Crack, Sharp);
    public static readonly DamageType Blunt = new DamageType("Contusão", InjuryType.Bruise, InjuryType.Crack, Physical).SoftMod(-0.1f);
    public static readonly DamageType Fire = new DamageType("Fogo", InjuryType.Burn, InjuryType.Burn, null).WithColor(System.Drawing.Color.DarkOrange);
    
    public readonly byte Id;
    public readonly string Name;
    public Color? Color { get; private set; }
    public string BBHint => 
        Color is Color c
            ? $"[color=#{c.R:X2}{c.G:X2}{c.B:X2}]{Name}[/color]"
            : Name;
    public readonly DamageType? Parent;
    public readonly InjuryType OnSoft;
    public readonly InjuryType OnHard;
    private StatModifier? softModifier;
    private StatModifier? hardModifier;
    private DamageType(string name, InjuryType onSoft, InjuryType onHard, DamageType? parent)
    {
        Id = (byte)dts.Count;
        Name = name;
        OnSoft = onSoft;
        OnHard = onHard;
        Parent = parent;
        dts.Add(this);
        dtsByName[name] = this;

        if (Color == null)
            Color = parent?.Color;
    }

    public bool IsDerivedFrom(DamageType dt)
    {
        if (Parent == null)
            return false;
        if (Parent == dt)
            return true;

        return Parent.IsDerivedFrom(dt);
    }

    public IEnumerable<StatModifier> GetModifiersForSoft()
    {
        if (Parent != null)
            foreach (StatModifier mod in Parent.GetModifiersForSoft())
                yield return mod;
        if (softModifier != null)
            yield return (StatModifier)softModifier;
    }
    public IEnumerable<StatModifier> GetModifiersForHard()
    {
        if (Parent != null)
            foreach (StatModifier mod in Parent.GetModifiersForHard())
                yield return mod;
        if (hardModifier != null)
            yield return (StatModifier)hardModifier;
    }

    private DamageType SoftMod(float value, StatModifierType op = StatModifierType.Percent)
    {
        softModifier = new StatModifier(Name + "-soft-mod", value, op);
        return this;
    }
    private DamageType HardMod(float value, StatModifierType op = StatModifierType.Percent)
    {
        hardModifier = new StatModifier(Name + "-hard-mod", value, op);
        return this;
    }

    private DamageType WithColor(Color color)
    {
        Color = color;
        return this;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteByte(Id);
    }

    public static DamageType? FromName(string name)
    {
        return dtsByName.GetValueOrDefault(name);
    }

    public static DamageType FromId(byte id)
    {
        return dts[id];
    }
    public static DamageType FromBytes(Stream stream)
    {
        return FromId((byte)stream.ReadByte());
    }
    public override int GetHashCode()
    {
        return Id;
    }
}

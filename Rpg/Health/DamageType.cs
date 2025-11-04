
using System.Drawing;
using System.Text.Json.Nodes;

namespace Rpg;

public class DamageType : ISerializable
{
    // Well-known bindings populated via Compendium (kept for backward compatibility)
    public static DamageType Physical { get; private set; } = null!;
    public static DamageType Sharp { get; private set; } = null!;
    public static DamageType Slash { get; private set; } = null!;
    public static DamageType Pierce { get; private set; } = null!;
    public static DamageType Blunt { get; private set; } = null!;
    public static DamageType Fire { get; private set; } = null!;
    
    public readonly string Id;
    public readonly string Name;
    public Color? Color { get; private set; }
    public string BBHint => 
        Color is Color c
            ? $"[color=#{c.R:X2}{c.G:X2}{c.B:X2}]{Name}[/color]"
            : Name;
    public DamageType? Parent { get; private set; }
    public readonly InjuryType OnSoft;
    public readonly InjuryType OnHard;
    private StatModifier? softModifier;
    private StatModifier? hardModifier;
    private DamageType(string id, string name, InjuryType onSoft, InjuryType onHard, DamageType? parent)
    {
        Id = id;
        Name = name;
        OnSoft = onSoft;
        OnHard = onHard;
        Parent = parent;

        if (Color == null)
            Color = parent?.Color;
    }

    public DamageType(string name, JsonObject json) : this(
        name,
        json["name"]?.GetValue<string>() ?? name,
        InjuryType.ByName(json["onSoft"]?.GetValue<string>() ?? throw new ArgumentException("Missing onSoft")) ?? throw new ArgumentException("Invalid onSoft"),
        InjuryType.ByName(json["onHard"]?.GetValue<string>() ?? throw new ArgumentException("Missing onHard")) ?? throw new ArgumentException("Invalid onHard"),
        !string.IsNullOrWhiteSpace(json["parent"]?.GetValue<string>())
            ? FromName(json["parent"]!.GetValue<string>()!)
            : null
    )
    {
        if (json["color"] is JsonNode colorNode)
        {
            string colorStr = colorNode.GetValue<string>();
            try
            {
                // Try HTML first (#RRGGBB), then known color names
                Color c = colorStr.StartsWith("#") ? ColorTranslator.FromHtml(colorStr) : System.Drawing.Color.FromName(colorStr);
                if (c.A != 0 || colorStr.StartsWith("#"))
                    Color = c;
            }
            catch {
                Logger.LogWarning("[DamageType] Invalid color '" + colorStr + "' in DamageType " + Name);
            }
        }
    }

    private void SetParent(DamageType parent)
    {
        Parent = parent;
        if (Color == null)
            Color = parent.Color;
    }

    private static void BindWellKnown(DamageType dt)
    {
        switch (dt.Name)
        {
            case "Físico": Physical = dt; break;
            case "Afiado": Sharp = dt; break;
            case "Corte": Slash = dt; break;
            case "Perfuração": Pierce = dt; break;
            case "Contusão": Blunt = dt; break;
            case "Fogo": Fire = dt; break;
        }
    }

    public bool IsDerivedFrom(DamageType dt)
    {
        if (Parent == null)
            return false;
        if (Parent == dt)
            return true;

        return Parent.IsDerivedFrom(dt);
    }

    public void ToBytes(Stream stream)
    {
        new CompendiumEntryRef<DamageType>(Id).ToBytes(stream);
    }

    public static DamageType? FromName(string name)
    {
        return Compendium.FindEntry<DamageType>(dt => dt.Name == name);
    }

    public static DamageType? FromId(string id)
    {
        return Compendium.GetEntry<DamageType>(id);
    }
    public static DamageType FromBytes(Stream stream)
    {
        return new CompendiumEntryRef<DamageType>(stream).Get()!;
    }
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

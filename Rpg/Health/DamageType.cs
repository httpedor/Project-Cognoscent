
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities.Components.Health;

namespace Rpg.Health;

public partial class DamageType : ISerializable, ITaggable
{
    public readonly string Id;
    public readonly string Name;
    public Color? Color { get; private set; }
    public string BBHint => 
        Color is Color c
            ? $"[color=#{c.R:X2}{c.G:X2}{c.B:X2}]{Name}[/color]"
            : Name;
    public HashSet<string> Tags = new();
    HashSet<string> ITaggable.Tags { get => Tags; set => Tags = value; }

    public readonly Func<DamageInstance, BodyPart, Injury> InjuryResolver;
    private DamageType(string id, string name, Func<DamageInstance, BodyPart, Injury> injuryResolver)
    {
        Id = id;
        Name = name;
        InjuryResolver = injuryResolver;
    }

    public DamageType(string name, JsonElement json) : this(
        name,
        json.GetProperty("name").GetString() ?? name,
        null!
    )
    {
        var defInjury = Compendium.GetDefaultEntry<InjuryType>();
        if (json.TryGetProperty("injury", out JsonElement injuryNode) && injuryNode.ValueKind == JsonValueKind.String)
        {
            string injuryStr = injuryNode.GetString()!;
            if (Compendium.IsEntry<InjuryType>(injuryStr))
            {
                var injuryType = Compendium.GetEntry<InjuryType>(injuryStr)!;
                InjuryResolver = (damageInstance, part) => new Injury(injuryType, damageInstance.Amount);
            }
            else
            {
                InjuryResolver = (damageInstance, part) => new Injury(defInjury, damageInstance.Amount);
            }
        }
        else
            InjuryResolver = (damageInstance, part) => new Injury(defInjury, damageInstance.Amount);

        if (json.TryGetProperty("color", out JsonElement colorNode))
        {
            string colorStr = colorNode.GetString()!;
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
        if (json.TryGetProperty("tags", out JsonElement tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
        {
            this.LoadTags(tagsArr);
        }
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

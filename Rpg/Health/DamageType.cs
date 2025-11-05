
using System.Drawing;
using System.Text.Json.Nodes;

namespace Rpg;

public partial class DamageType : ISerializable, ITaggable, ICustomDataContainer
{
    private class InjuryResolverContext
    {
        public DamageSource source;
        public double damage;
        public BodyPart part;
        public DamageType self;
    
        public InjuryResolverContext(DamageSource source, double damage, BodyPart part, DamageType self)
        {
            this.source = source;
            this.damage = damage;
            this.part = part;
            this.self = self;
        }
    }
    
    public readonly string Id;
    public readonly string Name;
    public Color? Color { get; private set; }
    public string BBHint => 
        Color is Color c
            ? $"[color=#{c.R:X2}{c.G:X2}{c.B:X2}]{Name}[/color]"
            : Name;
    public DamageType? Parent { get; private set; }
    public readonly Func<DamageSource, double, BodyPart, Injury> InjuryResolver;
    private DamageType(string id, string name, Func<DamageSource, double, BodyPart, Injury> injuryResolver, DamageType? parent)
    {
        Id = id;
        Name = name;
        InjuryResolver = injuryResolver;
        Parent = parent;

        if (Color == null)
            Color = parent?.Color;
    }

    public DamageType(string name, JsonObject json) : this(
        name,
        json["name"]?.GetValue<string>() ?? name,
        null!,
        !string.IsNullOrWhiteSpace(json["parent"]?.GetValue<string>())
            ? FromName(json["parent"]!.GetValue<string>()!)
            : null
    )
    {
        var defInjury = Compendium.GetDefaultEntry<InjuryType>();
        if (json["injury"] is JsonNode injuryNode)
        {
            string injuryStr = injuryNode.GetValue<string>();
            if (Compendium.IsEntry<InjuryType>(injuryStr))
            {
                var injuryType = Compendium.GetEntry<InjuryType>(injuryStr)!;
                InjuryResolver = (source, damage, part) => new Injury(injuryType, damage);
            }
            else
            {
                try
                {
                    var func = Scripting.Compile<InjuryResolverContext, Either<Injury, InjuryType>>(injuryStr);
                    InjuryResolver = (source, damage, part) => func(new InjuryResolverContext(source, damage, part, this))
                    .Match<Injury>(
                        injury => injury,
                        injuryType => new Injury(injuryType, damage)
                    );
                } catch
                {
                    Logger.LogWarning("[DamageType] Invalid injury code in DamageType " + Name);
                    Logger.LogWarning("[DamageType] Did you mean to reference an InjuryType by name? '" + injuryStr + "' is not registered.");
                    InjuryResolver = (source, damage, part) => new Injury(defInjury, damage);
                }
            }
        }
        else
            InjuryResolver = (source, damage, part) => new Injury(defInjury, damage);

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

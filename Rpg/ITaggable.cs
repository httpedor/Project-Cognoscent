using System.Text.Json.Nodes;
using TraitGenerator;

namespace Rpg;

public interface ITaggable
{
    public IEnumerable<string> Tags { get; }
    public bool Is(string tag);
}
public static class TaggableExtensions
{
    extension(ITaggable taggable)
    {
        public bool HasAllTags(IEnumerable<string> tags)
        {
            foreach (string tag in tags)
            {
                if (!taggable.Is(tag))
                    return false;
            }
            return true;
        }
        public bool HasAnyTag(IEnumerable<string> tags)
        {
            foreach (string tag in tags)
            {
                if (taggable.Is(tag))
                    return true;
            }
            return false;
        }

        public bool HasTag(string tag)
        {
            return taggable.Is(tag);
        }

        public void LoadTagsFromJson(JsonObject json)
        {
            if (taggable.Tags is not ICollection<string> tagCol)
            {
                Logger.LogWarning("[ITaggable] Cannot load tags into non-collection Tags implementation");
                return;
            }
            if (json["tags"] is JsonArray tagArr)
            {
                foreach (var node in tagArr)
                {
                    if (node is JsonValue val)
                    {
                        string? tag = val.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            tagCol.Add(tag!.ToLowerInvariant());
                        }
                    }
                }
            }
        }
    }
}

[Mixin(typeof(ITaggable))]
abstract class TaggableMixin : ITaggable
{
    protected HashSet<string> tags = new();

    IEnumerable<string> ITaggable.Tags => tags;


    public bool Is(string tag)
    {
        return tags.Contains(tag);
    }

    protected void TagsToBytes(Stream stream)
    {
        stream.WriteInt32(tags.Count);
        foreach (string tag in tags)
        {
            stream.WriteString(tag);
        }
    }

    protected void TagsFromBytes(Stream stream)
    {
        int count = stream.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string tag = stream.ReadString();
            tags.Add(tag);
        }
    }
}

public static class BodyTags
{
    /// <summary>
    /// Hard body parts, like bones.
    /// </summary>
    public const string Hard = "hard";
    /// <summary>
    /// Internal body parts, like organs.
    /// </summary>
    public const string Internal = "internal";
    /// <summary>
    /// Body parts that are socketted into place, like eyes or teeth.
    /// There's a chance that this body part can be dislodged on heavy impacts.
    /// </summary>
    public const string Socket = "socket";

    /// <summary>
    /// Body parts that can be broken, like arms or legs.
    /// </summary>
    public const string Joint = "joint";

    /// <summary>
    /// Body parts that are or have bones.
    /// </summary>
    public const string Bone = "bone";

    /// <summary>
    /// Body parts that overlap with their parent body part.
    /// </summary>
    public const string Overlaps = "overlaps";

    /// <summary>
    /// Limb body parts, like arms or legs.
    /// These can be used to grapple.
    /// </summary>
    public const string Limb = "limb";
}
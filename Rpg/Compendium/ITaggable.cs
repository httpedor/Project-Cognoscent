using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg;

public interface ITaggable
{
    public HashSet<string> Tags { get; protected set; }
}

public static class ITaggableExtensions
{
    public static bool HasTag(this ITaggable taggable, string tag)
    {
        return taggable.Tags.Contains(tag);
    }
    public static bool HasAllTags(this ITaggable taggable, IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            if (!taggable.Tags.Contains(tag))
                return false;
        }
        return true;
    }

    public static bool HasAnyTag(this ITaggable taggable, IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            if (taggable.Tags.Contains(tag))
                return true;
        }
        return false;
    }

    public static bool Is(this ITaggable taggable, string tag)
    {
        return taggable.HasTag(tag);
    }
    public static bool Is<T>(this ITaggable taggable, Tag<T> tag) where T : ITaggable
    {
        return taggable.HasTag(tag.Name);
    }

    public static void LoadTags(this ITaggable taggable, JsonElement json)
    {
        taggable.Tags.Clear();
        foreach (var tagNode in json.EnumerateArray())
        {
            taggable.Tags.Add(tagNode.GetString()!);
        }
    }
    public static void SaveTags(this ITaggable taggable, JsonArray json)
    {
        json.Clear();
        foreach (var tag in taggable.Tags)
        {
            json.Add(tag);
        }
    }
    public static void SaveTags(this ITaggable taggable, Stream stream)
    {
        stream.WriteUInt16((ushort)taggable.Tags.Count);
        foreach (var tag in taggable.Tags)
        {
            stream.WriteString(tag);
        }
    }
    public static void LoadTags(this ITaggable taggable, Stream stream)
    {
        taggable.Tags.Clear();
        ushort tagCount = stream.ReadUInt16();
        for (int i = 0; i < tagCount; i++)
        {
            string tag = stream.ReadString();
            taggable.Tags.Add(tag);
        }
    }
}

public class Tag<T> where T : ITaggable
{
    public string Name;
    public Tag(string name)
    {
        Name = name;
    }
}

/*public class CompositeTag<T> where T : ITaggable
{
    public enum Operator
    {
        And,
        Or
    }
    public IEnumerable<Tag<T>> SubTags;
    public Operator SubTagOperator;

    public CompositeTag(IEnumerable<Tag<T>> subTags, Operator subTagOperator)
    {
        SubTags = subTags;
        SubTagOperator = subTagOperator;
    }
}*/
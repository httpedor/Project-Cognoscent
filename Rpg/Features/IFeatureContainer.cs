using System.Text.Json.Serialization;
using TraitGenerator;

namespace Rpg;

public class FeatureContainerRef
{
    public IFeatureContainer? FeatureSource;
    public FeatureContainerRef(IFeatureContainer featureSource)
    {
        FeatureSource = featureSource;
    }

    public FeatureContainerRef(Stream stream)
    {
        byte type = (byte)stream.ReadByte();
        FeatureSource = type switch
        {
            0 => null,
            1 => new BodyPartRef(stream).BodyPart,
            2 => new EntityRef(stream).Entity,
            _ => throw new Exception("Unknown skill source type: " + type)
        };
    }

    public void ToBytes(Stream stream)
    {
        switch (FeatureSource)
        {
            case null:
                stream.WriteByte(0);
                break;
            case BodyPart bp:
                stream.WriteByte(1);
                new BodyPartRef(bp).ToBytes(stream);
                break;
            case Entity i:
                stream.WriteByte(2);
                new EntityRef(i).ToBytes(stream);
                break;
            default:
                throw new Exception("Unknown skill source type: " + FeatureSource.GetType());
        }
    }
}

public interface IFeatureContainer : ICustomDataContainer
{
    public event Action<Feature>? OnFeatureAdded;
    public event Action<Feature>? OnFeatureRemoved;
    public event Action<Feature>? OnFeatureEnabled;
    public event Action<Feature>? OnFeatureDisabled;

    public string Name { get; }
    public Board? Board { get; }
    public IEnumerable<Feature> Features { get; }
    public IEnumerable<Feature> EnabledFeatures { get; }
    public bool IsFeatureEnabled(string id);
    public Feature? GetFeature(string id);
    public void AddFeature(Feature feature);
    public Feature? RemoveFeature(string id);
    public bool EnableFeature(string id);
    public bool DisableFeature(string id);
    public bool HasFeature(string id);
}

[Mixin(typeof(IFeatureContainer))]
abstract class FeatureContainerMixin : IFeatureContainer
{
    public event Action<Feature>? OnFeatureAdded;
    public event Action<Feature>? OnFeatureRemoved;
    public event Action<Feature>? OnFeatureEnabled;
    public event Action<Feature>? OnFeatureDisabled;

    [JsonInclude]
    protected Dictionary<string, (Feature feature, bool enabled)> features = new();

    abstract public string Name { get; }

    abstract public Board Board { get; }

    [JsonIgnore]
    public IEnumerable<Feature> Features => features.Values.Select(t => t.feature);

    [JsonIgnore]
    public IEnumerable<Feature> EnabledFeatures => features.Values.Where(t => t.enabled).Select(t => t.feature);

    public abstract long Id { get; }

    public void AddFeature(Feature feature)
    {
        features[feature.GetId()] = (feature, true);
        OnFeatureAdded?.Invoke(feature);
        feature.OnAdded(this);
        feature.OnEnable(this);
    }

    public Feature? RemoveFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features.Remove(id);
            DisableFeature(id);
            value.feature.OnRemoved(this);
            OnFeatureRemoved?.Invoke(value.feature);
            return value.feature;
        }
        return null;
    }

    public bool DisableFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features[id] = (value.feature, false);
            value.feature.OnDisable(this);
            OnFeatureDisabled?.Invoke(value.feature);
            return true;
        }
        return false;
    }

    public bool EnableFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features[id] = (value.feature, true);
            value.feature.OnEnable(this);
            OnFeatureEnabled?.Invoke(value.feature);
            return true;
        }
        return false;
    }

    public abstract byte[]? GetCustomData(string id);

    public Feature? GetFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            return value.feature;
        }
        return null;
    }

    public bool HasFeature(string id)
    {
        return features.ContainsKey(id);
    }

    public bool IsFeatureEnabled(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            return value.enabled;
        }
        return false;
    }

    public abstract void SetCustomData(string id, byte[]? data);

    protected void FeaturesToBytes(Stream stream)
    {
        stream.WriteByte((byte)features.Count);
        foreach (var feature in features)
        {
            stream.WriteBoolean(feature.Value.enabled);
            feature.Value.feature.ToBytes(stream);
        }
    }
    protected void FeaturesFromBytes(Stream stream)
    {
        int count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            bool enabled = stream.ReadBoolean();
            Feature feature = Feature.FromBytes(stream);
            features[feature.GetId()] = (feature, enabled);
        }
    }
}

public static class FeatureSourceExtensions
{
    public static bool HasFeature(this IFeatureContainer source, Feature feature)
    {
        return source.HasFeature(feature.GetId());
    }
    public static Feature? RemoveFeature(this IFeatureContainer source, Feature feature)
    {
        return source.RemoveFeature(feature.GetId());
    }
    public static bool IsFeatureEnabled(this IFeatureContainer source, Feature feature)
    {
        return source.IsFeatureEnabled(feature.GetId());
    }
}
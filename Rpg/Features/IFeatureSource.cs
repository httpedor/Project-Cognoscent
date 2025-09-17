namespace Rpg;

public class FeatureSourceRef
{
    public IFeatureSource? FeatureSource;
    public FeatureSourceRef(IFeatureSource featureSource)
    {
        FeatureSource = featureSource;
    }

    public FeatureSourceRef(Stream stream)
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

public interface IFeatureSource : ICustomDataContainer
{
    Dictionary<string, (Feature feature, bool enabled)> FeaturesDict
    {
        get;
    }
    public string Name { get; }
    public Board Board { get; }
    public IEnumerable<Feature> Features => FeaturesDict.Values.Select(t => t.feature);
    public IEnumerable<Feature> EnabledFeatures => FeaturesDict.Values.Where(t => t.enabled).Select(t => t.feature);

    public bool IsFeatureEnabled(string id)
    {
        if (FeaturesDict.TryGetValue(id, out var tuple))
            return tuple.enabled;

        return false;
    }

    public Feature? GetFeature(string id)
    {
        if (FeaturesDict.TryGetValue(id, out var tuple))
            return tuple.feature;
        return null;
    }

    public void AddFeature(Feature feature)
    {
        FeaturesDict[feature.GetId()] = (feature, true);
        feature.Enable(this);
    }

    public Feature? RemoveFeature(string id)
    {
        if (!FeaturesDict.TryGetValue(id, out var featureData)) return null;
        if (featureData.enabled)
            featureData.feature.Disable(this);
        FeaturesDict.Remove(id);
        return featureData.feature;
    }
    
    public bool EnableFeature(string id)
    {
        if (!FeaturesDict.TryGetValue(id, out (Feature feature, bool enabled) featureData) || featureData.enabled) return false;
        
        featureData.feature.Enable(this);
        FeaturesDict[id] = (featureData.feature, true);
        return true;
    }

    public bool DisableFeature(string id)
    {
        if (!FeaturesDict.TryGetValue(id, out (Feature feature, bool enabled) featureData) || !featureData.enabled) return false;
        
        featureData.feature.Disable(this);
        FeaturesDict[id] = (featureData.feature, false);
        return true;
    }

    public bool HasFeature(string id)
    {
        return FeaturesDict.ContainsKey(id);
    }

    public bool HasFeature(Feature feature)
    {
        return FeaturesDict.ContainsKey(feature.GetId());
    }
}

public static class FeatureSourceExtensions
{
    public static bool HasFeature(this IFeatureSource source, string id)
    {
        return source.FeaturesDict.ContainsKey(id);
    }

    public static bool HasFeature(this IFeatureSource source, Feature feature)
    {
        return source.HasFeature(feature.GetId());
    }
    public static Feature? RemoveFeature(this IFeatureSource source, Feature feature)
    {
        return source.RemoveFeature(feature.GetId());
    }
    public static bool IsFeatureEnabled(this IFeatureSource source, Feature feature)
    {
        return source.IsFeatureEnabled(feature.GetId());
    }
}
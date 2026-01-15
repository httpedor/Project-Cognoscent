using System.Text.Json.Serialization;
using Rpg.Features;

namespace Rpg.Entities;

public abstract class FeatureEvent(Component component, Feature feature) : ComponentEvent(component)
{
    public Feature Feature = feature;
}
public class FeatureAddedEvent(Component component, Feature feature) : FeatureEvent(component, feature)
{
}
public class FeatureRemovedEvent(Component component, Feature feature) : FeatureEvent(component, feature) 
{
}
public class FeatureEnabledEvent(Component component, Feature feature) : FeatureEvent(component, feature) 
{
}
public class FeatureDisabledEvent(Component component, Feature feature) : FeatureEvent(component, feature) 
{
}
public partial class FeaturesComponent : Component, ITickableComponent
{
    [JsonInclude]
    protected Dictionary<string, (Feature feature, bool enabled)> features = new();

    [JsonIgnore]
    public IEnumerable<Feature> Features => features.Values.Select(t => t.feature);

    [JsonIgnore]
    public IEnumerable<Feature> EnabledFeatures => features.Values.Where(t => t.enabled).Select(t => t.feature);
    
    [OptionalComponent(typeof(CustomDataComponent))]
    public CustomDataComponent CustomData;

    public FeaturesComponent() : base()
    {

    }
    public FeaturesComponent(Stream stream) : base(stream)
    {
        int count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            bool enabled = stream.ReadBoolean();
            Feature feature = Feature.FromBytes(stream);
            features[feature.GetId()] = (feature, enabled);
        }
    }
    public void AddFeature(Feature feature)
    {
        features[feature.GetId()] = (feature, true);
        Entity.DispatchEvent(new FeatureAddedEvent(this, feature));
        feature.OnAdded(Entity);
        feature.OnEnable(Entity);
    }

    public Feature? RemoveFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features.Remove(id);
            DisableFeature(id);
            value.feature.OnRemoved(Entity);
            Entity.DispatchEvent(new FeatureRemovedEvent(this, value.feature));
            return value.feature;
        }
        return null;
    }
    public Feature? RemoveFeature(Feature feature)
    {
        return RemoveFeature(feature.GetId());
    }

    public bool DisableFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features[id] = (value.feature, false);
            value.feature.OnDisable(Entity);
            Entity.DispatchEvent(new FeatureDisabledEvent(this, value.feature));
            return true;
        }
        return false;
    }

    public bool EnableFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features[id] = (value.feature, true);
            value.feature.OnEnable(Entity);
            Entity.DispatchEvent(new FeatureEnabledEvent(this, value.feature));
            return true;
        }
        return false;
    }

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
    public bool HasFeature(Feature feature)
    {
        return features.ContainsKey(feature.GetId());
    }

    public bool IsFeatureEnabled(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            return value.enabled;
        }
        return false;
    }
    public bool IsFeatureEnabled(Feature feature)
    {
        return IsFeatureEnabled(feature.GetId());
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteByte((byte)features.Count);
        foreach (var feature in features)
        {
            stream.WriteBoolean(feature.Value.enabled);
            feature.Value.feature.ToBytes(stream);
        }
    }

    public void OnTick()
    {
        foreach (var feature in EnabledFeatures)
        {
            feature.OnTick(Entity);
        }
    }
}
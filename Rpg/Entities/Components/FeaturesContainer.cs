using System.Text.Json.Serialization;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Interfaces;
using Rpg.Features;
using Rpg.Health;
using Rpg.Skills;

namespace Rpg.Entities.Components;

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
public partial class FeaturesContainer : Component,
                                        ITickableComponent, ISkillProvider, IPostureProvider,
                                        ComponentEventHandler<DamageEvent>, ComponentEventHandler<BodyLayerInjuryAddedEvent>
{
    [JsonInclude]
    protected Dictionary<string, (Feature feature, bool enabled)> features = new();

    [JsonIgnore]
    public IEnumerable<Feature> Features => features.Values.Select(t => t.feature);

    [JsonIgnore]
    public IEnumerable<Feature> EnabledFeatures => features.Values.Where(t => t.enabled).Select(t => t.feature);
    
    [OptionalComponent(typeof(CustomDataComponent))]
    public CustomDataComponent CustomData;

    public FeaturesContainer() : base()
    {

    }
    public FeaturesContainer(Stream stream) : base(stream)
    {
        int count = stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            bool enabled = stream.ReadBoolean();
            Feature feature = Feature.FromBytes(stream);
            features[feature.Id] = (feature, enabled);
        }
    }
    public void AddFeature(Feature feature)
    {
        features[feature.Id] = (feature, true);
        Entity.DispatchEvent(new FeatureAddedEvent(this, feature));
        feature.OnAdded(this);
        feature.OnEnable(this);
    }

    public void AddCondition(ConditionFeature condition, float duration)
    {
        if (CustomData == null)
        {
            Logger.Log($"FeaturesContainer {Entity.Id} does not have a CustomDataComponent. Cannot track condition duration.", LogLevel.Error);
            return;
        }
        AddFeature(condition);
        CustomData.SetUInt(condition.EndTickKey, Entity.Board.CurrentTick + (uint)(duration * Physics.TicksPerSecond));
    }

    public Feature? RemoveFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features.Remove(id);
            DisableFeature(id);
            value.feature.OnRemoved(this);
            Entity.DispatchEvent(new FeatureRemovedEvent(this, value.feature));
            if (value.feature is ConditionFeature condition && CustomData != null)
            {
                CustomData.Remove(condition.StartTickKey);
                CustomData.Remove(condition.EndTickKey);
            }
            return value.feature;
        }
        return null;
    }
    public Feature? RemoveFeature(Feature feature)
    {
        return RemoveFeature(feature.Id);
    }

    public bool DisableFeature(string id)
    {
        if (features.TryGetValue(id, out var value))
        {
            features[id] = (value.feature, false);
            value.feature.OnDisable(this);
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
            value.feature.OnEnable(this);
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
        return features.ContainsKey(feature.Id);
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
        return IsFeatureEnabled(feature.Id);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
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
            feature.OnTick(this);
        }
    }

    public void HandleEvent(DamageEvent ev)
    {
        foreach (var feat in EnabledFeatures)
        {
            ev.DamageModifiers.AddRange(feat.ModifyReceivingDamageModifiers(this, ev.DamageInstance));
        }
        foreach (var feat in EnabledFeatures)
        {
            var ret = feat.ModifyReceivingDamage(this, ev.Damaged, ev.DamageInstance);
            if (ev.DamageInstance.Amount == ret.Item1)
                continue;
            ev.DamageInstance.Amount = (float)ret.Item1;
            if (ev.Formula != null)
                ev.Formula += "\n " + ret.Item2;
        }
    }

    public void HandleEvent(BodyLayerInjuryAddedEvent componentEvent)
    {
        foreach (var feat in EnabledFeatures)
        {
            feat.OnInjured(this, componentEvent.Part, componentEvent.Injury);
        }
    }

    public IEnumerable<Skill> GetSkillsFor(SkillExecutor executor)
    {
        foreach (var feat in EnabledFeatures)
        {
            foreach (var skill in feat.GetSkills(this, executor))
            {
                yield return skill;
            }
        }
    }

    public IEnumerable<BodyPosture> GetProvidedPostures()
    {
        foreach (var feat in EnabledFeatures)
        {
            foreach (var posture in feat.GetProvidedPostures(this))
            {
                yield return posture;
            }
        }
    }
}
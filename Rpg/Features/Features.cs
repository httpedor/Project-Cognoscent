namespace Rpg;

public static class Features
{
    private static Dictionary<string, Feature> _features = new();
    public static IEnumerable<Feature> All => _features.Values;
    public static readonly Feature OnFire = register(new DamageOverTimeCondition("on_fire", "Fogo", "Esta entidade está pegando fogo.", DamageType.Fire, 1));

    private static T register<T>(T feature) where T : Feature
    {
        _features[feature.GetId()] = feature;
        return feature;
    }
    
    public static Feature Get(string id)
    {
        if (_features.TryGetValue(id, out var feature))
            return feature;
        throw new KeyNotFoundException($"Feature with ID '{id}' not found.");
    }
    public static bool Exists(string id)
    {
        return _features.ContainsKey(id);
    }
    public static bool TryGet(string id, out Feature? feature)
    {
        return _features.TryGetValue(id, out feature);
    }
}
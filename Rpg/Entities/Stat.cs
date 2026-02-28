using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Rpg;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;

public enum StatModifierType
{
    Flat,
    FlatPostMods,
    Percent,
    Multiplier,
    Capmax,
    Capmin,
    OverrideBase,
    OverrideFinal
}
public class StatModifier : ISerializable
{
    public readonly string Id;
    public float Value;
    public StatModifierType Type;

    public StatModifier(string id, float value, StatModifierType type)
    {
        Id = id;
        Value = value;
        Type = type;
    }

    public StatModifier(Stream stream)
    {
        Id = stream.ReadString();
        Value = stream.ReadFloat();
        Type = (StatModifierType)stream.ReadByte();
    }

    public StatModifier(JsonObject json, string defId = "")
    {
        Id = json.ContainsKey("id") ? json["id"]!.GetValue<string>() : defId;
        Value = json["value"]!.GetValue<float>();
        Type = Enum.Parse<StatModifierType>(json["type"]!.GetValue<string>().ToLower().FirstCharToUpper());
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
        stream.WriteFloat(Value);
        stream.WriteByte((byte)Type);
    }

    public static implicit operator StatModifier((string id, float value, StatModifierType type) tuple)
    {
        return new StatModifier(tuple.id, tuple.value, tuple.type);
    }
}

public class StatEvent
{
    public Stat Stat;
    public StatEvent(Stat stat)
    {
        Stat = stat;
    }
}
public class StatBaseChangeEvent : StatEvent
{
    public float OldBaseValue;
    public float NewBaseValue;
    public StatBaseChangeEvent(Stat stat, float oldBaseValue, float newBaseValue) : base(stat)
    {
        OldBaseValue = oldBaseValue;
        NewBaseValue = newBaseValue;
    }
}
public class StatModifierChangeEvent : StatEvent
{
    public StatModifier Modifier;
    public bool Removed; // if true, the modifier was removed. if false, the modifier was added or changed.
    public StatModifierChangeEvent(Stat stat, StatModifier modifier, bool removed) : base(stat)
    {
        Modifier = modifier;
        Removed = removed;
    }
}
public class StatBoundsChangeEvent : StatEvent
{
    public float OldMinValue;
    public float NewMinValue;
    public float OldMaxValue;
    public float NewMaxValue;
    public StatBoundsChangeEvent(Stat stat, float oldMinValue, float newMinValue, float oldMaxValue, float newMaxValue) : base(stat)
    {
        OldMinValue = oldMinValue;
        NewMinValue = newMinValue;
        OldMaxValue = oldMaxValue;
        NewMaxValue = newMaxValue;
    }
}
public interface IStatEventHandler
{
    void OnStatChanged(StatEvent statEvent);
}
public class Stat : ISerializable
{
    public IStatEventHandler? Container;

    public string Id { get; }
    public string Name;
    [JsonInclude]
    private float minValue;
    [JsonInclude]
    private float maxValue;
    [JsonIgnore]
    public float MinValue
    {
        get => minValue;
        set
        {
            var old = minValue;
            minValue = value;
            // if under cap is enabled, ensure baseValue respects new min
            if (UnderCap)
                baseValue = Math.Max(baseValue, minValue);
            if (old == value)
                return;
            Container?.OnStatChanged(new StatBoundsChangeEvent(this, old, minValue, maxValue, maxValue));
            CalculateFinalValue();
        }
    }

    [JsonIgnore]
    public float MaxValue
    {
        get => maxValue;
        set
        {
            var old = maxValue;
            maxValue = value;
            // if over cap is enabled, ensure baseValue respects new max
            if (OverCap)
                baseValue = Math.Min(baseValue, maxValue);
            if (old == value)
                return;
            Container?.OnStatChanged(new StatBoundsChangeEvent(this, minValue, minValue, old, maxValue));
            CalculateFinalValue();
        }
    }
    public bool OverCap { get; private set; }
    public bool UnderCap { get; private set; }
    [JsonInclude]
    private float baseValue;
    [JsonInclude]
    private float finalValue;
    public string[] Aliases = [];
    [JsonInclude]
    private readonly Dictionary<string, StatModifier> modifiers;

    [JsonIgnore]
    public float BaseValue
    {
        get => baseValue;
        set
        {
            var old = baseValue;
            baseValue = value;
            if (OverCap)
                baseValue = Math.Min(baseValue, MaxValue);
            if (UnderCap)
                baseValue = Math.Max(baseValue, MinValue);
            if (old == value)
                return;
            Container?.OnStatChanged(new StatBaseChangeEvent(this, old, value));
            CalculateFinalValue();
        }
    }

    [JsonIgnore]
    public float FinalValue => finalValue;

    public Stat(string id, float baseValue, float min = 0, float max = float.MaxValue, bool overCap = true, bool underCap = true)
    {
        Id = id;
        Name = id;
        // assign backing fields directly to avoid firing change events during construction
        minValue = min;
        maxValue = max;
        OverCap = overCap;
        UnderCap = underCap;
        this.baseValue = baseValue;
        modifiers = new Dictionary<string, StatModifier>();
    }

    public Stat(Stream stream)
    {
        Id = stream.ReadString();
        Name = stream.ReadString();
        baseValue = stream.ReadFloat();
        // read backing fields directly to avoid firing change events during deserialization
        minValue = stream.ReadFloat();
        maxValue = stream.ReadFloat();
        OverCap = stream.ReadBoolean();
        UnderCap = stream.ReadBoolean();
        byte aliasCount = (byte)stream.ReadByte();
        Aliases = new string[aliasCount];
        for (int i = 0; i < aliasCount; i++)
            Aliases[i] = stream.ReadString();
        modifiers = new Dictionary<string, StatModifier>();
        byte count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            var mod = new StatModifier(stream);
            modifiers[mod.Id] = mod;
        }
        CalculateFinalValue();
    }

    public void RemoveModifier(string id)
    {
        if (!modifiers.ContainsKey(id)) return;
        
        var mod = modifiers[id];
        modifiers.Remove(id);
        Container?.OnStatChanged(new StatModifierChangeEvent(this, mod, true));
        CalculateFinalValue();
    }
    public void RemoveModifier(StatModifier modifier)
    {
        RemoveModifier(modifier.Id);
    }

    public void SetModifier(StatModifier modifier)
    {
        modifiers[modifier.Id] = modifier;
        Container?.OnStatChanged(new StatModifierChangeEvent(this, modifier, false));
        CalculateFinalValue();
    }
    public void SetModifier(string id, float value, StatModifierType type)
    {
        SetModifier(new StatModifier(id, value, type));
    }
    public void AddModifier(string id, float value, StatModifierType type)
    {
        SetModifier(new StatModifier(id, value, type));
    }
    public void AddModifier(StatModifier modifier)
    {
        SetModifier(modifier);
    }

    public void ClearModifiers()
    {
        modifiers.Clear();
        CalculateFinalValue();
    }

    private void CalculateFinalValue()
    {
        float old = finalValue;
        float newBase = baseValue;
        finalValue = ApplyModifiers(modifiers.Values, newBase, MinValue, MaxValue, OverCap, UnderCap);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
        stream.WriteString(Name);
        stream.WriteFloat(baseValue);
        stream.WriteFloat(MinValue);
        stream.WriteFloat(MaxValue);
        stream.WriteBoolean(OverCap);
        stream.WriteBoolean(UnderCap);
        stream.WriteByte((byte)Aliases.Length);
        foreach (string alias in Aliases)
            stream.WriteString(alias);
        stream.WriteByte((byte)modifiers.Count);
        foreach (StatModifier modifier in modifiers.Values)
            modifier.ToBytes(stream);
    }

    public IEnumerable<StatModifier> GetModifiers()
    {
        return modifiers.Values;
    }

    public StatModifier? GetModifier(string id)
    {
        return modifiers.ContainsKey(id) ? modifiers[id] : null;
    }

    public Stat Clone()
    {
        var ret = new Stat(Id, baseValue, MinValue, MaxValue, OverCap, UnderCap);
        foreach (var mod in GetModifiers())
            ret.AddModifier(mod);
        ret.Aliases = (string[])Aliases.Clone();
        return ret;
    }

    public static float ApplyModifiers(IEnumerable<StatModifier> modifiers, float baseValue = 0, float min = float.MinValue, float max = float.MaxValue, bool overCap = true, bool underCap = true)
    {
        float newBase = baseValue;
        var modsPerType = new Dictionary<StatModifierType, List<StatModifier>>();
        foreach (StatModifier modifier in modifiers)
        {
            if (!modsPerType.ContainsKey(modifier.Type))
                modsPerType[modifier.Type] = new List<StatModifier>();
            modsPerType[modifier.Type].Add(modifier);
        }

        foreach (var mod in modsPerType.GetValueOrDefault(StatModifierType.Flat, []))
            newBase += mod.Value;
        float finalValue = newBase;
        foreach (StatModifier modifier in modsPerType.GetValueOrDefault(StatModifierType.Percent, []))
            finalValue += newBase * modifier.Value;
        foreach (StatModifier modifier in modsPerType.GetValueOrDefault(StatModifierType.Multiplier, []))
            finalValue *= 1 + modifier.Value;
        foreach (var mod in modsPerType.GetValueOrDefault(StatModifierType.FlatPostMods, []))
            finalValue += mod.Value;

        if (overCap)
            finalValue = Math.Min(finalValue, max);
        if (underCap)
            finalValue = Math.Max(finalValue, min);
        
        float minValue = float.MinValue;
        float maxValue = float.MaxValue;
        var minMods = modsPerType.GetValueOrDefault(StatModifierType.Capmin, []);
        var maxMods = modsPerType.GetValueOrDefault(StatModifierType.Capmax, []);
        if (minMods.Count > 0)
            minValue = minMods.Max(modifier => modifier.Value);
        if (maxMods.Count > 0)
            maxValue = maxMods.Min(modifier => modifier.Value);

        finalValue = Math.Clamp(finalValue, minValue, maxValue);

        return finalValue;
    }
}

public static class CreatureStats
{
    public const string ADRENALINE = "adrenaline";
    public const string AGILITY = "agility";
    public const string INTELLIGENCE = "intelligence";
    public const string KNOWLEDGE =  "knowledge";
    public const string WISDOM = KNOWLEDGE;
    public const string STRENGTH = "strength";
    public const string PERCEPTION = "perception";
    public const string JUMP = "jump";
    public const string RESPIRATION = "respiration";
    public const string BLOOD_FLOW = "blood flow";
    public const string CONSCIOUSNESS = "consciousness";
    public const string SIGHT = "sight";
    public const string HEARING = "hearing";
    public const string SOCIAL = "social";
    public const string MOVEMENT = "movement";
    public const string MOVEMENT_SPEED = MOVEMENT;
    public const string PAIN = "pain";

    public static string[] GetAllStats()
    {
        return typeof(CreatureStats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)!).ToArray()!;
    }

    public static float GetAttributeModifier(float stat)
    {
        return stat/3;
    }
}
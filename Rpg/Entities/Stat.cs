using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text.Json.Nodes;
using Rpg;

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
public struct StatModifier : ISerializable
{
    public string Id;
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
}

//TODO: MaxBase, MinBase, MaxFinal, MinFinal
public class Stat : ISerializable
{
    public event Action<float, float>? ValueChanged;
    public event Action<float, float>? BaseValueChanged;
    public event Action<StatModifier>? ModifierAdded;
    public event Action<StatModifier>? ModifierUpdated;
    public event Action<StatModifier>? ModifierRemoved;

    public string Id { get; }
    public float MinValue { get; private set; }
    public float MaxValue { get; private set; }
    public bool OverCap { get; private set; }
    public bool UnderCap { get; private set; }
    private float baseValue;
    private float finalValue;
    private readonly Dictionary<string, StatModifier> modifiers;

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
            BaseValueChanged?.Invoke(baseValue, old);
            CalculateFinalValue();
        }
    }

    public float FinalValue => finalValue;

    public Stat(string id, float baseValue, float min = 0, float max = float.MaxValue, bool overCap = true, bool underCap = true)
    {
        Id = id;
        MinValue = min;
        MaxValue = max;
        OverCap = overCap;
        UnderCap = underCap;
        this.baseValue = baseValue;
        modifiers = new Dictionary<string, StatModifier>();
    }

    public Stat(Stream stream)
    {
        Id = stream.ReadString();
        baseValue = stream.ReadFloat();
        MinValue = stream.ReadFloat();
        MaxValue = stream.ReadFloat();
        OverCap = stream.ReadBoolean();
        UnderCap = stream.ReadBoolean();
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
        
        modifiers.Remove(id);
        ModifierRemoved?.Invoke(modifiers[id]);
        CalculateFinalValue();
    }
    public void RemoveModifier(StatModifier modifier)
    {
        RemoveModifier(modifier.Id);
    }

    public void SetModifier(StatModifier modifier)
    {
        bool wasThere = modifiers.ContainsKey(modifier.Id);
        modifiers[modifier.Id] = modifier;
        CalculateFinalValue();
        ModifierUpdated?.Invoke(modifier);
        if (!wasThere)
            ModifierAdded?.Invoke(modifier);
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

    private void CalculateFinalValue()
    {
        float old = finalValue;
        float newBase = baseValue;
        finalValue = ApplyModifiers(modifiers.Values, newBase, MinValue, MaxValue, OverCap, UnderCap);
        ValueChanged?.Invoke(old, finalValue);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
        stream.WriteFloat(baseValue);
        stream.WriteFloat(MinValue);
        stream.WriteFloat(MaxValue);
        stream.WriteBoolean(OverCap);
        stream.WriteBoolean(UnderCap);
        stream.WriteByte((byte)modifiers.Count);
        foreach (StatModifier modifier in modifiers.Values)
        {
            modifier.ToBytes(stream);
        }
    }

    public virtual void ClearEvents()
    {
        ValueChanged = null;
        BaseValueChanged = null;
        ModifierAdded = null;
        ModifierUpdated = null;
        ModifierRemoved = null;
    }

    public IEnumerable<StatModifier> GetModifiers()
    {
        return modifiers.Values;
    }

    public Stat Clone()
    {
        var ret = new Stat(Id, baseValue, MinValue, MaxValue, OverCap, UnderCap);
        foreach (var mod in GetModifiers())
            ret.AddModifier(mod);
        return ret;
    }

    public static float ApplyModifiers(IEnumerable<StatModifier> modifiers, float baseValue = 0, float min = float.MinValue, float max = float.MaxValue, bool overCap = true, bool underCap = true)
    {
        float newBase = baseValue;
        var flatModifiers = new List<StatModifier>();
        var flatPostMods = new List<StatModifier>();
        var percentModifiers = new List<StatModifier>();
        var multiplierModifiers = new List<StatModifier>();
        var minModifiers = new List<StatModifier>();
        var maxModifiers = new List<StatModifier>();
        foreach (StatModifier modifier in modifiers)
        {
            switch (modifier.Type)
            {
                case StatModifierType.Flat:
                    flatModifiers.Add(modifier);
                    break;
                case StatModifierType.FlatPostMods:
                    flatPostMods.Add(modifier);
                    break;
                case StatModifierType.Percent:
                    percentModifiers.Add(modifier);
                    break;
                case StatModifierType.Multiplier:
                    multiplierModifiers.Add(modifier);
                    break;
                case StatModifierType.Capmax:
                    maxModifiers.Add(modifier);
                    break;
                case StatModifierType.Capmin:
                    minModifiers.Add(modifier);
                    break;
                case StatModifierType.OverrideFinal:
                    return modifier.Value;
                case StatModifierType.OverrideBase:
                    newBase = modifier.Value;
                    break;
            }
        }

        foreach (var mod in flatModifiers)
            newBase += mod.Value;
        float finalValue = newBase;
        foreach (StatModifier modifier in percentModifiers)
            finalValue += newBase * modifier.Value;
        foreach (StatModifier modifier in multiplierModifiers)
            finalValue *= 1 + modifier.Value;
        foreach (var mod in flatPostMods)
            finalValue += mod.Value;

        if (overCap)
            finalValue = Math.Min(finalValue, max);
        if (underCap)
            finalValue = Math.Max(finalValue, min);
        float minValue = minModifiers.Select(modifier => modifier.Value).Max();
        float maxValue = maxModifiers.Select(modifier => modifier.Value).Min();

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
        return typeof(CreatureStats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)).ToArray();
    }

    public static string GetUniqueStat(string stat, string partName)
    {
        return partName + "/" + stat;
    }
    public static string GetUniqueStat(string stat, BodyPart part)
    {
        return GetUniqueStat(stat, part.Name);
    }

    public static float GetAttributeModifier(float stat)
    {
        return stat/3;
    }
}
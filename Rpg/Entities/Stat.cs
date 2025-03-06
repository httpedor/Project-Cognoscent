using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Rpg;

public enum StatModifierType
{
    Flat,
    Percent,
    Multiplier
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

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
        stream.WriteFloat(Value);
        stream.WriteByte((byte)Type);
    }
}
public class Stat : ISerializable
{
    public event Action<float, float>? ValueChanged;
    public event Action<float, float>? BaseValueChanged;
    public event Action<StatModifier>? ModifierAdded;
    public event Action<StatModifier>? ModifierUpdated;
    public event Action<StatModifier>? ModifierRemoved;

    public string Id { get; private set; }
    private float baseValue;
    private float finalValue;
    private Dictionary<string, StatModifier> modifiers;

    public float BaseValue
    {
        get { return baseValue; }
        set
        {
            var old = baseValue;
            baseValue = value;
            BaseValueChanged?.Invoke(baseValue, old);
            CalculateFinalValue();
        }
    }

    public float FinalValue
    {
        get {
            return finalValue;
        }
    }

    public Stat(string id, float baseValue)
    {
        Id = id;
        this.baseValue = baseValue;
        modifiers = new Dictionary<string, StatModifier>();
    }

    public Stat(Stream stream)
    {
        Id = stream.ReadString();
        baseValue = stream.ReadFloat();
        modifiers = new Dictionary<string, StatModifier>();
        byte count = (Byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            StatModifier mod = new StatModifier(stream);
            modifiers[mod.Id] = mod;
        }
        CalculateFinalValue();
    }

    public void RemoveModifier(string id)
    {
        if (modifiers.ContainsKey(id))
        {
            modifiers.Remove(id);
            ModifierRemoved?.Invoke(modifiers[id]);
            CalculateFinalValue();
        }
    }
    public void RemoveModifier(StatModifier modifier)
    {
        RemoveModifier(modifier.Id);
    }

    public void SetModifier(StatModifier modifier)
    {
        var wasThere = modifiers.ContainsKey(modifier.Id);
        modifiers[modifier.Id] = modifier;
        CalculateFinalValue();
        ModifierUpdated?.Invoke(modifier);
        if (!wasThere)
            ModifierAdded?.Invoke(modifier);
    }

    public void AddDependency(Stat other, float weight, float maxVal = 1)
    {
        other.ValueChanged += (old, newVal) => {
            SetModifier(new StatModifier(other.Id + "_dep", -((maxVal - newVal) * weight), StatModifierType.Percent));
        };
    }

    public void AddDependents(params (Stat dependent, float weight)[] deps)
    {
        foreach (var tuple in deps)
            tuple.dependent.AddDependency(this, tuple.weight);
    }

    private void CalculateFinalValue()
    {
        var old = finalValue;
        var newBase = baseValue;
        finalValue = baseValue;
        List<StatModifier> percentModifiers = new List<StatModifier>();
        List<StatModifier> multiplierModifiers = new List<StatModifier>();
        foreach (StatModifier modifier in modifiers.Values)
        {
            if (modifier.Type == StatModifierType.Flat)
                newBase += modifier.Value;
            else if (modifier.Type == StatModifierType.Percent) 
                percentModifiers.Add(modifier);
            else if (modifier.Type == StatModifierType.Multiplier)
                multiplierModifiers.Add(modifier);
        }
        finalValue += newBase;
        foreach (StatModifier modifier in percentModifiers)
        {
            finalValue += newBase * modifier.Value;
        }
        foreach (StatModifier modifier in multiplierModifiers)
        {
            finalValue *= 1 + modifier.Value;
        }

        ValueChanged?.Invoke(old, finalValue);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
        stream.WriteFloat(baseValue);
        stream.WriteByte((byte)modifiers.Count);
        foreach (var modifier in modifiers.Values)
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

    public Dictionary<string, StatModifier>.ValueCollection GetModifiers()
    {
        return modifiers.Values;
    }
}

public static class CreatureStats
{
    public const string AGILITY = "agility";
    public const string INTELLIGENCE = "intelligence";
    public const string WISDOM = "wisdom";
    public const string UTILITY_STRENGTH = "utility strength";
    public const string MOVEMENT_STRENGTH = "movement strength";
    public const string PERCEPTION = "perception";
    public const string DEXTERITY = "dexterity";
    public const string JUMP = "jump";
    public const string RESPIRATION = "respiration";
    public const string BLOOD_FLOW = "blood flow";
    public const string CONSCIOUSNESS = "consciousness";
    public const string SIGHT = "sight";
    public const string HEARING = "hearing";
    public const string SOCIAL = "social";

    public static string[] GetAllStats()
    {
        return typeof(CreatureStats).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)).ToArray();
    }
}
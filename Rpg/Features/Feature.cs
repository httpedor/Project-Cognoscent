using System.Text.Json.Nodes;
using Rpg;

public abstract class Feature : ISerializable
{
    public string? CustomName;
    public string? CustomIcon;
    private readonly Dictionary<string, List<StatModifier>> statModifiers = new();

    public string BBHint => $"[hint={GetTooltip()}]{GetName()}[/hint]";

    public static Feature FromBytes(Stream bytes){
        string path = bytes.ReadString();
        var type = Type.GetType(path);

        if (type == null)
            throw new Exception("Failed to get feature type: " + path);
        if (type.GetConstructor(new Type[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get feature constructor: " + path);
        return (Feature)Activator.CreateInstance(type, bytes);
    }

    public static Feature? FromJson(string id, JsonObject? json)
    {
        string? type = json["type"]?.GetValue<string>();
        if (type == null)
        {
            Console.WriteLine("Feature type is null in JSON: " + json);
            return null;
        }
        Feature? feature = null;
        string? name = json["name"]?.GetValue<string>();
        string? icon = json["icon"]?.GetValue<string>();
        string? description = json["description"]?.GetValue<string>();
        bool toggleable = json["toggleable"]?.GetValue<bool>() ?? false;
        if (name == null)
        {
            Console.WriteLine("Feature name is null in JSON: " + json);
            return null;
        }
        if (icon == null)
        {
            Console.WriteLine("Feature icon is null in JSON: " + json);
            return null;
        }
        if (description == null)
        {
            Console.WriteLine("Feature description is null in JSON: " + json);
            return null;
        }
        switch (type)
        {
            case "damage_over_time":
            {
                string? dtName = json["damage_over_type"]?.GetValue<string>();
                if (dtName == null)
                {
                    Console.WriteLine("Damage type is null in JSON: " + json);
                    return null;
                }
                DamageType? dt = DamageType.FromName(dtName);
                if (dt == null)
                {
                    Console.WriteLine("Damage type not found: " + dtName);
                    return null;
                }
                float? damage = json["damage"]?.GetValue<float>();
                if (damage == null)
                {
                    Console.WriteLine("Damage is null in JSON: " + json);
                    return null;
                }
                uint interval = json["interval"]?.GetValue<uint>() ?? 0;
                

                feature = new DamageOverTimeCondition(id, name, description, dt, (double)damage, interval);
                break;
            }
            case "arbitrary":
            {
                break;
            }
            case "simple":
            {
                feature = new SimpleFeature(id, name, description, toggleable);
                break;
            }
            case "condition":
            {
                feature = new SimpleCondition(id, name, description, toggleable);
                break;
            }
            case "arbitrary_condition":
            {
                break;
            }
        }
        return feature;
    }

    protected Feature()
    {
        
    }

    protected Feature(Stream data)
    {
        if (data.ReadByte() != 0)
            CustomName = data.ReadString();
        if (data.ReadByte() != 0)
            CustomIcon = data.ReadString();
        int count = data.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string statId = data.ReadString();
            int modCount = data.ReadByte();
            for (int j = 0; j < modCount; j++)
            {
                StatModifier modifier = new(data);
                if (!statModifiers.ContainsKey(statId))
                    statModifiers[statId] = new List<StatModifier>();
                statModifiers[statId].Add(modifier);
            }
        }
    }
    
    public virtual void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteByte((byte)(CustomName == null ? 0 : 1));
        if (CustomName != null)
            stream.WriteString(CustomName);
        stream.WriteByte((byte)(CustomIcon == null ? 0 : 1));
        if (CustomIcon != null)
            stream.WriteString(CustomIcon);
        stream.WriteByte((byte)statModifiers.Count);
        foreach (var kvp in statModifiers)
        {
            stream.WriteString(kvp.Key);
            stream.WriteByte((byte)kvp.Value.Count);
            foreach (StatModifier modifier in kvp.Value)
            {
                modifier.ToBytes(stream);
            }
        }
    }
    
    public abstract string GetId();
    
    public virtual string GetName()
    {
        if (CustomName == null)
            return "";
        return CustomName;
    }
    public abstract string GetDescription();

    public virtual string GetTooltip()
    {
        return GetName() + "\n" + GetDescription();
    }

    public virtual string GetIconName()
    {
        if (CustomIcon != null)
            return CustomIcon;
        return GetName().ToLower();
    }

    public virtual void Enable(IFeatureSource source)
    {
        if (source is not Entity entity) return;
        
        foreach (var kvp in statModifiers)
        {
            var stat = entity.GetStat(kvp.Key);
            if (stat == null)
                continue;
            foreach (StatModifier modifier in kvp.Value)
            {
                stat.SetModifier(modifier);
            }
        }
    }
    public virtual void Disable(IFeatureSource source)
    {
        if (source is not Entity entity) return;
        
        foreach (var kvp in statModifiers)
        {
            var stat = entity.GetStat(kvp.Key);
            if (stat == null)
                continue;
            foreach (StatModifier modifier in kvp.Value)
            {
                stat.RemoveModifier(modifier);
            }
        }
    }

    public virtual void OnTick(IFeatureSource entity)
    {
        
    }

    /// <summary>
    /// Called when an AttackSkill is about to hit an IFeatureSource with this Feature
    /// </summary>
    /// <param name="source">The source of this feature. Keep in mind this is also an IFeatureSource</param>
    /// <param name="damage">The DamageSource</param>
    /// <param name="hit">The "default" state before this feature affects the hit.</param>
    /// <returns>A tuple with a boolean representing if it did hit, and if it didn't, a string with the reason(this can be null)</returns>
    public virtual (bool, string?) DoesGetAttacked(IDamageable source, DamageSource damage, bool hit)
    {
        return (true, null);
    }

    public virtual (bool, string?) DoesAttack(IFeatureSource source, IDamageable attacked, DamageSource damage, bool hit)
    {
        return (true, null);
    }
    public virtual (bool, string?) DoesExecuteSkill(Creature executor, Skill skill, List<SkillArgument> arguments)
    {
        return (true, null);
    }

    public virtual IEnumerable<StatModifier> ModifyReceivingDamageModifiers(IDamageable attacked, DamageSource source, double damage)
    {
        return Array.Empty<StatModifier>();
    }
    
    public virtual double ModifyReceivingDamage(IDamageable attacked, DamageSource source, double damage)
    {
        return damage;
    }
    
    public virtual double ModifyAttackingDamage(Creature attacker, IDamageable target, DamageSource source, double damage)
    {
        return damage;
    }

    public virtual void OnAttacked(IDamageable attacked, DamageSource source, double damage, bool hit)
    {
        
    }

    public virtual void OnAttack(Creature attacker, IDamageable target, DamageSource source, double damage, bool hit)
    {
        
    }

    public virtual void OnExecuteSkill(Creature executor, Skill skill, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        
    }
    
    public virtual bool IsToggleable(Entity entity)
    {
        return false;
    }
    
    public Feature WithName(string name)
    {
        CustomName = name;
        return this;
    }
    public Feature WithIcon(string icon)
    {
        CustomIcon = icon;
        return this;
    }

    public Feature WithMod(string statId, StatModifier modifier)
    {
        if (!statModifiers.ContainsKey(statId))
            statModifiers[statId] = new List<StatModifier>();
        statModifiers[statId].Add(modifier);
        return this;
    }
}
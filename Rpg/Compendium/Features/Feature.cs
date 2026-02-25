using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg.Features;

public abstract class Feature : ISerializable
{
    public string? CustomName;
    public string? CustomIcon;
    private readonly Dictionary<string, List<StatModifier>> statModifiers = new();

    public string BBHint => $"[hint={GetTooltip()}]{GetName()}[/hint]";

    public static Feature FromBytes(Stream bytes){
        string path = bytes.ReadString();
        var type = Type.GetType(path);
        if (type != null && (type.IsAssignableTo(typeof(ArbitraryFeature))))
        {
            string id = bytes.ReadString();
            var ret = Compendium.GetEntry<Feature>(id);
            if (ret == null)
                throw new Exception("Failed to get feature from compendium: " + id);
            return ret;
        }

        if (type == null)
            throw new Exception("Failed to get feature type: " + path);
        if (type.GetConstructor(new Type[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get feature constructor: " + path);
        return (Feature)Activator.CreateInstance(type, bytes)!;
    }

    public static Feature? FromJson(string id, JsonElement json)
    {
        string? type = json.GetProperty("type").GetString();
        if (type == null)
            type = "arbitrary";
        Feature? feature = null;
        string? name = json.GetProperty("name").GetString();
        string? icon = json.GetProperty("icon").GetString();
        string? description = json.GetProperty("description").GetString();
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
                string? dtName = json.TryGetProperty("damage_type", out JsonElement dtNameElement) ? dtNameElement.GetString() : null;
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
                float? damage = json.TryGetProperty("damage", out JsonElement damageElement) ? damageElement.GetSingle() : null;
                if (damage == null)
                {
                    Console.WriteLine("Damage is null in JSON: " + json);
                    return null;
                }
                uint interval = json.TryGetProperty("interval", out JsonElement intervalElement) ? intervalElement.GetUInt32() : 0;
                

                feature = new DamageOverTimeCondition(id, name, description, dt, damage.Value, interval);
                break;
            }
            case "arbitrary":
            {
                EffectExpr? onTick = json.TryGetProperty("tick", out JsonElement onTickElement) ? ExpressionCompiler.CompileEffect(onTickElement) : null;
                EffectExpr? onEnable = json.TryGetProperty("enable", out JsonElement onEnableElement) ? ExpressionCompiler.CompileEffect(onEnableElement) : null;
                EffectExpr? onDisable = json.TryGetProperty("disable", out JsonElement onDisableElement) ? ExpressionCompiler.CompileEffect(onDisableElement) : null;
                (Expr<bool>, Expr<string>)? doesGetAttacked = null;
                if (json.TryGetProperty("doesGetAttacked", out JsonElement doesGetAttackedElement))
                {
                    var condition = ExpressionCompiler.CompileCondition(doesGetAttackedElement);
                    Expr<string>? reason = new StringLiteralExpr("");
                    if (doesGetAttackedElement.TryGetProperty("reason", out JsonElement reasonElement))
                    {
                        reason = ExpressionCompiler.CompileString(reasonElement);
                    }
                    doesGetAttacked = (condition, reason);
                }
                (Expr<bool>, Expr<string>)? doesAttack = null;
                if (json.TryGetProperty("doesAttack", out JsonElement doesAttackElement))
                {
                    var condition = ExpressionCompiler.CompileCondition(doesAttackElement);
                    Expr<string>? reason = new StringLiteralExpr("");
                    if (doesAttackElement.TryGetProperty("reason", out JsonElement reasonElement))
                    {
                        reason = ExpressionCompiler.CompileString(reasonElement);
                    }
                    doesAttack = (condition, reason);
                }
                (Expr<bool>, Expr<string>)? doesExecuteSkill = null;
                if (json.TryGetProperty("doesExecuteSkill", out JsonElement doesExecuteSkillElement))
                {
                    var condition = ExpressionCompiler.CompileCondition(doesExecuteSkillElement);
                    Expr<string>? reason = new StringLiteralExpr("");
                    if (doesExecuteSkillElement.TryGetProperty("reason", out JsonElement reasonElement))
                    {
                        reason = ExpressionCompiler.CompileString(reasonElement);
                    }
                    doesExecuteSkill = (condition, reason);
                }
                EffectExpr? onAttacked = json.TryGetProperty("attacked", out JsonElement onAttackedElement) ? ExpressionCompiler.CompileEffect(onAttackedElement) : null;
                EffectExpr? onAttack = json.TryGetProperty("attack", out JsonElement onAttackElement) ? ExpressionCompiler.CompileEffect(onAttackElement) : null;
                EffectExpr? onExecuteSkill = json.TryGetProperty("executeSkill", out JsonElement onExecuteSkillElement) ? ExpressionCompiler.CompileEffect(onExecuteSkillElement) : null;
                EffectExpr? onInjured = json.TryGetProperty("injured", out JsonElement onInjuredElement) ? ExpressionCompiler.CompileEffect(onInjuredElement) : null;
                (Expr<float>, Expr<string>)? modifyReceivingDamage = null;
                if (json.TryGetProperty("receivingDamage", out JsonElement modifyReceivingDamageElement))
                {
                    var numberExpr = ExpressionCompiler.CompileNumber(modifyReceivingDamageElement);
                    Expr<string>? reason = new StringLiteralExpr("");
                    if (modifyReceivingDamageElement.TryGetProperty("formula", out JsonElement reasonElement))
                    {
                        reason = ExpressionCompiler.CompileString(reasonElement);
                    }
                    modifyReceivingDamage = (numberExpr, reason);
                }
                (Expr<float>, Expr<string>)? modifyAttackingDamage = null;
                if (json.TryGetProperty("attackingDamage", out JsonElement modifyAttackingDamageElement))
                {
                    var numberExpr = ExpressionCompiler.CompileNumber(modifyAttackingDamageElement);
                    Expr<string>? reason = new StringLiteralExpr("");
                    if (modifyAttackingDamageElement.TryGetProperty("formula", out JsonElement reasonElement))
                    {
                        reason = ExpressionCompiler.CompileString(reasonElement);
                    }
                    modifyAttackingDamage = (numberExpr, reason);
                }

                Expr<bool> toggleable = json.TryGetProperty("toggleable", out JsonElement toggleableElement) ? ExpressionCompiler.CompileCondition(toggleableElement) : new ConstConditionExpr(false);

                CompendiumEntryExpr<Skill>[]? skillExprs = null;
                if (json.TryGetProperty("skills", out JsonElement skillsElement) && skillsElement.ValueKind == JsonValueKind.Array)
                {
                    List<CompendiumEntryExpr<Skill>> skillExprsList = new();
                    foreach (JsonElement skillElement in skillsElement.EnumerateArray())
                    {
                        var skillExpr = ExpressionCompiler.CompileCompendiumEntry<Skill>(skillElement);
                        if (skillExpr != null)
                            skillExprsList.Add(skillExpr);
                    }
                    skillExprs = skillExprsList.ToArray();
                }
                feature = new ArbitraryFeature(
                    id, name, description,
                    onTick, onEnable, onDisable,
                    doesGetAttacked, doesAttack, doesExecuteSkill,
                    onAttacked, onAttack, onExecuteSkill, onInjured,
                    modifyReceivingDamage, modifyAttackingDamage,
                    skillExprs,
                    toggleable
                );
                break;
            }
            case "simple":
            {
                bool hidden = json.TryGetProperty("hidden", out JsonElement hiddenElement) ? hiddenElement.GetBoolean() : false;
                bool toggleable = json.TryGetProperty("toggleable", out JsonElement toggleableElement) ? toggleableElement.GetBoolean() : false;
                feature = new SimpleFeature(id, name, description, hidden, toggleable);
                break;
            }
            case "condition":
            {
                bool hidden = json.TryGetProperty("hidden", out JsonElement hiddenElement) ? hiddenElement.GetBoolean() : false;
                bool toggleable = json.TryGetProperty("toggleable", out JsonElement toggleableElement) ? toggleableElement.GetBoolean() : false;
                feature = new SimpleCondition(id, name, description, toggleable, 0, hidden);
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

    public virtual void OnAdded(FeaturesContainer source)
    {
    }

    public virtual void OnRemoved(FeaturesContainer source)
    {
    }

    public virtual void OnEnable(FeaturesContainer source)
    {
        if (!source.Entity.TryGetComponent<StatsContainer>(out var stats))
        {
            return;
        }
        
        foreach (var kvp in statModifiers)
        {
            var stat = stats.GetStat(kvp.Key);
            if (stat == null)
                continue;
            foreach (StatModifier modifier in kvp.Value)
            {
                stat.SetModifier(modifier);
            }
        }
    }
    public virtual void OnDisable(FeaturesContainer source)
    {
        if (!source.Entity.TryGetComponent<StatsContainer>(out var stats))
        {
            return;
        }
        
        foreach (var kvp in statModifiers)
        {
            var stat = stats.GetStat(kvp.Key);
            if (stat == null)
                continue;
            foreach (StatModifier modifier in kvp.Value)
            {
                stat.RemoveModifier(modifier);
            }
        }
    }

    public virtual void OnTick(FeaturesContainer source)
    {
        
    }

    /// <summary>
    /// Called when an AttackSkill is about to hit an Entity with this Feature
    /// </summary>
    /// <param name="source">The source of this feature. Keep in mind this is also an IFeatureSource</param>
    /// <param name="damage">The DamageSource</param>
    /// <param name="hit">The "default" state before this feature affects the hit.</param>
    /// <returns>A tuple with a boolean representing if it did hit, and if it didn't, a string with the reason(this can be null)</returns>
    public virtual (bool, string?) DoesGetAttacked(FeaturesContainer source, IDamageable attacked, DamageSource damage, bool hit)
    {
        return (hit, null);
    }

    public virtual (bool, string?) DoesAttack(SkillExecutor source, IDamageable attacked, DamageSource damage, bool hit)
    {
        return (hit, null);
    }
    public virtual (bool, string?) DoesExecuteSkill(SkillExecutor executor, Skill skill, List<SkillArgument> arguments)
    {
        return (true, null);
    }

    public virtual IEnumerable<StatModifier> ModifyAttackingDamageModifiers(SkillExecutor attacker, DamageInstance damage)
    {
        return Array.Empty<StatModifier>();
    }
    public virtual IEnumerable<StatModifier> ModifyReceivingDamageModifiers(FeaturesContainer attacked, DamageInstance damage)
    {
        return Array.Empty<StatModifier>();
    }
    
    public virtual (double, string?) ModifyReceivingDamage(FeaturesContainer attacked, IDamageable target, DamageInstance damage)
    {
        return (damage.Amount, null);
    }
    
    public virtual (double, string?) ModifyAttackingDamage(SkillExecutor attacker, IDamageable target, DamageInstance damage)
    {
        return (damage.Amount, null);
    }

    public virtual void OnAttacked(FeaturesContainer attacked, IDamageable target, DamageInstance damage, bool hit)
    {
        
    }

    public virtual void OnAttack(SkillExecutor attacker, IDamageable target, DamageInstance damage, bool hit)
    {
        
    }

    public virtual void OnExecuteSkill(SkillExecutor executor, Skill skill, List<SkillArgument> arguments, uint tick)
    {
        
    }

    public virtual void OnInjured(FeaturesContainer source, IDamageable injured, Injury injury)
    {
        
    }
    
    public virtual IEnumerable<Skill> GetSkills(FeaturesContainer source, SkillExecutor executor)
    {
        return Array.Empty<Skill>();
    }
    
    public virtual bool CanBeSeenBy(Entity viewer)
    {
        return true;
    }
    
    public virtual bool IsToggleable(FeaturesContainer entity)
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
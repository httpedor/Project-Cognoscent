using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg.Features;

public class Feature : ISerializable
{
    public string Id;
    public string Name;
    public string Icon;
    public string Description;
    public string Tooltip => Name + "\n" + Description;
    private Dictionary<Expr<string>, List<(Expr<float>, Expr<StatModifierType>)>> statModifiers = new();
    private EffectExpr? tickExpr;
    private EffectExpr? enableExpr;
    private EffectExpr? disableExpr;
    private (Expr<bool>, Expr<string>)? doesGetAttackedExpr;
    private (Expr<bool>, Expr<string>)? doesAttackExpr;
    private (Expr<bool>, Expr<string>)? doesExecuteSkillExpr;
    private EffectExpr? attackedExpr;
    private EffectExpr? attackExpr;
    private EffectExpr? executeSkillExpr;
    private EffectExpr? injuredExpr;
    private (Expr<float>, Expr<string>)? modifyReceivingDamage;
    private (Expr<float>, Expr<string>)? modifyAttackingDamage;
    private Expr<Skill>[] skills;
    private Expr<bool>? toggleable;

    public string BBHint => $"[hint={Tooltip}]{Name}[/hint]";

    public static Feature FromBytes(Stream bytes){
        string id = bytes.ReadString();
        var ret = Compendium.GetEntry<Feature>(id);
        if (ret == null)
            throw new Exception("Failed to get feature from compendium: " + id);
        return ret;
    }

    public static Feature? FromJson(string id, JsonElement json)
    {
        string? type = json.GetProperty("type").GetString();
        if (type == null)
            type = "simple";
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

        EffectExpr? onTick = json.TryGetProperty("tick", out JsonElement onTickElement) ? ExpressionCompiler.CompileEffect(onTickElement) : null;
        EffectExpr? onEnable = json.TryGetProperty("enable", out JsonElement onEnableElement) ? ExpressionCompiler.CompileEffect(onEnableElement) : null;
        EffectExpr? onDisable = json.TryGetProperty("disable", out JsonElement onDisableElement) ? ExpressionCompiler.CompileEffect(onDisableElement) : null;
        (Expr<bool>, Expr<string>)? doesGetAttacked = null;
        if (json.TryGetProperty("doesGetAttacked", out JsonElement doesGetAttackedElement))
        {
            var condition = ExpressionCompiler.Compile<bool>(doesGetAttackedElement);
            Expr<string>? reason = new StringLiteralExpr("");
            if (doesGetAttackedElement.TryGetProperty("reason", out JsonElement reasonElement))
            {
                reason = ExpressionCompiler.Compile<string>(reasonElement);
            }
            doesGetAttacked = (condition, reason);
        }
        (Expr<bool>, Expr<string>)? doesAttack = null;
        if (json.TryGetProperty("doesAttack", out JsonElement doesAttackElement))
        {
            var condition = ExpressionCompiler.Compile<bool>(doesAttackElement);
            Expr<string>? reason = new StringLiteralExpr("");
            if (doesAttackElement.TryGetProperty("reason", out JsonElement reasonElement))
            {
                reason = ExpressionCompiler.Compile<string>(reasonElement);
            }
            doesAttack = (condition, reason);
        }
        (Expr<bool>, Expr<string>)? doesExecuteSkill = null;
        if (json.TryGetProperty("doesExecuteSkill", out JsonElement doesExecuteSkillElement))
        {
            var condition = ExpressionCompiler.Compile<bool>(doesExecuteSkillElement);
            Expr<string>? reason = new StringLiteralExpr("");
            if (doesExecuteSkillElement.TryGetProperty("reason", out JsonElement reasonElement))
            {
                reason = ExpressionCompiler.Compile<string>(reasonElement);
            }
            doesExecuteSkill = (condition, reason);
        }
        EffectExpr? onAttacked = json.TryGetProperty("attacked", out JsonElement onAttackedElement) ? ExpressionCompiler.CompileEffect(onAttackedElement) : null;
        EffectExpr? onAttack = json.TryGetProperty("attack", out JsonElement onAttackElement) ? ExpressionCompiler.CompileEffect(onAttackElement) : null;
        EffectExpr? onExecuteSkill = json.TryGetProperty("onExecuteSkill", out JsonElement onExecuteSkillElement) ? ExpressionCompiler.CompileEffect(onExecuteSkillElement) : null;
        EffectExpr? onInjured = json.TryGetProperty("injured", out JsonElement onInjuredElement) ? ExpressionCompiler.CompileEffect(onInjuredElement) : null;
        (Expr<float>, Expr<string>)? modifyReceivingDamage = null;
        if (json.TryGetProperty("receivingDamage", out JsonElement modifyReceivingDamageElement))
        {
            var numberExpr = ExpressionCompiler.Compile<float>(modifyReceivingDamageElement);
            Expr<string>? reason = new StringLiteralExpr("");
            if (modifyReceivingDamageElement.TryGetProperty("formula", out JsonElement reasonElement))
            {
                reason = ExpressionCompiler.Compile<string>(reasonElement);
            }
            modifyReceivingDamage = (numberExpr, reason);
        }
        (Expr<float>, Expr<string>)? modifyAttackingDamage = null;
        if (json.TryGetProperty("attackingDamage", out JsonElement modifyAttackingDamageElement))
        {
            var numberExpr = ExpressionCompiler.Compile<float>(modifyAttackingDamageElement);
            Expr<string>? reason = new StringLiteralExpr("");
            if (modifyAttackingDamageElement.TryGetProperty("formula", out JsonElement reasonElement))
            {
                reason = ExpressionCompiler.Compile<string>(reasonElement);
            }
            modifyAttackingDamage = (numberExpr, reason);
        }
        Dictionary<Expr<string>, List<(Expr<float>, Expr<StatModifierType>)>> statModifiers = new();
        if (json.TryGetProperty("statModifiers", out JsonElement statMods))
        {
            if (statMods.ValueKind != JsonValueKind.Object)
            {
                Console.WriteLine("statModifiers must be an object in JSON: " + json);
                return null;
            }
            foreach (JsonProperty statMod in statMods.EnumerateObject())
            {
                string statId = statMod.Name;
                JsonElement modifiersArray = statMod.Value;
                if (modifiersArray.ValueKind != JsonValueKind.Array)
                {
                    Console.WriteLine("statModifiers for each stat must be an array in JSON: " + json);
                    return null;
                }
                List<(Expr<float>, Expr<StatModifierType>)> modifiersList = new();
                foreach (JsonElement modifierElement in modifiersArray.EnumerateArray())
                {
                    if (modifierElement.ValueKind != JsonValueKind.Object)
                    {
                        Console.WriteLine("Each statModifier must be an object in JSON: " + json);
                        return null;
                    }
                    if (!modifierElement.TryGetProperty("type", out JsonElement typeElement))
                    {
                        Console.WriteLine("Each statModifier must have a string 'type' property in JSON: " + json);
                        return null;
                    }
                    if (!modifierElement.TryGetProperty("value", out JsonElement valueElement))
                    {
                        Console.WriteLine("Each statModifier must have a 'value' property in JSON: " + json);
                        return null;
                    }
                    var typeExpr = ExpressionCompiler.Compile<StatModifierType>(typeElement);
                    var valueExpr = ExpressionCompiler.Compile<float>(valueElement);
                    modifiersList.Add((valueExpr, typeExpr));
                }
                statModifiers[new StringLiteralExpr(statId)] = modifiersList;
            }
        }
        Expr<bool> toggleable = json.TryGetProperty("toggleable", out JsonElement toggleableElement) ? ExpressionCompiler.Compile<bool>(toggleableElement) : new ConstConditionExpr(false);

        Expr<Skill>[]? skillExprs = null;
        if (json.TryGetProperty("skills", out JsonElement skillsElement) && skillsElement.ValueKind == JsonValueKind.Array)
        {
            List<Expr<Skill>> skillExprsList = new();
            foreach (JsonElement skillElement in skillsElement.EnumerateArray())
            {
                var skillExpr = ExpressionCompiler.Compile<Skill>(skillElement);
                if (skillExpr != null)
                    skillExprsList.Add(skillExpr);
            }
            skillExprs = skillExprsList.ToArray();
        }
        bool hidden = json.TryGetProperty("hidden", out JsonElement hiddenElement) ? hiddenElement.GetBoolean() : false;
        switch (type)
        {
            case "damage_over_time":
            {
                Expr<DamageType?> dt;
                if (json.TryGetProperty("damage_type", out JsonElement dtNameElement))
                {
                    dt = ExpressionCompiler.Compile<DamageType>(dtNameElement);
                }
                else
                {
                    Logger.LogError($"DamageOverTimeCondition {id} is missing 'damage_type' property in JSON: {json}");
                    return null;
                }
                Expr<float> damage;
                if (json.TryGetProperty("damage", out JsonElement damageElement))
                {
                    damage = ExpressionCompiler.Compile<float>(damageElement);
                }
                else
                {
                    Logger.LogError($"DamageOverTimeCondition {id} is missing 'damage' property in JSON: {json}");
                    return null;
                }
                Expr<float> interval;
                if (json.TryGetProperty("interval", out JsonElement intervalElement))
                {
                    interval = ExpressionCompiler.Compile<float>(intervalElement);
                }
                else
                {
                    Logger.LogError($"DamageOverTimeCondition {id} is missing 'interval' property in JSON: {json}");
                    return null;
                }

                feature = new DamageOverTimeCondition(id, name, description, icon, dt, damage, interval);
                break;
            }
            case "simple":
            {
                feature = new Feature(id, name, description, icon);
                break;
            }
            case "condition":
            {
                Expr<float>? defaultDuration = null;
                if (json.TryGetProperty("defaultDuration", out JsonElement defaultDurationElement))
                {
                    defaultDuration = ExpressionCompiler.Compile<float>(defaultDurationElement);
                }
                feature = new ConditionFeature(id, name, description, icon)
                {
                    DefaultDuration = defaultDuration ?? new ConstNumberExpr(1)
                };
                break;
            }
        }
        if (feature == null)
        {
            Console.WriteLine("Invalid feature type: " + type);
            return null;
        }
        feature.toggleable = toggleable;
        feature.attackedExpr = onAttacked;
        feature.attackExpr = onAttack;
        feature.executeSkillExpr = onExecuteSkill;
        feature.injuredExpr = onInjured;
        feature.doesGetAttackedExpr = doesGetAttacked;
        feature.doesAttackExpr = doesAttack;
        feature.doesExecuteSkillExpr = doesExecuteSkill;
        feature.modifyReceivingDamage = modifyReceivingDamage;
        feature.modifyAttackingDamage = modifyAttackingDamage;
        feature.skills = skillExprs ?? Array.Empty<CompendiumEntryExpr<Skill>>();
        feature.disableExpr = onDisable;
        feature.enableExpr = onEnable;
        feature.tickExpr = onTick;
        feature.statModifiers = statModifiers;
        return feature;
    }

    public Feature(string id, string name, string description, string icon = "")
    {
        Id = id;
        Name = name;
        Description = description;
        Icon = icon;
        skills = [];
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
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
        
        var ctx = new EvalContext()
        {
            Target = source.Entity,
            Caller = source.Entity,
            Board = source.Entity.Board
        };
        int i = 0;
        foreach (var kvp in statModifiers)
        {
            var stat = stats.GetStat(kvp.Key.Eval(ctx));
            if (stat == null)
                continue;
            foreach (var modTuple in kvp.Value)
            {
                var mod = new StatModifier($"{Id}-{i}", modTuple.Item1.Eval(ctx), modTuple.Item2.Eval(ctx));
                stat.SetModifier(mod);
                i++;
            }
        }
        enableExpr?.Eval(source.Entity);
    }
    public virtual void OnDisable(FeaturesContainer source)
    {
        if (!source.Entity.TryGetComponent<StatsContainer>(out var stats))
        {
            return;
        }
        
        foreach (var stat in stats.Stats)
        {
            foreach (var mod in stat.GetModifiers())
            {
                if (mod.Id.StartsWith(Id + "-"))
                {
                    stat.RemoveModifier(mod.Id);
                }
            }
        }
        disableExpr?.Eval(source.Entity);
    }

    public virtual void OnTick(FeaturesContainer source)
    {
        tickExpr?.Eval(source.Entity);
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
        if (doesGetAttackedExpr != null)
        {
            var ctx = new EvalContext(
                hit,
                damage.Type,
                null!
            ) {
                Target = damage.Attacker?.Entity,
                Caller = source.Entity,
                TargetComponent = attacked is BodyPart bp ? bp : null,
                Board = source.Entity.Board,
            };
            var ret = doesGetAttackedExpr.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, doesGetAttackedExpr.Value.Item2.Eval(ctx));
        }
        return (hit, null);
    }

    public virtual (bool, string?) DoesAttack(SkillExecutor source, IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesAttackExpr != null)
        {
            var ctx = new EvalContext(
                hit,
                damage.Type,
                null!
            ) {
                Target = damage.Attacker?.Entity,
                Caller = source.Entity,
                TargetComponent = attacked is BodyPart bp ? bp : null,
                Board = source.Entity.Board,
            };
            var ret = doesAttackExpr.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, doesAttackExpr.Value.Item2.Eval(ctx));
        }
        return (hit, null);
    }
    public virtual (bool, string?) DoesExecuteSkill(SkillExecutor executor, Skill skill, List<SkillArgument> arguments)
    {
        if (doesExecuteSkillExpr != null)
        {
            var arr = new object[arguments.Count + 1];
            arr[0] = skill.GetName();
            for (int i = 0; i < arguments.Count; i++)
            {
                arr[i+1] = arguments[i];
            }
            var ctx = new EvalContext(arr)
            {
                Caller = executor.Entity,
                Target = executor.Entity,
                Board = executor.Entity.Board,
            };
            return (doesExecuteSkillExpr.Value.Item1.Eval(ctx), doesExecuteSkillExpr.Value.Item2.Eval(ctx));
        }
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
        if (modifyReceivingDamage != null)
        {
            var ctx = new EvalContext(
                damage.Amount,
                damage.Source.Type,
                null!
            ) {
                Target = damage.Source.Attacker?.Entity,
                Caller = attacked.Entity,
                TargetComponent = target is BodyPart bp ? bp : null,
                Board = attacked.Entity.Board,
            };
            var ret = modifyReceivingDamage.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, modifyReceivingDamage.Value.Item2.Eval(ctx));
        }
        return (damage.Amount, null);
    }
    
    public virtual (double, string?) ModifyAttackingDamage(SkillExecutor attacker, IDamageable target, DamageInstance damage)
    {
        if (modifyAttackingDamage != null)
        {
            var ctx = new EvalContext(
                damage.Amount,
                damage.Source.Type,
                null!
            ) {
                Target = damage.Source.Attacker?.Entity,
                Caller = attacker.Entity,
                TargetComponent = target is BodyPart bp ? bp : null,
                Board = attacker.Entity.Board,
            };
            var ret = modifyAttackingDamage.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, modifyAttackingDamage.Value.Item2.Eval(ctx));
        }
        return (damage.Amount, null);
    }

    public virtual void OnAttacked(FeaturesContainer attacked, IDamageable target, DamageInstance damage, bool hit)
    {
        if (attackedExpr == null)
            return;
        var ctx = new EvalContext(
            hit,
            damage.Source.Type,
            damage.Amount
        ) {
            Target = damage.Source.Attacker?.Entity,
            Caller = attacked.Entity,
            TargetComponent = target is BodyPart bp ? bp : null,
            Board = attacked.Entity.Board,
        };
        attackedExpr.Eval(ctx);
    }

    public virtual void OnAttack(SkillExecutor attacker, IDamageable target, DamageInstance damage, bool hit)
    {
        if (attackExpr == null)
            return;
        var ctx = new EvalContext(
            hit,
            damage.Source.Type,
            damage.Amount
        ) {
            Target = damage.Source.Attacker?.Entity,
            Caller = attacker.Entity,
            TargetComponent = target is BodyPart bp ? bp : null,
            Board = attacker.Entity.Board,
        };
        attackExpr.Eval(ctx);
    }

    public virtual void OnExecuteSkill(SkillExecutor executor, Skill skill, List<SkillArgument> arguments, uint tick)
    {
        if (executeSkillExpr == null)
            return;
        var arr = new object[arguments.Count + 1];
        arr[0] = skill.GetName();
        for (int i = 0; i < arguments.Count; i++)
        {
            arr[i+1] = arguments[i].Value;
        }
        var ctx = new EvalContext(arr)
        {
            Caller = executor.Entity,
            Target = executor.Entity,
            Board = executor.Entity.Board,
        };
        executeSkillExpr.Eval(ctx);
    }

    public virtual void OnInjured(FeaturesContainer source, IDamageable injured, Injury injury)
    {
        injuredExpr?.Eval(new EvalContext(
            injury.Severity,
            injury.Type.Id
        ) {
            Target = injured is BodyPart bp ? bp.OwnerEntity : (injured is Component c ? c.Entity : null),
            Caller = source.Entity,
            TargetComponent = injured is BodyPart bp2 ? bp2 : null,
            Board = source.Entity.Board,
        });
    }
    
    public virtual IEnumerable<BodyPosture> GetProvidedPostures(FeaturesContainer source)
    {
        return Array.Empty<BodyPosture>();
    }
    
    public virtual IEnumerable<Skill> GetSkills(FeaturesContainer source, SkillExecutor executor)
    {
        foreach (var skillRef in skills)
        {
            var skill = skillRef.Eval(source.Entity);
            if (skill != null)
                yield return skill;
        }
    }
    
    public virtual bool CanBeSeenBy(Entity viewer)
    {
        return true;
    }
    
    public virtual bool IsToggleable(FeaturesContainer entity)
    {
        return toggleable?.Eval(entity.Entity) ?? false;
    }
    
    public Feature WithName(string name)
    {
        Name = name;
        return this;
    }
    public Feature WithIcon(string icon)
    {
        Icon = icon;
        return this;
    }
}
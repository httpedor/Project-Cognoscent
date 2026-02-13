using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Health;
using Rpg.Scripting;

namespace Rpg.Skills;

public class SkillExpressions
{
    public readonly EffectExpr? onExecute;
    public readonly EffectExpr? onStart;
    public readonly EffectExpr? onCancel;
    public readonly ConditionExpr? canCancel;
    public readonly NumberExpr? delay;
    public readonly NumberExpr? cooldown;
    public readonly NumberExpr? duration;
    public readonly ConditionExpr? canExecute;
    public readonly StringExpr[]? layers;
    
    public readonly (NumberExpr, CompendiumEntryExpr<DamageType>)? damage;
    public readonly (ConditionExpr, StringExpr)? doesHit;
    public readonly EffectExpr? onHit;
    public readonly EffectExpr? onAttack;
    public readonly ConditionExpr? condition;
    public readonly ConditionExpr? canTarget;
    public readonly Type[][] argumentTypes;
    
    public SkillExpressions(JsonElement obj)
    {
        T? TryCompile<T>(string property) where T : BaseExpr
        {
            if (obj.GetPropertyOrNull(property) is JsonElement prop)
            {
                if (typeof(T) == typeof(EffectExpr))
                {
                    var expr = Skill.DefaultCompilerContext.CompileEffect(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(ConditionExpr))
                {
                    var expr = Skill.DefaultCompilerContext.CompileCondition(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(NumberExpr))
                {
                    var expr = Skill.DefaultCompilerContext.CompileNumber(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(StringExpr))
                {
                    var expr = Skill.DefaultCompilerContext.CompileString(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(CompendiumEntryExpr<DamageType>))
                {
                    var expr = Skill.DefaultCompilerContext.CompileCompendiumEntry<DamageType>(prop);
                    return (T)(BaseExpr)expr;
                }
            }
            return null;
        }
        // Basic skill properties
        onExecute = TryCompile<EffectExpr>("execute");
        onStart = TryCompile<EffectExpr>("start");
        onCancel = TryCompile<EffectExpr>("cancel");
        canCancel = TryCompile<ConditionExpr>("canCancel");
        delay = TryCompile<NumberExpr>("delay");
        cooldown = TryCompile<NumberExpr>("cooldown");
        duration = TryCompile<NumberExpr>("duration");
        if (obj.GetPropertyOrNull("layers") is JsonElement layersElem && layersElem.ValueKind == JsonValueKind.Array)
        {
            var list = new List<StringExpr>();
            foreach (var item in layersElem.EnumerateArray())
            {
                var expr = Skill.DefaultCompilerContext.CompileString(item);
                list.Add(expr);
            }
            layers = list.ToArray();
        }
        canExecute = TryCompile<ConditionExpr>("canExecute");
        
        // Attack skill properties
        onHit = TryCompile<EffectExpr>("onHit");
        onAttack = TryCompile<EffectExpr>("onAttack");
        if (obj.GetPropertyOrNull("damage") is JsonElement damageElem && damageElem.ValueKind == JsonValueKind.Object)
        {
            var numberExpr = Skill.DefaultCompilerContext.CompileNumber(damageElem.GetProperty("amount"));
            var damageTypeExpr = Skill.DefaultCompilerContext.CompileCompendiumEntry<DamageType>(damageElem.GetProperty("type"));
            damage = (numberExpr, damageTypeExpr);
        }
        if (obj.GetPropertyOrNull("doesHit") is JsonElement doesHitElem && doesHitElem.ValueKind == JsonValueKind.Object)
        {
            var conditionExpr = Skill.DefaultCompilerContext.CompileCondition(doesHitElem.GetProperty("condition"));
            var stringExpr = Skill.DefaultCompilerContext.CompileString(doesHitElem.GetProperty("description"));
            doesHit = (conditionExpr, stringExpr);
        }
        condition = TryCompile<ConditionExpr>("condition");
        canTarget = TryCompile<ConditionExpr>("canTarget");
        if (obj.GetPropertyOrNull("arguments") is JsonElement argumentsElem && argumentsElem.ValueKind == JsonValueKind.Array)
        {
            var list = new List<Type[]>();
            foreach (var item in argumentsElem.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Array)
                {
                    var argList = new List<Type>();
                    foreach (var arg in item.EnumerateArray())
                    {
                        if (arg.GetString() is string typeStr)
                            argList.Add(SkillArgument.ArgumentTypeFromString(typeStr));
                    }
                    list.Add(argList.ToArray());
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    if (item.GetString() is string typeStr)
                        list.Add(new[] { SkillArgument.ArgumentTypeFromString(typeStr) });
                }
            }
            argumentTypes = list.ToArray();
        }
        else
        {
            argumentTypes = Array.Empty<Type[]>();
        }
    }

    public EvalContext CreateEvalContext(SkillExecutor exec, List<SkillArgument> args, Dictionary<string, object>? otherArgs = null)
    {
        var context = new EvalContext
        {

            Caller = exec.Entity,
            Variables = new object[args.Count + (otherArgs?.Count ?? 0)]
        };
        for (int i = 0; i < args.Count; i++)
        {
            context.Variables[Skill.DefaultCompilerContext.GetSymbol("arg" + i)] = args[i];
        }
        if (otherArgs != null)
        {
            foreach (var entry in otherArgs)
                context.Variables[Skill.DefaultCompilerContext.GetSymbol(entry.Key)] = entry.Value;
        }

        return context;
    }
    public EvalContext CreateEvalContext(SkillExecutor exec, IDamageable target, List<SkillArgument> args, Dictionary<string, object>? otherArgs = null)
    {
        var context = new EvalContext
        {

            Caller = exec.Entity,
            Target = (target is BodyPart bp) ? bp.OwnerEntity : (target as Entity),
            TargetPart = (target is BodyPart bp2) ? bp2.Entity : null,
            Variables = new object[args.Count + (otherArgs?.Count ?? 0)]
        };
        for (int i = 0; i < args.Count; i++)
        {
            context.Variables[Skill.DefaultCompilerContext.GetSymbol("arg" + i)] = args[i];
        }
        if (otherArgs != null)
        {
            foreach (var entry in otherArgs)
                context.Variables[Skill.DefaultCompilerContext.GetSymbol(entry.Key)] = entry.Value;
        }

        return context;
    }
    public EvalContext CreateEvalContext(SkillExecutor exec, IDamageable target)
    {
        var context = new EvalContext
        {

            Caller = exec.Entity,
            Target = (target is BodyPart bp) ? bp.OwnerEntity : (target as Entity),
            TargetPart = (target is BodyPart bp2) ? bp2.Entity : null,
            Variables = Array.Empty<object>()
        };

         return context;
    }
}
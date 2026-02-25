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
    public readonly Expr<bool>? canCancel;
    public readonly Expr<float>? delay;
    public readonly Expr<float>? cooldown;
    public readonly Expr<float>? duration;
    public readonly Expr<bool>? canExecute;
    public readonly Expr<string>[]? layers;
    
    public readonly (Expr<float>, CompendiumEntryExpr<DamageType>)? damage;
    public readonly (Expr<bool>, Expr<string>)? doesHit;
    public readonly EffectExpr? onHit;
    public readonly EffectExpr? onAttack;
    public readonly Expr<bool>? condition;
    public readonly Expr<bool>? canTarget;
    public readonly Type[][] argumentTypes;
    
    public SkillExpressions(JsonElement obj)
    {
        T? TryCompile<T>(string property) where T : BaseExpr
        {
            if (obj.GetPropertyOrNull(property) is JsonElement prop)
            {
                if (typeof(T) == typeof(EffectExpr))
                {
                    var expr = ExpressionCompiler.CompileEffect(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(Expr<bool>))
                {
                    var expr = ExpressionCompiler.CompileCondition(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(Expr<float>))
                {
                    var expr = ExpressionCompiler.CompileNumber(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(Expr<string>))
                {
                    var expr = ExpressionCompiler.CompileString(prop);
                    return (T)(BaseExpr)expr;
                }
                else if (typeof(T) == typeof(CompendiumEntryExpr<DamageType>))
                {
                    var expr = ExpressionCompiler.CompileCompendiumEntry<DamageType>(prop);
                    return (T)(BaseExpr)expr;
                }
            }
            return null;
        }
        // Basic skill properties
        onExecute = TryCompile<EffectExpr>("execute");
        onStart = TryCompile<EffectExpr>("start");
        onCancel = TryCompile<EffectExpr>("cancel");
        canCancel = TryCompile<Expr<bool>>("canCancel");
        delay = TryCompile<Expr<float>>("delay");
        cooldown = TryCompile<Expr<float>>("cooldown");
        duration = TryCompile<Expr<float>>("duration");
        if (obj.GetPropertyOrNull("layers") is JsonElement layersElem && layersElem.ValueKind == JsonValueKind.Array)
        {
            var list = new List<Expr<string>>();
            foreach (var item in layersElem.EnumerateArray())
            {
                var expr = ExpressionCompiler.CompileString(item);
                list.Add(expr);
            }
            layers = list.ToArray();
        }
        canExecute = TryCompile<Expr<bool>>("canExecute");
        
        // Attack skill properties
        onHit = TryCompile<EffectExpr>("onHit");
        onAttack = TryCompile<EffectExpr>("onAttack");
        if (obj.GetPropertyOrNull("damage") is JsonElement damageElem && damageElem.ValueKind == JsonValueKind.Object)
        {
            var numberExpr = ExpressionCompiler.CompileNumber(damageElem.GetProperty("amount"));
            var damageTypeExpr = ExpressionCompiler.CompileCompendiumEntry<DamageType>(damageElem.GetProperty("type"));
            damage = (numberExpr, damageTypeExpr);
        }
        if (obj.GetPropertyOrNull("doesHit") is JsonElement doesHitElem && doesHitElem.ValueKind == JsonValueKind.Object)
        {
            var conditionExpr = ExpressionCompiler.CompileCondition(doesHitElem.GetProperty("condition"));
            var stringExpr = ExpressionCompiler.CompileString(doesHitElem.GetProperty("description"));
            doesHit = (conditionExpr, stringExpr);
        }
        condition = TryCompile<Expr<bool>>("condition");
        canTarget = TryCompile<Expr<bool>>("canTarget");
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

    public EvalContext CreateEvalContext(SkillExecutor exec, List<SkillArgument> args, params object[] otherArgs)
    {
        var context = new EvalContext
        {
            Caller = exec.Entity,
            Variables = new object[args.Count + otherArgs.Length]
        };
        for (int i = 0; i < otherArgs.Length; i++)
        {
            context.Variables[i] = otherArgs[i];
        }
        for (int i = 0; i < args.Count; i++)
        {
            context.Variables[otherArgs.Length + i] = args[i];
        }

        return context;
    }
    public EvalContext CreateEvalContext(SkillExecutor exec, IDamageable target, List<SkillArgument> args, params object[] otherArgs)
    {
        var context = CreateEvalContext(exec, args, otherArgs);
        context.Target = (target is BodyPart bp) ? bp.OwnerEntity : (target as Entity);
        context.TargetPart = (target is BodyPart bp2) ? bp2.Entity : null;
        return context;
    }
    public EvalContext CreateEvalContext(SkillExecutor exec, IDamageable target)
    {
        var context = CreateEvalContext(exec, target, new List<SkillArgument>());
        return context;
    }
}
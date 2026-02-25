using System.Reflection;
using Rpg.Entities.Components;
using Rpg.Health;

namespace Rpg.Skills;
public class ArbitraryAttackSkill : AttackSkill
{
    public readonly string Id;
    private readonly SkillExpressions code;

    private readonly string description;

    public ArbitraryAttackSkill(
        string id,
        SkillExpressions code,
        string description,
        string name,
        string icon)
    {
        Id = id;
        this.code = code;
        this.description = description;
        
        CustomName = name;
        CustomIcon = icon;
    }

    public override void Start(SkillExecutor executor, List<SkillArgument> arguments)
    {
        base.Start(executor, arguments);
        
        code.onStart?.Eval(code.CreateEvalContext(executor, arguments));
    }

    public override void Execute(SkillExecutor executor, List<SkillArgument> arguments, uint tick)
    {
        base.Execute(executor, arguments, tick);
        code.onExecute?.Eval(code.CreateEvalContext(executor, arguments, tick));
    }

    public override void Cancel(SkillExecutor executor, List<SkillArgument> arguments, bool interrupted = false)
    {
        base.Cancel(executor, arguments, interrupted);
        code.onCancel?.Eval(code.CreateEvalContext(executor, arguments, interrupted));
    }

    public override bool CanCancel(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.canCancel != null)
            return code.canCancel.Eval(code.CreateEvalContext(executor, arguments));
        return base.CanCancel(executor, arguments);
    }

    public override uint GetDelay(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.delay != null)
            return (uint)code.delay.Eval(code.CreateEvalContext(executor, arguments));
        return base.GetDelay(executor, arguments);
    }

    public override uint GetCooldown(SkillExecutor executor, List<SkillArgument> arguments)
    {
        if (code.cooldown != null)
            return (uint)code.cooldown.Eval(code.CreateEvalContext(executor, arguments));
        return base.GetCooldown(executor, arguments);
    }

    public override (float damage, DamageType type, string? formula) GetDamage(SkillExecutor executor, List<SkillArgument> arguments,  IDamageable target) 
    {
        if (code.damage.HasValue)
        {
            var dmg = code.damage.Value.Item1.Eval(code.CreateEvalContext(executor, target, arguments));
            var type = code.damage.Value.Item2.Eval(code.CreateEvalContext(executor, target, arguments));
            if (type == null)
            {
                type = Compendium.GetDefaultEntry<DamageType>();
                executor.Entity.Log("Damage type expression evaluated to null, using default damage type instead.", LogLevel.Warning);
            }
            return (dmg, type, null);
        }
        return (0, Compendium.GetDefaultEntry<DamageType>(), null);
    }

    public override void OnAttack(SkillExecutor executor, List<SkillArgument> arguments, IDamageable target, bool hit)
    {
        if (hit)
            code.onHit?.Eval(code.CreateEvalContext(executor, target, arguments));
        code.onAttack?.Eval(code.CreateEvalContext(executor, target, arguments));
    }

    public override (bool, string?) DoesHit(SkillExecutor executor, List<SkillArgument> arguments, IDamageable target)
    {
        if (code.doesHit.HasValue)
        {
            var doesHit = code.doesHit.Value.Item1.Eval(code.CreateEvalContext(executor, target, arguments));
            var reason = code.doesHit.Value.Item2.Eval(code.CreateEvalContext(executor, target, arguments));
            return (doesHit, reason);
        }
        return base.DoesHit(executor, arguments, target);
    }

    public override bool CanBeUsed(SkillExecutor executor)
    {
        if (code.condition != null)
            return code.condition.Eval(executor.Entity);
        return base.CanBeUsed(executor);
    }

    public override bool CanTarget(SkillExecutor executor, IDamageable target)
    {
        if (code.canTarget != null)
            return base.CanTarget(executor, target) && code.canTarget.Eval(code.CreateEvalContext(executor, target));
        return base.CanTarget(executor, target);
    }

    public override Type[][] GetArguments()
    {
        var baseArgs = base.GetArguments();
        var newArr = new Type[baseArgs.Length + (code.argumentTypes?.Length ?? 0)][];
        for (int i = 0; i < baseArgs.Length; i++)
        {
            newArr[i] = baseArgs[i];
        }
        if (code.argumentTypes != null)
        {
            for (int i = 0; i < code.argumentTypes.Length; i++)
            {
                newArr[baseArgs.Length + i] = code.argumentTypes[i];
            }
        }
        return newArr;
    }

    public override string GetDescription()
    {
        return description;
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(Id);
    }
}

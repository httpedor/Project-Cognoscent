using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Rpg;
public class ArbitraryAttackSkill : AttackSkill
{
    public class Context
    {
        public Creature? creature;
        public List<SkillArgument>? arguments;
        public ISkillSource? source;
        public IDamageable? target;
        public uint? tick;
        public bool? interrupted;
    }

    public readonly string Id;
    private readonly SkillJson code;

    private readonly string description;

    public ArbitraryAttackSkill(
        string id,
        SkillJson code,
        string description,
        string name,
        string icon)
    {
        this.Id = id;
        this.code = code;
        this.description = description;
        
        CustomName = name;
        CustomIcon = icon;
    }

    public override void Start(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        base.Start(executor, arguments, source);
        code.onStart?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source });
    }

    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        code.onExecute?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, tick = tick, source = source });
    }

    public override void Cancel(Creature executor, List<SkillArgument> arguments, ISkillSource source, bool interrupted = false)
    {
        base.Cancel(executor, arguments, source, interrupted);
        code.onCancel?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source, interrupted = interrupted });
    }

    public override bool CanCancel(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return code.canCancel?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source }) 
            ?? base.CanCancel(executor, arguments, source);
    }

    public override uint GetDelay(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return code.delay?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source }) 
            ?? base.GetDelay(executor, arguments, source);
    }

    public override uint GetCooldown(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return code.cooldown?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source }) 
            ?? base.GetCooldown(executor, arguments, source);
    }

    public override (float damage, DamageType type, string? formula) GetDamage(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target)
    {
        if (code.damage != null)
        {
            var ret = code.damage(new SkillJson.Context
                { creature = executor, arguments = arguments, source = source, target = target });
            return (ret.Item1, ret.Item2, null);
        }
        return (0, DamageType.Physical, null);
    }

    public override void OnAttack(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target, bool hit)
    {
        if (hit)
            code.onHit?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source, target = target });
        code.onAttack?.Invoke(new SkillJson.Context { creature = executor, arguments = arguments, source = source, target = target });
    }

    public override (bool, string?) DoesHit(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target)
    {
        if (code.doesHit != null)
            return code.doesHit(new SkillJson.Context { creature = executor, arguments = arguments, source = source, target = target });
        return base.DoesHit(executor, arguments, source, target);
    }

    public override bool CanBeUsed(Creature executor, ISkillSource source)
    {
        if (code.condition != null)
            return code.condition(new SkillJson.Context { creature = executor, source = source });
        return base.CanBeUsed(executor, source);
    }

    public override bool CanTarget(Creature executor, ISkillSource source, IDamageable target)
    {
        if (code.canTarget != null)
            return base.CanTarget(executor, source, target) && code.canTarget(new SkillJson.Context { creature = executor, source = source, target = target });
        return base.CanTarget(executor, source, target);
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

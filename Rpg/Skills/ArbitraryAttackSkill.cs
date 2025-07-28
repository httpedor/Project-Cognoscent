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

    private readonly string id;
    private readonly Func<Context, object>? onExecute;
    private readonly Func<Context, object>? onStart;
    private readonly Func<Context, object>? onCancel;
    private readonly Func<Context, bool>? canCancel;
    private readonly Func<Context, uint>? delay;
    private readonly Func<Context, uint>? cooldown;
    private readonly Func<Context, (float, DamageType)>? damage;
    private readonly Func<Context, (bool, string?)>? doesHit;
    private readonly Func<Context, object>? onHit;
    private readonly Func<Context, object>? onAttack;

    private readonly string description;

    public ArbitraryAttackSkill(
        string id,
        string? execute,
        string? start,
        string? cancel,
        string? canCancel,
        string? delay,
        string? cooldown,
        string? damage,
        string? doesHit,
        string? onHit,
        string? onAttack,
        string description,
        string name,
        string icon)
    {
        this.id = id;
        if (!SidedLogic.Instance.IsClient())
        {
            Func<Context, T> Compile<T>(string code)
            {
                var script = CSharpScript.Create<T>(code, ScriptOptions.Default.WithReferences(typeof(Creature).Assembly).WithImports("Rpg"), typeof(Context)).CreateDelegate();
                return ctx => script(ctx).Result;
            }
            if (execute != null) onExecute = Compile<object>(execute);
            if (start != null) onStart = Compile<object>(start);
            if (cancel != null) onCancel = Compile<object>(cancel);
            if (onHit != null) this.onHit = Compile<object>(onHit);
            if (onAttack != null) this.onAttack = Compile<object>(onAttack);
            if (canCancel != null) this.canCancel = Compile<bool>(canCancel);
            if (delay != null) this.delay = Compile<uint>(delay);
            if (cooldown != null) this.cooldown = Compile<uint>(cooldown);
            if (damage != null) this.damage = Compile<(float, DamageType)>(damage);
            if (doesHit != null) this.doesHit = Compile<(bool, string?)>(doesHit);
        }

        this.description = description;
        CustomName = name;
        CustomIcon = icon;
    }

    public override void Start(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        base.Start(executor, arguments, source);
        onStart?.Invoke(new Context { creature = executor, arguments = arguments, source = source });
    }

    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        onExecute?.Invoke(new Context { creature = executor, arguments = arguments, tick = tick, source = source });
    }

    public override void Cancel(Creature executor, List<SkillArgument> arguments, ISkillSource source, bool interrupted = false)
    {
        base.Cancel(executor, arguments, source, interrupted);
        onCancel?.Invoke(new Context { creature = executor, arguments = arguments, source = source, interrupted = interrupted });
    }

    public override bool CanCancel(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return canCancel?.Invoke(new Context { creature = executor, arguments = arguments, source = source }) 
            ?? base.CanCancel(executor, arguments, source);
    }

    public override uint GetDelay(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return delay?.Invoke(new Context { creature = executor, arguments = arguments, source = source }) 
            ?? base.GetDelay(executor, arguments, source);
    }

    public override uint GetCooldown(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return cooldown?.Invoke(new Context { creature = executor, arguments = arguments, source = source }) 
            ?? base.GetCooldown(executor, arguments, source);
    }

    public override (float damage, DamageType type) GetDamage(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target)
    {
        if (damage != null)
            return damage(new Context { creature = executor, arguments = arguments, source = source, target = target });
        return (0, DamageType.Physical);
    }

    public override void OnAttack(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target, bool hit)
    {
        if (hit)
            onHit?.Invoke(new Context { creature = executor, arguments = arguments, source = source, target = target });
        onAttack?.Invoke(new Context { creature = executor, arguments = arguments, source = source, target = target });
    }

    public override (bool, string?) DoesHit(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target)
    {
        if (doesHit != null)
            return doesHit(new Context { creature = executor, arguments = arguments, source = source, target = target });
        return base.DoesHit(executor, arguments, source, target);
    }

    public override string GetDescription()
    {
        return description;
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(id);
    }
}

using Rpg.Entities;

namespace Rpg.Features;

public class ArbitraryFeature : Feature
{
    protected readonly string id;
    protected readonly string description;
    protected readonly bool toggleable;

    public class Context
    {
        public IDamageable? injured;
        public Entity? entity;
        public DamageSource? damage;
        public Injury? injury;
        public double amount = 0;
        public bool hit = false;
        public Skill? skill;
        public List<SkillArgument>? arguments;
        public uint tick = 0;
        public SkillExecutorComponent? skillExecutor;
    }

    private readonly Action<Context>? onTick;
    private readonly Action<Context>? onEnable;
    private readonly Action<Context>? onDisable;
    private readonly Func<Context, (bool, string?)>? doesGetAttacked;
    private readonly Func<Context, (bool, string?)>? doesAttack;
    private readonly Func<Context, (bool, string?)>? doesExecuteSkill;
    private readonly Action<Context>? onAttacked;
    private readonly Action<Context>? onAttack;
    private readonly Action<Context>? onExecuteSkill;
    private readonly Action<Context>? onInjured;
    private readonly Func<Context, double>? modifyReceivingDamage;
    private readonly Func<Context, double>? modifyAttackingDamage;

    public ArbitraryFeature(
        string id,
        string name,
        string description,
        string? onTick = null,
        string? onEnable = null,
        string? onDisable = null,
        string? doesGetAttacked = null,
        string? doesAttack = null,
        string? doesExecuteSkill = null,
        string? onAttacked = null,
        string? onAttack = null,
        string? onExecuteSkill = null,
        string? onInjured = null,
        string? modifyReceivingDamage = null,
        string? modifyAttackingDamage = null,
        bool toggleable = false
    )
    {
        this.id = id;
        this.description = description;
        this.toggleable = toggleable;
        CustomName = name;

        if (!SidedLogic.Instance.IsClient())
        {
            if (onTick != null) this.onTick = Scripting.Compile<Context>(onTick);
            if (onEnable != null) this.onEnable = Scripting.Compile<Context>(onEnable);
            if (onDisable != null) this.onDisable = Scripting.Compile<Context>(onDisable);
            if (doesGetAttacked != null) this.doesGetAttacked = Scripting.Compile<Context, (bool, string?)>(doesGetAttacked);
            if (doesAttack != null) this.doesAttack = Scripting.Compile<Context, (bool, string?)>(doesAttack);
            if (doesExecuteSkill != null) this.doesExecuteSkill = Scripting.Compile<Context, (bool, string?)>(doesExecuteSkill);
            if (onAttacked != null) this.onAttacked = Scripting.Compile<Context>(onAttacked);
            if (onAttack != null) this.onAttack = Scripting.Compile<Context>(onAttack);
            if (onExecuteSkill != null) this.onExecuteSkill = Scripting.Compile<Context>(onExecuteSkill);
            if (onInjured != null) this.onInjured = Scripting.Compile<Context>(onInjured);
            if (modifyReceivingDamage != null) this.modifyReceivingDamage = Scripting.Compile<Context, double>(modifyReceivingDamage);
            if (modifyAttackingDamage != null) this.modifyAttackingDamage = Scripting.Compile<Context, double>(modifyAttackingDamage);
        }
    }

    public override string GetId() => id;
    public override string GetDescription() => description;
    public override bool IsToggleable(FeaturesComponent entity) => toggleable;

    public override void OnTick(FeaturesComponent source)
    {
        onTick?.Invoke(new Context { entity = source.Entity });
    }

    public override void OnEnable(FeaturesComponent source)
    {
        base.OnEnable(source);
        onEnable?.Invoke(new Context { entity = source.Entity });
    }

    public override void OnDisable(FeaturesComponent source)
    {
        base.OnDisable(source);
        onDisable?.Invoke(new Context { entity = source.Entity });
    }

    public override (bool, string?) DoesGetAttacked(FeaturesComponent source, IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesGetAttacked != null)
        {
            return doesGetAttacked(new Context { entity = source.Entity, damage = damage, hit = hit });
        }
        return base.DoesGetAttacked(source, attacked, damage, hit);
    }

    public override (bool, string?) DoesAttack(SkillExecutorComponent source, IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesAttack != null)
        {
            return doesAttack(new Context { skillExecutor = source, entity = source.Entity, injured = attacked, damage = damage, hit = hit });
        }
        return base.DoesAttack(source, attacked, damage, hit);
    }

    public override (bool, string?) DoesExecuteSkill(SkillExecutorComponent executor, Skill skill, List<SkillArgument> arguments)
    {
        if (doesExecuteSkill != null)
        {
            return doesExecuteSkill(new Context { entity = executor.Entity, skillExecutor = executor, skill = skill, arguments = arguments });
        }
        return base.DoesExecuteSkill(executor, skill, arguments);
    }

    public override void OnAttacked(FeaturesComponent attacked, IDamageable target, DamageSource source, double damage, bool hit)
    {
        onAttacked?.Invoke(new Context { entity = attacked.Entity, injured = target, damage = source, amount = damage, hit = hit });
    }

    public override void OnAttack(SkillExecutorComponent attacker, IDamageable target, DamageSource source, double damage, bool hit)
    {
        onAttack?.Invoke(new Context { entity = attacker.Entity, injured = target, damage = source, amount = damage, hit = hit });
    }

    public override void OnExecuteSkill(SkillExecutorComponent executor, Skill skill, List<SkillArgument> arguments, uint tick)
    {
        onExecuteSkill?.Invoke(new Context { entity = executor.Entity, skill = skill, arguments = arguments, tick = tick, skillExecutor = executor});
    }

    public override void OnInjured(FeaturesComponent source, IDamageable injured, Injury injury)
    {
        
        onInjured?.Invoke(new Context() { entity = source.Entity, injured = injured, injury = injury});
    }

    public override (double, string?) ModifyReceivingDamage(FeaturesComponent attacked, IDamageable target, DamageSource source, double damage)
    {
        if (modifyReceivingDamage != null)
        {
            return (modifyReceivingDamage(new Context { entity = attacked.Entity, injured = target, damage = source, amount = damage }), GetName());
        }
        return base.ModifyReceivingDamage(attacked, target, source, damage);
    }

    public override (double, string?) ModifyAttackingDamage(SkillExecutorComponent attacker, IDamageable target, DamageSource source, double damage)
    {
        if (modifyAttackingDamage != null)
        {
            return (modifyAttackingDamage(new Context { entity = attacker.Entity, injured = target, damage = source, amount = damage }), GetName());
        }
        return base.ModifyAttackingDamage(attacker, target, source, damage);
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(id);
    }
}
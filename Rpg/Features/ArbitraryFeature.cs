using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Rpg;
public class ArbitraryFeature : Feature
{
    protected readonly string id;
    protected readonly string description;
    protected readonly bool toggleable;

    public class Context
    {
        public IFeatureContainer source;
        public IDamageable? injured;
        public Creature? creature;
        public DamageSource? damage;
        public Injury? injury;
        public double amount = 0;
        public bool hit = false;
        public Skill? skill;
        public List<SkillArgument>? arguments;
        public uint tick = 0;
        public ISkillSource? skillSource;
    }

    private readonly Func<Context, object>? onTick;
    private readonly Func<Context, object>? onEnable;
    private readonly Func<Context, object>? onDisable;
    private readonly Func<Context, (bool, string?)>? doesGetAttacked;
    private readonly Func<Context, (bool, string?)>? doesAttack;
    private readonly Func<Context, (bool, string?)>? doesExecuteSkill;
    private readonly Func<Context, object>? onAttacked;
    private readonly Func<Context, object>? onAttack;
    private readonly Func<Context, object>? onExecuteSkill;
    private readonly Func<Context, object>? onInjured;
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
            Func<Context, T> Compile<T>(string code)
            {
                var script = CSharpScript.Create<T>(
                    code,
                    ScriptOptions.Default.WithReferences(typeof(Creature).Assembly).WithImports("Rpg"),
                    typeof(Context)
                ).CreateDelegate();

                return ctx => script(ctx).Result;
            }

            if (onTick != null) this.onTick = Compile<object>(onTick);
            if (onEnable != null) this.onEnable = Compile<object>(onEnable);
            if (onDisable != null) this.onDisable = Compile<object>(onDisable);
            if (doesGetAttacked != null) this.doesGetAttacked = Compile<(bool, string?)>(doesGetAttacked);
            if (doesAttack != null) this.doesAttack = Compile<(bool, string?)>(doesAttack);
            if (doesExecuteSkill != null) this.doesExecuteSkill = Compile<(bool, string?)>(doesExecuteSkill);
            if (onAttacked != null) this.onAttacked = Compile<object>(onAttacked);
            if (onAttack != null) this.onAttack = Compile<object>(onAttack);
            if (onExecuteSkill != null) this.onExecuteSkill = Compile<object>(onExecuteSkill);
            if (onInjured != null) this.onInjured = Compile<object>(onInjured);
            if (modifyReceivingDamage != null) this.modifyReceivingDamage = Compile<double>(modifyReceivingDamage);
            if (modifyAttackingDamage != null) this.modifyAttackingDamage = Compile<double>(modifyAttackingDamage);
        }
    }

    public override string GetId() => id;
    public override string GetDescription() => description;
    public override bool IsToggleable(Entity entity) => toggleable;

    public override void OnTick(IFeatureContainer source)
    {
        onTick?.Invoke(new Context { source = source });
    }

    public override void OnEnable(IFeatureContainer source)
    {
        base.OnEnable(source);
        onEnable?.Invoke(new Context { source = source });
    }

    public override void OnDisable(IFeatureContainer source)
    {
        base.OnDisable(source);
        onDisable?.Invoke(new Context { source = source });
    }

    public override (bool, string?) DoesGetAttacked(IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesGetAttacked != null)
        {
            return doesGetAttacked(new Context { source = attacked as IFeatureContainer, damage = damage, hit = hit });
        }
        return base.DoesGetAttacked(attacked, damage, hit);
    }

    public override (bool, string?) DoesAttack(IFeatureContainer source, IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesAttack != null)
        {
            return doesAttack(new Context { source = source, injured = attacked, damage = damage, hit = hit });
        }
        return base.DoesAttack(source, attacked, damage, hit);
    }

    public override (bool, string?) DoesExecuteSkill(Creature executor, Skill skill, List<SkillArgument> arguments)
    {
        if (doesExecuteSkill != null)
        {
            return doesExecuteSkill(new Context { creature = executor, skill = skill, arguments = arguments });
        }
        return base.DoesExecuteSkill(executor, skill, arguments);
    }

    public override void OnAttacked(IDamageable attacked, DamageSource damage, double amount, bool hit)
    {
        onAttacked?.Invoke(new Context { source = attacked as IFeatureContainer, injured = ((BodyPartSkillArgument?)damage.Arguments?.Find(a => a is BodyPartSkillArgument))?.Part, damage = damage, amount = amount, hit = hit });
    }

    public override void OnAttack(Creature attacker, IDamageable target, DamageSource damage, double amount, bool hit)
    {
        onAttack?.Invoke(new Context { creature = attacker, injured = target, damage = damage, amount = amount, hit = hit });
    }

    public override void OnExecuteSkill(Creature executor, Skill skill, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        onExecuteSkill?.Invoke(new Context { creature = executor, skill = skill, arguments = arguments, tick = tick, skillSource = source });
    }

    public override void OnInjured(IDamageable injured, Injury injury)
    {
        Creature? creature;
        if (injured is Creature c)
            creature = c;
        else
            creature = (injured as BodyPart)?.Owner;
        
        onInjured?.Invoke(new Context() {creature = creature, injured = injured, injury = injury});
    }

    public override (double, string?) ModifyReceivingDamage(IDamageable attacked, DamageSource source, double damage)
    {
        if (modifyReceivingDamage != null)
        {
            return (modifyReceivingDamage(new Context { source = (attacked as IFeatureContainer)!, damage = source, amount = damage }), GetName());
        }
        return base.ModifyReceivingDamage(attacked, source, damage);
    }

    public override (double, string?) ModifyAttackingDamage(Creature attacker, IDamageable target, DamageSource source, double damage)
    {
        if (modifyAttackingDamage != null)
        {
            return (modifyAttackingDamage(new Context { creature = attacker, injured = target, damage = source, amount = damage }), GetName());
        }
        return base.ModifyAttackingDamage(attacker, target, source, damage);
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(id);
    }
}

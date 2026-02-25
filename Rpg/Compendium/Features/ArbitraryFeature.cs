using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg.Features;

public class ArbitraryFeature : Feature
{
    protected readonly string id;
    protected readonly string description;
    protected readonly Expr<bool> toggleable;


    private readonly EffectExpr? onTick;
    private readonly EffectExpr? onEnable;
    private readonly EffectExpr? onDisable;
    private readonly (Expr<bool>, Expr<string>)? doesGetAttacked;
    private readonly (Expr<bool>, Expr<string>)? doesAttack;
    private readonly (Expr<bool>, Expr<string>)? doesExecuteSkill;
    private readonly EffectExpr? onAttacked;
    private readonly EffectExpr? onAttack;
    private readonly EffectExpr? onExecuteSkill;
    private readonly EffectExpr? onInjured;
    private readonly (Expr<float>, Expr<string>)? modifyReceivingDamage;
    private readonly (Expr<float>, Expr<string>)? modifyAttackingDamage;
    private readonly CompendiumEntryExpr<Skill>[] skills;

    public ArbitraryFeature(
        string id,
        string name,
        string description,
        EffectExpr? onTick = null,
        EffectExpr? onEnable = null,
        EffectExpr? onDisable = null,
        (Expr<bool>, Expr<string>)? doesGetAttacked = null,
        (Expr<bool>, Expr<string>)? doesAttack = null,
        (Expr<bool>, Expr<string>)? doesExecuteSkill = null,
        EffectExpr? onAttacked = null,
        EffectExpr? onAttack = null,
        EffectExpr? onExecuteSkill = null,
        EffectExpr? onInjured = null,
        (Expr<float>, Expr<string>)? modifyReceivingDamage = null,
        (Expr<float>, Expr<string>)? modifyAttackingDamage = null,
        CompendiumEntryExpr<Skill>[]? skills = null,
        Expr<bool>? toggleable = null
    )
    {
        this.id = id;
        this.description = description;
        if (toggleable != null)
            this.toggleable = toggleable;
        else
            this.toggleable = new ConstConditionExpr(false);
        CustomName = name;

        this.onTick = onTick;
        this.onEnable = onEnable;
        this.onDisable = onDisable;
        this.doesGetAttacked = doesGetAttacked;
        this.doesAttack = doesAttack;
        this.doesExecuteSkill = doesExecuteSkill;
        this.onAttacked = onAttacked;
        this.onAttack = onAttack;
        this.onExecuteSkill = onExecuteSkill;
        this.onInjured = onInjured;
        this.modifyReceivingDamage = modifyReceivingDamage;
        this.modifyAttackingDamage = modifyAttackingDamage;
        this.skills = skills ?? Array.Empty<CompendiumEntryExpr<Skill>>();
    }

    public override string GetId() => id;
    public override string GetDescription() => description;
    public override bool IsToggleable(FeaturesContainer container) => toggleable.Eval(container.Entity);

    public override IEnumerable<Skill> GetSkills(FeaturesContainer source, SkillExecutor executor)
    {
        foreach (var skillRef in skills)
        {
            var skill = skillRef.Eval(source.Entity);
            if (skill != null)
                yield return skill;
        }
    }

    public override void OnTick(FeaturesContainer source)
    {
        onTick?.Eval(source.Entity);
    }

    public override void OnEnable(FeaturesContainer source)
    {
        base.OnEnable(source);
        onEnable?.Eval(source.Entity);
    }

    public override void OnDisable(FeaturesContainer source)
    {
        base.OnDisable(source);
        onDisable?.Eval(source.Entity);
    }

    public override (bool, string?) DoesGetAttacked(FeaturesContainer source, IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesGetAttacked != null && attacked is Component component)
        {
            var ctx = new EvalContext(
                hit,
                damage.Type,
                null!
            ) {
                Target = damage.Attacker?.Entity,
                Caller = source.Entity,
                TargetPart = component.Entity,
                Board = source.Entity.Board,
            };
            var ret = doesGetAttacked.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, doesGetAttacked.Value.Item2.Eval(ctx));
        }
        return base.DoesGetAttacked(source, attacked, damage, hit);
    }

    public override (bool, string?) DoesAttack(SkillExecutor source, IDamageable attacked, DamageSource damage, bool hit)
    {
        if (doesAttack != null && attacked is Component component)
        {
            var ctx = new EvalContext(
                hit,
                damage.Type,
                null!
            ) {
                Target = damage.Attacker?.Entity,
                Caller = source.Entity,
                TargetPart = component.Entity,
                Board = source.Entity.Board,
            };
            var ret = doesAttack.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, doesAttack.Value.Item2.Eval(ctx));
        }
        return base.DoesAttack(source, attacked, damage, hit);
    }

    public override (bool, string?) DoesExecuteSkill(SkillExecutor executor, Skill skill, List<SkillArgument> arguments)
    {
        if (doesExecuteSkill != null)
        {
            var arr = new object[arguments.Count + 1];
            for (int i = 0; i < arguments.Count; i++)
            {
                arr[i] = arguments[i];
            }
            var ctx = new EvalContext(arr)
            {
                Caller = executor.Entity,
                Target = executor.Entity,
                Board = executor.Entity.Board,
            };
            var ret = doesExecuteSkill.Value.Item1.Eval(ctx);
            ctx.Variables[arguments.Count] = ret;
            return (ret, doesExecuteSkill.Value.Item2.Eval(ctx));
        }
        return base.DoesExecuteSkill(executor, skill, arguments);
    }

    public override void OnAttacked(FeaturesContainer attacked, IDamageable target, DamageInstance damage, bool hit)
    {
        if (onAttacked == null)
            return;
        var ctx = new EvalContext(
            hit,
            damage.Source.Type,
            damage.Amount
        ) {
            Target = damage.Source.Attacker?.Entity,
            Caller = attacked.Entity,
            TargetPart = target is Component component ? component.Entity : null,
            Board = attacked.Entity.Board,
        };
        onAttacked.Eval(ctx);
    }

    public override void OnAttack(SkillExecutor attacker, IDamageable target, DamageInstance damage, bool hit)
    {
        if (onAttack == null)
            return;
        var ctx = new EvalContext(
            hit,
            damage.Source.Type,
            damage.Amount
        ) {
            Target = damage.Source.Attacker?.Entity,
            Caller = attacker.Entity,
            TargetPart = target is Component component ? component.Entity : null,
            Board = attacker.Entity.Board,
        };
        onAttack.Eval(ctx);
    }

    public override void OnExecuteSkill(SkillExecutor executor, Skill skill, List<SkillArgument> arguments, uint tick)
    {
        onExecuteSkill?.Eval(executor.Entity);
    }

    public override void OnInjured(FeaturesContainer source, IDamageable injured, Injury injury)
    {
        if (onInjured == null)
            return;
        var ctx = new EvalContext(
            injury.Type.Id,
            injury.Severity
        ) {
            TargetPart = injured is BodyPart bp ? bp.Entity : null,
            Target = injured is BodyPart comp ? comp.OwnerEntity : (injured is Component c ? c.Entity : null),
            Caller = source.Entity,
            Board = source.Entity.Board,
        };
        onInjured.Eval(ctx);
    }

    public override (double, string?) ModifyReceivingDamage(FeaturesContainer attacked, IDamageable target, DamageInstance damage)
    {
        if (modifyReceivingDamage != null)
        {
            var ctx = new EvalContext(
                damage.Source.Type,
                damage.Amount,
                null!
            ) {
                Target = damage.Source.Attacker?.Entity,
                Caller = attacked.Entity,
                TargetPart = target is Component component ? component.Entity : null,
                Board = attacked.Entity.Board,
            };
            var ret = modifyReceivingDamage.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, modifyReceivingDamage.Value.Item2.Eval(ctx));
        }
        return base.ModifyReceivingDamage(attacked, target, damage);
    }

    public override (double, string?) ModifyAttackingDamage(SkillExecutor attacker, IDamageable target, DamageInstance damage)
    {
        if (modifyAttackingDamage != null)
        {
            var ctx = new EvalContext(
                damage.Source.Type,
                damage.Amount,
                null!
            ) {
                Target = damage.Source.Attacker?.Entity,
                Caller = attacker.Entity,
                TargetPart = target is Component component ? component.Entity : null,
                Board = attacker.Entity.Board,
            };
            var ret = modifyAttackingDamage.Value.Item1.Eval(ctx);
            ctx.Variables[2] = ret;
            return (ret, modifyAttackingDamage.Value.Item2.Eval(ctx));
        }
        return base.ModifyAttackingDamage(attacker, target, damage);
    }

    public override void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
        stream.WriteString(id);
    }
}
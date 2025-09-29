using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Rpg;

namespace Rpg;

public abstract class AttackSkill : Skill
{
    protected AttackSkill()
    {
    }

    public abstract (float damage, DamageType type, string? formula) GetDamage(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target);

    /// <summary>
    /// Checks if this attack hits a damageable target
    /// </summary>
    /// <returns>A tuple with a boolean(if it hit) and a nullable string giving the reason for the miss(if it didn't hit)</returns>
    public virtual (bool, string?) DoesHit(Creature executor, List<SkillArgument> arguments, ISkillSource source,
        IDamageable target)
    {
        return (true, null);
    }

    public virtual void OnAttack(Creature executor, List<SkillArgument> arguments, ISkillSource source, IDamageable target, bool hit)
    {
        
    }

    public virtual bool IsProjectile(Creature executor, List<SkillArgument> arguments, ISkillSource source,
        IDamageable target)
    {
        return false;
    }
    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        IDamageable? target = arguments[0] switch
        {
            EntitySkillArgument { Entity: IDamageable damageable } => damageable,
            BodyPartSkillArgument { Part: not null } bpArg => bpArg.Part,
            _ => null
        };

        if (target == null)
            return;

        var hitInfo = DoesHit(executor, arguments, source, target);
        bool hit = hitInfo.Item1;
        if (!hit)
        {
            string missedHint = hitInfo.Item2 == null ? "errou" : $"[hint={hitInfo.Item2}]errou[/hint]";
            executor.Log($"{executor.BBLink} {missedHint} {BBHint} no(a) {target.BBLink}");
        }

        var dmgInfo = GetDamage(executor, arguments, source, target);
        var damageSource = new DamageSource(dmgInfo.type, executor, this, arguments.ToArray());
        if (target is IFeatureSource fs)
        {
            foreach (Feature feature in fs.EnabledFeatures)
            {
                (bool, string?) info = feature.DoesGetAttacked(target, damageSource, hit);
                if (info.Item2 != null)
                    target.Board?.Log(info.Item2);
                hit = info.Item1;
            }
        }
        foreach (Feature feature in executor.EnabledFeatures)
        {
            (bool, string?) info = feature.DoesAttack(executor, target, damageSource, hit);
            if (info.Item2 != null)
                target.Board?.Log(info.Item2);
            hit = info.Item1;
        }

        OnAttack(executor, arguments, source, target, hitInfo.Item1);
        double dmg = dmgInfo.damage;
        string formula = dmg.ToString("0.##");
        if (dmgInfo.formula != null)
            formula += " (" + dmgInfo.formula + ")";
        if (target is IFeatureSource fs2)
        {
            foreach (Feature feature in fs2.EnabledFeatures)
            {
                var info = feature.ModifyAttackingDamage(executor, target, damageSource, dmg);
                if (info.Item1 == dmg)
                    continue;
                dmg = info.Item1;
                formula += "\n" + dmg;
                if (info.Item2 != null)
                     formula += "("+info.Item2+")";
            }
        }

        if (hit)
        {
            target.Damage(damageSource, dmg);
            string acertouHint = hitInfo.Item2 == null ? "acertou" : $"[hint={hitInfo.Item2}]acertou[/hint]";
            executor.Log($"{executor.BBLink} {acertouHint} {BBHint} no(a) {target.BBLink} com [hint={formula}]{dmg}[/hint] de dano {dmgInfo.type.BBHint}");
        }
        
        if (target is IFeatureSource f2)
        {
            foreach (Feature feature in f2.EnabledFeatures)
            {
                feature.OnAttacked(target, damageSource, dmg, hit);
            }
        }

        foreach (var feature in executor.EnabledFeatures)
        {
            feature.OnAttack(executor, target, damageSource, dmg, hit);
        }
    }
    
    public override string GetName()
    {
        if (CustomName == null)
            return "Atacar";
        return CustomName;
    }
    public override string GetDescription()
    {
        return "Atacar parte do corpo de um inimigo, ou objeto.";
    }
    public override bool IsCombatSkill(Creature executor, List<SkillArgument> arguments, ISkillSource source)
    {
        return true;
    }

    public virtual bool CanTarget(Creature executor, ISkillSource source, IDamageable target)
    {
        return true;
    }
    public override bool CanUseArgument(Creature executor, ISkillSource source, int index, SkillArgument arg)
    {
        if (index != 0)
            return false;

        return arg switch
        {
            EntitySkillArgument esa => esa.Entity is IDamageable damageable && CanTarget(executor, source, damageable),
            BodyPartSkillArgument bpsa => bpsa.Part is { IsAlive: true, IsInternal: false } part && CanTarget(executor, source, part),
            _ => false
        };
    }
    public override Type[][] GetArguments()
    {
        return [[typeof(BodyPartSkillArgument), typeof(EntitySkillArgument)]];
    }

    public override string[] GetLayers(Creature executor, ISkillSource source)
    {
        return ["action"];
    }

    protected AttackSkill(Stream stream) : base(stream)
    {
    }
}

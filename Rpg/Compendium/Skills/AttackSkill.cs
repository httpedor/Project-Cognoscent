using System.Numerics;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Features;
using Rpg.Health;

namespace Rpg.Skills;

public abstract class AttackSkill : Skill
{
    protected AttackSkill()
    {
    }

    public abstract (float damage, DamageType type, string? formula) GetDamage(SkillExecutor executor, List<SkillArgument> arguments,  IDamageable target);
    public virtual float GetRange(SkillExecutor executor)
    {
        return 1f;
    }

    /// <summary>
    /// Checks if this attack hits a damageable target
    /// </summary>
    /// <returns>A tuple with a boolean(if it hit) and a nullable string giving the reason for the miss(if it didn't hit)</returns>
    public virtual (bool, string?) DoesHit(SkillExecutor executor, List<SkillArgument> arguments, IDamageable target)
    {
        return (true, null);
    }

    public virtual void OnAttack(SkillExecutor executor, List<SkillArgument> arguments, IDamageable target, bool hit)
    {
        
    }

    public virtual bool IsProjectile(SkillExecutor executor, List<SkillArgument> arguments, IDamageable target)
    {
        return false;
    }
    public override void Execute(SkillExecutor executor, List<SkillArgument> arguments, uint tick)
    {
        base.Execute(executor, arguments, tick);
        IDamageable? target = null; 
        Entity? targetOwner = null;
        if (arguments[0] is BodyPartSkillArgument bpArg && bpArg.Part != null)
        {
            target = bpArg.Part;
            targetOwner = bpArg.Part.OwnerEntity;
        }
        else if (arguments[0] is EntitySkillArgument entArg)
        {
            var ent = entArg.Entity;
            if (ent == null)
                return;

            foreach (var comp in ent.Components)
            {
                if (comp is IDamageable damageable)
                {
                    target = damageable;
                    break;
                }
            }
        }

        if (target == null)
        {
            executor.Entity.Log("Called execute on a attackskill without a valid target argument.", LogLevel.Warning);
            return;
        }
        Component targetComponent = (Component)target;

        var hitInfo = DoesHit(executor, arguments, target);
        bool hit = hitInfo.Item1;
        if (!hit)
        {
            string missedHint = hitInfo.Item2 == null ? "errou" : $"[hint={hitInfo.Item2}]errou[/hint]";
            executor.Board?.Log($"{executor.Entity.BBLink} {missedHint} {BBHint} no(a) {targetComponent.Entity.BBLink}");
        }

        var dmgInfo = GetDamage(executor, arguments, target);
        var damageSource = new DamageSource(dmgInfo.type, executor, this, arguments.ToArray());

        //TODO: Rewrite this whole thing with bodyparts and stuff in mind
        List<(FeaturesContainer source, Feature feat)> targetEnabledFeatures = new List<(FeaturesContainer, Feature)>();
        if (targetComponent.Entity.TryGetComponent<FeaturesContainer>(out var targetFeatures))
            foreach (Feature feature in targetFeatures.EnabledFeatures)
                targetEnabledFeatures.Add((targetFeatures, feature));
        if (targetOwner != null && targetOwner.TryGetComponent<FeaturesContainer>(out var targetOwnerFeatures))
            foreach (Feature feature in targetOwnerFeatures.EnabledFeatures)
                targetEnabledFeatures.Add((targetOwnerFeatures, feature));
        List<(FeaturesContainer source, Feature feat)> execFeatures = new List<(FeaturesContainer, Feature)>();
        if (executor.Entity.TryGetComponent<FeaturesContainer>(out var execFeaturesContainer))
        {
            foreach (Feature feature in execFeaturesContainer.EnabledFeatures)
                execFeatures.Add((execFeaturesContainer, feature));
        }
        
        foreach (var pair in targetEnabledFeatures)
        {
            (bool, string?) info = pair.feat.DoesGetAttacked(pair.source, target, damageSource, hit);
            if (info.Item2 != null)
                targetComponent.Board?.Log(info.Item2);
            hit = info.Item1;
        }
        
        foreach (var pair in execFeatures)
        {
            (bool, string?) info = pair.feat.DoesAttack(executor, target, damageSource, hit);
            if (info.Item2 != null)
                executor.Board?.Log(info.Item2);
            hit = info.Item1;
        }

        var dmgInstance = new DamageInstance(damageSource, dmgInfo.damage);
        string formula = dmgInstance.Amount.ToString("0.##");
        if (dmgInfo.formula != null)
            formula += " (" + dmgInfo.formula + ")";
        
        var mods = new List<StatModifier>();
        // Apply damage modifications from features
        foreach (var pair in execFeatures)
        {
            mods.AddRange(pair.feat.ModifyAttackingDamageModifiers(executor, dmgInstance));
        }
        dmgInstance.Amount = Stat.ApplyModifiers(mods, dmgInstance.Amount);

        foreach (var pair in execFeatures)
        {
            var info = pair.feat.ModifyAttackingDamage(executor, target, dmgInstance);
            if (info.Item1 == dmgInstance.Amount)
                continue;
            dmgInstance.Amount = (float)info.Item1;
            formula += "\n" + dmgInstance.Amount;
            if (info.Item2 != null)
                    formula += "("+info.Item2+")";
        }

        if (hit)
        {
            string acertouHint = hitInfo.Item2 == null ? "acertou" : $"[hint={hitInfo.Item2}]acertou[/hint]";
            executor.Board?.Log($"{executor.Entity.BBLink} {acertouHint} {BBHint} no(a) {targetComponent.Entity.BBLink} com [hint={formula}]{dmgInstance.Amount}[/hint] de dano {dmgInfo.type.BBHint}");
            target.Damage(dmgInstance);
        }
        
        foreach (var pair in targetEnabledFeatures)
        {
            pair.feat.OnAttacked(pair.source, target, dmgInstance, hit);
        }

        foreach (var pair in execFeatures)
        {
            pair.feat.OnAttack(executor, target, dmgInstance, hit);
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
    public override bool IsCombatSkill(SkillExecutor executor, List<SkillArgument> arguments)
    {
        return true;
    }

    public virtual bool CanTarget(SkillExecutor executor, IDamageable target)
    {
        Vector3 targetPos;
        Vector3 execPos;
        if (executor.Entity.TryGetComponent<Token>(out var execToken))
            execPos = execToken.Position;
        else
            return false;

        if (target is Component comp && comp.Entity.TryGetComponent<Token>(out var token))
            targetPos = token.Position;
        else
            return false;
        float range = GetRange(executor);
        if (Vector3.Distance(execPos, targetPos) > range)
            return false;
        return true;
    }
    public override bool CanUseArgument(SkillExecutor executor, int index, SkillArgument arg)
    {
        if (index != 0)
            return false;
        return arg switch
        {
            BodyPartSkillArgument bpsa => bpsa.Part is { IsAlive: true, IsInternal: false } part && CanTarget(executor, part),
            _ => false
        };
    }
    public override Type[][] GetArguments()
    {
        return [[typeof(BodyPartSkillArgument), typeof(EntitySkillArgument)]];
    }

    public override string[] GetLayers(SkillExecutor executor)
    {
        return ["action"];
    }

    protected AttackSkill(Stream stream) : base(stream)
    {
    }
}

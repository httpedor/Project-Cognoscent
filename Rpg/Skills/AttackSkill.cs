using System.Text;
using Rpg.Entities;

namespace Rpg;

public class AttackSkill : Skill
{
    public readonly InjuryType InjuryType;
    public readonly float DamageMultiplier;
    public readonly float Delay;
    public readonly float Cooldown;
    public readonly float StaminaUse;

    public AttackSkill(InjuryType type, float dmgMult = 1, float delay = 100, float cooldown = 0, float staminaUse = 10)
    {
        InjuryType = type;
        DamageMultiplier = dmgMult;
        Delay = delay;
        Cooldown = cooldown;
        StaminaUse = staminaUse;
    }
    public AttackSkill(Stream stream) : base(stream)
    {
        InjuryType = new InjuryType(stream);
        DamageMultiplier = stream.ReadFloat();
        Delay = stream.ReadFloat();
        Cooldown = stream.ReadFloat();
        StaminaUse = stream.ReadFloat();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        InjuryType.ToBytes(stream);
        stream.WriteFloat(DamageMultiplier);
        stream.WriteFloat(Delay);
        stream.WriteFloat(Cooldown);
        stream.WriteFloat(StaminaUse);
    }
    public override String GetName()
    {
        if (CustomName == null)
            return "Atacar(" + InjuryType.Translation + ")";
        return CustomName;
    }
    public override String GetDescription()
    {
        return "Atacar parte do corpo de um inimigo, ou objeto.";
    }
    public override Boolean CanUseArgument(ISkillSource source, Int32 index, SkillArgument arg)
    {
        if (index != 0)
            return false;
        
        if (arg is EntitySkillArgument esa)
        {
            return esa.Entity is ItemEntity;
        }
        else if (arg is BodyPartSkillArgument bpsa)
            return bpsa.Part != null && bpsa.Part.IsAlive && !bpsa.Part.IsInternal; //TODO: Check if stance allows it

        return false;
    }
    public override Type[][] GetArguments()
    {
        return new Type[][]{new[] {typeof(BodyPartSkillArgument), typeof(EntitySkillArgument)}};
    }
}

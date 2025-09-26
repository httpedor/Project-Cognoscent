using Rpg.Inventory;

namespace Rpg;

public class ParrySkill : Skill
{
    public override string GetDescription()
    {
        return "Cancela todo o dano do próximo ataque corpo-a-corpo ou de projetil bloqueável que você receber durante um tempo baseado na sua agilidade. Requer que você veja o atacante. Caso utilizado sem uma arma equipada, você redicionará o dano a uma de suas mãos.";
    }

    public override void Execute(Creature executor, List<SkillArgument> arguments, uint tick, ISkillSource source)
    {
        base.Execute(executor, arguments, tick, source);
        uint ticks = 10;
        switch (arguments[0])
        {
            case BodyPartSkillArgument bpsa:
                if (bpsa.Part != null)
                    executor.AddFeature(new ParryingFeature(ticks, bpsa.Part));
                break;
            case ItemSkillArgument isa:
                if (isa.Item != null)
                    executor.AddFeature(new ParryingFeature(ticks, isa.Item));
                break;
        }
    }

    public override bool CanUseArgument(Creature executor, ISkillSource source, int index, SkillArgument arg)
    {
        switch (arg)
        {
            case BodyPartSkillArgument bpsa:
            {
                var part = bpsa.Part;
                return part != null && part.Owner == executor && part is { IsAlive: true, IsInternal: false };
            }
            case ItemSkillArgument isa:
            {
                var item = isa.Item;
                return item != null && executor.Body.IsEquipped(item);
            }
            default:
                return base.CanUseArgument(executor, source, index, arg);
        }
    }

    public override Type[][] GetArguments()
    {
        return [[typeof(BodyPartSkillArgument), typeof(ItemSkillArgument)]];
    }
}
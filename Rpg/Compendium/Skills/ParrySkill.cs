using Rpg.Entities.Components;
using Rpg.Features;

namespace Rpg.Skills;

public class ParrySkill : Skill
{
    public override string GetDescription()
    {
        return "Cancela todo o dano do próximo ataque corpo-a-corpo ou de projetil bloqueável que você receber durante um tempo baseado na sua agilidade. Requer que você veja o atacante. Caso utilizado sem uma arma equipada, você redicionará o dano a uma de suas mãos.";
    }

    public override void Execute(SkillExecutor executor, List<SkillArgument> arguments, uint tick)
    {
        base.Execute(executor, arguments, tick);
        uint ticks = 10;
        switch (arguments[0])
        {
            case BodyPartSkillArgument bpsa:
                if (bpsa.Part != null)
                    executor.Entity.Features!.AddFeature(new ParryingFeature(ticks, bpsa.Part));
                break;
            case ItemSkillArgument isa:
                if (isa.Item != null)
                    executor.Entity.Features!.AddFeature(new ParryingFeature(ticks, isa.Item));
                break;
        }
    }

    public override bool CanUseArgument(SkillExecutor executor, int index, SkillArgument arg)
    {
        switch (arg)
        {
            case BodyPartSkillArgument bpsa:
            {
                var part = bpsa.Part;
                return part != null && part.OwnerEntity == executor.Entity && part is { IsAlive: true, IsInternal: false };
            }
            case ItemSkillArgument isa:
            {
                var item = isa.Item;
                return item != null && (executor.Entity.Body?.IsEquipped(item) ?? false);
            }
            default:
                return base.CanUseArgument(executor, index, arg);
        }
    }

    public override bool CanBeUsed(SkillExecutor executor)
    {
        return base.CanBeUsed(executor) && executor.Entity.HasComponent(FeaturesContainer.ID);
    }

    public override Type[][] GetArguments()
    {
        return [[typeof(BodyPartSkillArgument), typeof(ItemSkillArgument)]];
    }
}
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Components.Inventory;
using Rpg.Health;
using Rpg.Skills;

namespace Rpg.Features;

public class ParryingFeature : ConditionFeature
{
    private Either<Item, BodyPart> used;
    public ParryingFeature(uint ticks, Item used) : base(ticks)
    {
        this.used = new Either<Item, BodyPart>(used);
    }
    public ParryingFeature(uint ticks, BodyPart used) : base(ticks)
    {
        this.used = new Either<Item, BodyPart>(used);
    }
    
    public ParryingFeature(Stream stream) : base(stream)
    {
        if (stream.ReadByte() == 1)
            used = new Either<Item, BodyPart>(new ComponentRef<Item>(stream).Component!);
        else
            used = new Either<Item, BodyPart>(new ComponentRef<BodyPart>(stream).Component!);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        if (used.IsLeft)
        {
            stream.WriteByte(1);
            new ComponentRef<Item>(used.Left!).ToBytes(stream);
        }
        else
        {
            stream.WriteByte(0);
            new ComponentRef<BodyPart>(used.Right!).ToBytes(stream);
        }
    }

    public override string GetId()
    {
        return "parrying";
    }

    public override string GetDescription()
    {
        return "Essa entidade irá bloquear o próximo ataque corpo-a-corpo ou de projetil não-mágico que receber, desde que consiga ver o atacante.";
    }

    public override (double, string?) ModifyReceivingDamage(FeaturesContainer attacked, IDamageable target, DamageInstance damage)
    {
        var source = damage.Source;
        var skill = source.SkillUsed;
        if (skill == null)
            return base.ModifyReceivingDamage(attacked, target, damage);

        if (!skill.Is(SkillTags.Unparryable) &&
            source.ContactEntity != null && 
            (attacked.Entity.TryGetComponent(out Token token) && source.ContactEntity.TryGetComponent(out Token sourceToken) &&
            token.CanSee(sourceToken.Position.XY())))
        {
            attacked.Board!.RunTaskLater(() => attacked.RemoveFeature(this), 0);
            
            return (0, " [hint=defendeu com " + (used.IsLeft ? used.Left!.Name : used.Right!.Name) + "]defendeu[/hint] o ataque de " + source.ContactEntity.BBLink);
        }
        return base.ModifyReceivingDamage(attacked, target, damage);
    }
}
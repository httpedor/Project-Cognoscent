using Rpg.Inventory;

namespace Rpg;

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
            used = new Either<Item, BodyPart>(new ItemRef(stream).Item);
        else
            used = new Either<Item, BodyPart>(new BodyPartRef(stream).BodyPart);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        if (used.IsLeft)
        {
            stream.WriteByte(1);
            new ItemRef(used.Left!).ToBytes(stream);
        }
        else
        {
            stream.WriteByte(0);
            new BodyPartRef(used.Right!).ToBytes(stream);
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

    public override double ModifyReceivingDamage(IDamageable attacked, DamageSource source, double damage)
    {
        if (source.SkillUsed is { } skill && 
            (skill.HasTag("melee") || skill.HasTag("projectile")) && 
            !skill.HasTag("magic") && 
            source.ContactEntity != null && 
            (attacked is Entity attackedEntity && 
            attackedEntity.CanSee(source.ContactEntity.Position.XY())))
        {
            var fs = (IFeatureSource)attacked;
            fs.Board.RunTaskLater(() => fs.RemoveFeature(this), 0);
            
            return 0;
        }
        return base.ModifyReceivingDamage(attacked, source, damage);
    }
}
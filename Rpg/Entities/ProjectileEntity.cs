namespace Rpg;

public class ProjectileEntity : Entity
{
    public Skill? UsedSkill;
    public Entity? Owner;
    public override EntityType GetEntityType()
    {
        return EntityType.Projectile;
    }

    public ProjectileEntity() : base()
    {
        
    }

    public ProjectileEntity(Stream stream) : base(stream)
    {
        if (stream.ReadBoolean())
            UsedSkill = Skill.FromBytes(stream);
        if (stream.ReadBoolean())
            Owner = new EntityRef(stream).Entity;
    }

    public override void Tick()
    {
        base.Tick();
        //TODO: Collision detection
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        if (UsedSkill != null)
        {
            stream.WriteBoolean(true);
            UsedSkill.ToBytes(stream);
        }
        else
            stream.WriteBoolean(false);

        if (Owner != null)
        {
            stream.WriteBoolean(true);
            new EntityRef(Owner).ToBytes(stream);
        }
        else
            stream.WriteBoolean(false);
    }
}
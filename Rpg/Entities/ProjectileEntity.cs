namespace Rpg;

public class ProjectileEntity : Entity
{
    public event Action<Entity> OnHit;
    
    public Skill? UsedSkill;
    public Entity? Owner;
    public bool DestroyOnHit = true;
    public bool DestroyOnHitWall = true;
        
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
        foreach (var entity in Floor.PossibleEntityIntersections(Hitbox))
        {
            if (Geometry.OBBOBBIntersection(entity.Hitbox, Hitbox, out _))
            {
                if (DestroyOnHit)
                {
                    Board.RemoveEntity(this);
                    OnHit?.Invoke(entity);
                    break;
                }
            }
        }

        if (DestroyOnHitWall)
        {
            foreach (var line in Floor.PossibleOBBIntersections(Hitbox))
            {
                if (Geometry.OBBLineIntersection(Hitbox, line, out _))
                {
                    Board.RemoveEntity(this);
                    break;
                }
            }
        }
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
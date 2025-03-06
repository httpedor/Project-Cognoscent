using System.Numerics;
using Rpg.Entities;

namespace Rpg;

public abstract class SkillArgument : ISerializable
{
    public static SkillArgument FromBytes(Stream stream)
    {
        var path = stream.ReadString();
        Type? type = Type.GetType(path);

        if (type == null)
            throw new Exception("Failed to get Skill Argument Type: " + path);
        if (type.GetConstructor(new Type[] { typeof(Stream) }) == null)
            throw new Exception("Failed to get SkillArgument constructor: " + path);
        return (SkillArgument)Activator.CreateInstance(type, stream);
    }
    public virtual void ToBytes(Stream stream)
    {
        stream.WriteString(GetType().FullName);
    }
}
public class PositionSkillArgument : SkillArgument
{
    Vector3 Position;
    public PositionSkillArgument(Vector3 pos)
    {
        Position = pos;
    }
    public PositionSkillArgument(Stream stream)
    {
        Position = stream.ReadVec3();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteVec3(Position);
    }
}
public class CreatureSkillArgument : SkillArgument
{
    protected CreatureRef cRef;
    public Creature? Creature {
        get {
            return cRef.Creature;
        }
    }
    public CreatureSkillArgument(Creature creature)
    {
        cRef = new CreatureRef(creature);
    }
    public CreatureSkillArgument(Stream stream)
    {
        cRef = new CreatureRef(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        cRef.ToBytes(stream);
    }
}
public class BodyPartSkillArgument : SkillArgument
{
    BodyPartRef bpRef;
    public BodyPart? Part
    {
        get {
            return bpRef.BodyPart;
        }
    }
    public BodyPartSkillArgument(BodyPart part)
    {
        bpRef = new BodyPartRef(part);
    }
    public BodyPartSkillArgument(Stream stream)
    {
        bpRef = new BodyPartRef(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        bpRef.ToBytes(stream);
    }
}

public class BooleanSkillArgument : SkillArgument
{
    bool Value;
    public BooleanSkillArgument(bool value)
    {
        Value = value;
    }

    public BooleanSkillArgument(Stream stream)
    {
        Value = stream.ReadByte() != 0;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteByte((Byte)(Value ? 1 : 0));
    }
}

public class EntitySkillArgument : SkillArgument
{
    EntityRef entity;
    public Entity? Entity => entity.Entity;
    public EntitySkillArgument(Entity entity)
    {
        this.entity = new EntityRef(entity);
    }
    public EntitySkillArgument(Stream stream)
    {
        entity = new EntityRef(stream);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        entity.ToBytes(stream);
    }
}
using System.Numerics;
using Rpg;
using Rpg.Inventory;

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
    public Vector3 Position;

    public PositionSkillArgument(Vector3 position)
    {
        Position = position;
    }
    public PositionSkillArgument(Stream stream) : this(stream.ReadVec3())
    {
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteVec3(Position);
    }
}
public class BodyPartSkillArgument : SkillArgument
{
    private readonly BodyPartRef bpRef;
    public BodyPart? Part => bpRef.BodyPart;

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

public class BooleanSkillArgument(bool value) : SkillArgument
{
    public BooleanSkillArgument(Stream stream) : this(stream.ReadByte() != 0)
    {
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteByte((byte)(value ? 1 : 0));
    }
}

public class EntitySkillArgument : SkillArgument
{
    private readonly EntityRef entity;
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

public class ItemSkillArgument : SkillArgument
{
    private ItemRef item;
    public Item? Item => item.Item;

    public ItemSkillArgument(Item item)
    {
        this.item = new ItemRef(item);
    }
    
    public ItemSkillArgument(Stream stream)
    {
        item = new ItemRef(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        item.ToBytes(stream);
    }
}
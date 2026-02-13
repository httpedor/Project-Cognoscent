using System.Numerics;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Components.Inventory;

namespace Rpg.Skills;

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

    public static Type ArgumentTypeFromString(string str)
    {
        return str switch
        {
            "entity" => typeof(EntitySkillArgument),
            "position" => typeof(PositionSkillArgument),
            "bodypart" => typeof(BodyPartSkillArgument),
            "item" => typeof(ItemSkillArgument),
            "boolean" => typeof(BooleanSkillArgument),
            _ => throw new Exception("Unknown argument type: " + str)
        };
    }
    public static string ArgumentTypeToString(Type type)
    {
        if (type == typeof(EntitySkillArgument))
            return "entity";
        if (type == typeof(PositionSkillArgument))
            return "position";
        if (type == typeof(BodyPartSkillArgument))
            return "bodypart";
        if (type == typeof(ItemSkillArgument))
            return "item";
        if (type == typeof(BooleanSkillArgument))
            return "boolean";
        throw new Exception("Unknown argument type: " + type.FullName);
    }
}
public abstract class ComponentArgument<T> : SkillArgument where T : Component
{
    private readonly ComponentRef<T> componentRef;
    public T? Component => componentRef.Component;

    public ComponentArgument(T component)
    {
        componentRef = new ComponentRef<T>(component);
    }
    public ComponentArgument(Stream stream)
    {
        componentRef = new ComponentRef<T>(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        componentRef.ToBytes(stream);
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
public class BodyPartSkillArgument : ComponentArgument<BodyPart>
{
    public BodyPartSkillArgument(BodyPart component) : base(component)
    {
    }
    public BodyPartSkillArgument(Stream stream) : base(stream)
    {
    }


    public BodyPart? Part => Component;
}

public class BooleanSkillArgument(bool value) : SkillArgument
{
    public bool Value => value;
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

public class ItemSkillArgument : ComponentArgument<Item>
{
    public Item? Item => Component;

    public ItemSkillArgument(Item item) : base(item)
    {
    }
    
    public ItemSkillArgument(Stream stream) : base(stream)
    {
    }
}
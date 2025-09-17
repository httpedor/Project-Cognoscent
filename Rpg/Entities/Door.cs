using System.Numerics;
using Rpg;

namespace Rpg;

public class DoorRef : ISerializable
{
    public readonly string Board;
    public readonly int Id;
    public DoorEntity? Door
    {
        get
        {
            Board? board = SidedLogic.Instance.GetBoard(Board);

            return board?.GetEntityById(Id) as DoorEntity;
        }
    }
    public DoorRef(DoorEntity door)
    {
        Board = door.Board.Name;
        Id = door.Id;
    }
    public DoorRef(Stream stream)
    {
        Board = stream.ReadString();
        Id = stream.ReadInt32();
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Board);
        stream.WriteInt32(Id);
    }
}
public class DoorEntity : Entity, IDamageable
{
    public Vector2[] Bounds;
    public Vector2 OpenBound2 {
        get {
            if (Slide)
                return Bounds[0];

            return Bounds[0] + Vector2.Transform(Bounds[1] - Bounds[0], Matrix3x2.CreateRotation(MathF.PI/2));
        }
    }
    public bool Closed;
    public bool BlocksVision;
    public bool Locked;
    public bool Slide;
    public double Health { get; }
    public double MaxHealth { get; }

    public DoorEntity()
    {
        Bounds = Array.Empty<Vector2>();
        Rotation = 0;
        Closed = true;
        BlocksVision = true;
        Locked = false;
        Slide = false;
    }

    public DoorEntity(Stream stream) : base(stream)
    {
        int len = stream.ReadByte();
        Bounds = new Vector2[len];
        for (int i = 0; i < len; i++)
        {
            Bounds[i] = stream.ReadVec2();
        }

        Closed = stream.ReadByte() != 0;
        BlocksVision = stream.ReadByte() != 0;
        Locked = stream.ReadByte() != 0;
        Slide = stream.ReadByte() != 0;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteByte((byte)Bounds.Length);
        foreach (Vector2 bound in Bounds)
        {
            stream.WriteVec2(bound);
        }

        stream.WriteByte((byte)(Closed ? 1 : 0));
        stream.WriteByte((byte)(BlocksVision ? 1 : 0));
        stream.WriteByte((byte)(Locked ? 1 : 0));
        stream.WriteByte((byte)(Slide ? 1 : 0));
    }

    public bool CanBeOpenedBy(Creature creature)
    {
        return !Locked && creature.FloorIndex == FloorIndex && (creature.Position.XY() - Position.XY()).Length() <= 1;
    }

    public override EntityType GetEntityType()
    {
        return EntityType.Door;
    }


    public void CopyFrom(DoorEntity door)
    {
        Bounds = door.Bounds;
        Closed = door.Closed;
        Position = door.Position;
        Rotation = door.Rotation;
        Size = door.Size;
        stats = door.stats;
        BlocksVision = door.BlocksVision;
        Slide = door.Slide;
        Locked = door.Locked;
    }

    public double Damage(DamageSource source, double damage)
    {
        throw new NotImplementedException();
    }

    public string BBLink { get; }
}

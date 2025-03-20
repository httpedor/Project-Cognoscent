using System.Numerics;
using Rpg.Entities;

namespace Rpg;

public class DoorRef : ISerializable
{
    public string Board;
    public int Id;
    public Door? Door
    {
        get
        {
            var board = SidedLogic.Instance.GetBoard(Board);
            if (board == null)
                return null;

            return board.GetEntityById(Id) as Door;
        }
    }
    public DoorRef(Door door)
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
public class Door : Entity, ISerializable
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

    public Door() : base()
    {
        Bounds = new Vector2[0];
        Rotation = 0;
        Closed = true;
        BlocksVision = true;
        Locked = false;
        Slide = false;
    }

    public Door(Stream stream) : base(stream)
    {
        var len = stream.ReadByte();
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

        stream.WriteByte((Byte)Bounds.Length);
        foreach (var bound in Bounds)
        {
            stream.WriteVec2(bound);
        }

        stream.WriteByte((Byte)(Closed ? 1 : 0));
        stream.WriteByte((Byte)(BlocksVision ? 1 : 0));
        stream.WriteByte((Byte)(Locked ? 1 : 0));
        stream.WriteByte((Byte)(Slide ? 1 : 0));
    }

    public bool CanBeOpenedBy(Creature creature)
    {
        return !Locked && creature.FloorIndex == FloorIndex && (creature.Position.XY() - Position.XY()).Length() <= 1;
    }

    public override EntityType GetEntityType()
    {
        return EntityType.Door;
    }


    public void CopyFrom(Door door)
    {
        Bounds = door.Bounds;
        Closed = door.Closed;
        Position = door.Position;
        Rotation = door.Rotation;
        Size = door.Size;
        _stats = door._stats;
        BlocksVision = door.BlocksVision;
        Slide = door.Slide;
        Locked = door.Locked;
    }
}

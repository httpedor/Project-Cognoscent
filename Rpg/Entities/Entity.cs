
using System.Collections;
using System.Diagnostics.Contracts;
using System.Numerics;

namespace Rpg.Entities;

public enum EntityType : byte
{
    Creature,
    Item,
    Projectile
}

public struct EntityRef : ISerializable
{
    public string Board;
    public int Id;
    public Entity? Entity
    {
        get
        {
            var board = SidedLogic.Instance.GetBoard(Board);
            if (board == null)
                return null;
            
            return board.GetEntityById(Id);
        }
    }
    public EntityRef(Entity target)
    {
        Board = target.Board.Name;
        Id = target.Id;
    }
    public EntityRef(string board, int id)
    {
        Board = board;
        Id = id;
    }
    
    public EntityRef(Stream stream)
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

public abstract class Entity : ISerializable
{
    public int Id { get; private set; }

    //First vector is new, second is old

    public event Action<Vector3, Vector3>? OnPositionChanged;
    public event Action<float, float>? OnRotationChanged;
    public event Action<Vector3, Vector3>? OnSizeChanged;
    public event Action<byte[]>? OnImageChanged;

    protected Vector3 _position;
    public Vector3 Position
    {
        get
        {
            return _position;
        }
        set
        {
            OnPositionChanged?.Invoke(value, _position);
            _position = value;
        }
    }
    protected float _rotation;
    public virtual float Rotation
    {
        get
        {
            return _rotation;
        }
        set
        {
            OnRotationChanged?.Invoke(value, _rotation);
            _rotation = value;
        }
    }
    public int FloorIndex
    {
        get
        {
            return (int)Position.Z;
        }
    }
    public bool IsGrounded
    {
        get
        {
            return (Position.Z % 1) == 0;
        }
    }
    protected Vector3 _size;
    public Vector3 Size
    {
        get
        {
            return _size;
        }
        set
        {
            OnSizeChanged?.Invoke(value, _size);
            _size = value;
        }
    }
    protected byte[] _image = new byte[0];
    public byte[] Image
    {
        get
        {
            return _image;
        }
        set
        {
            OnImageChanged?.Invoke(value);
            _image = value;
        }
    }
    protected Dictionary<string, Stat> _stats = new Dictionary<string, Stat>();
    public List<Stat> Stats
    {
        get
        {
            return _stats.Values.ToList();
        }
    }

    public OBB Hitbox
    {
        get
        {
            return new OBB(new Vector2(Position.X, Position.Y), new Vector2(Size.X/2, Size.Y/2), Rotation);
        }
    }

    Int64 features = 0;

    public Board Board;

    public Entity()
    {
        Id = new Random().Next();
        Position = new Vector3(0);
        Size = new Vector3(1, 1, 0.56f);
        Rotation = 0;
    }

    protected Entity(Stream stream)
    {
        Id = stream.ReadInt32();
        Position = stream.ReadVec3();
        Size = stream.ReadVec3();
        Rotation = stream.ReadFloat();
        features = stream.ReadInt64();
        byte count = (Byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            Stat stat = new Stat(stream);
            _stats[stat.Id] = stat;
        }
        Image = stream.ReadExactly(stream.ReadUInt32());
    }

    public abstract EntityType GetEntityType();

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteByte((byte)GetEntityType());
        stream.WriteInt32(Id);
        stream.WriteVec3(Position);
        stream.WriteVec3(Size);
        stream.WriteFloat(Rotation);
        stream.WriteInt64(features);
        stream.WriteByte((Byte)_stats.Count);
        foreach (var stat in _stats.Values)
            stat.ToBytes(stream);
        stream.WriteUInt32((uint)Image.Length);
        stream.Write(Image);
    }

    public static Entity FromBytes(Stream stream)
    {
        EntityType type = (EntityType)stream.ReadByte();
        switch (type)
        {
            case EntityType.Creature:
                return new Creature(stream);
            case EntityType.Item:
                return new ItemEntity(stream);
            default:
                throw new Exception("Invalid entity type");
        }
    }

    public Stat? GetStat(string id)
    {
        return _stats[id];
    }

    public Stat GetOrCreateStat(string id, float defaultValue = 0)
    {
        if (!_stats.ContainsKey(id))
            return CreateStat(id, defaultValue);
        return _stats[id];
    }

    public float GetStatValueOrDefault(string id, float defaultValue = 0)
    {
        if (_stats.ContainsKey(id))
            return GetStat(id).FinalValue;
        return defaultValue;
    }

    public Stat CreateStat(string id, float defaultValue = 0)
    {
        _stats[id] = new Stat(id, defaultValue);
        return _stats[id];
    }

    public virtual bool HasFeature(long feature)
    {
        return (features & feature) != 0;
    }

    public virtual void ClearEvents()
    {
        OnPositionChanged = null;
        OnRotationChanged = null;
        OnSizeChanged = null;
        OnImageChanged = null;

        foreach (var stat in Stats)
        {
            stat.ClearEvents();
        }
    }
}

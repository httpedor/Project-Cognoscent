using System.Numerics;
using System.Text.Json.Serialization;

namespace Rpg;

public enum EntityType : byte
{
    Creature,
    Item,
    Projectile,
    Door,
    Container,
    Light,
    Prop
}

public readonly struct EntityRef(string board, int id) : ISerializable
{
    public readonly string Board = board;
    public readonly int Id = id;

    [JsonIgnore]
    public Entity? Entity
    {
        get
        {
            Board? board = SidedLogic.Instance.GetBoard(Board);
            return board?.GetEntityById(Id);
        }
    }
    public EntityRef(Entity target) : this(target.Board.Name, target.Id)
    {
    }

    public EntityRef(Stream stream) : this(stream.ReadString(), stream.ReadInt32())
    {
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Board);
        stream.WriteInt32(Id);
    }
}

public abstract partial class Entity : ISerializable, IFeatureContainer, IStatHolder
{
    public int Id { get; }

    //First vector is new, second is old
    public event Action<Vector3, Vector3>? OnPositionChanged;
    public event Action<string, string>? OnNameChange;
    public event Action<float, float>? OnRotationChanged;
    public event Action<Vector3, Vector3>? OnSizeChanged;
    public event Action<Midia>? OnDisplayChanged;

    public string Name
    {
        get;
        set
        {
            OnNameChange?.Invoke(value, field);
            field = value;
        }
    }
    // ReSharper disable once InconsistentNaming
    [JsonIgnore]
    public string BBLink => "[url=gotoent " + Id + "]" + Name + "[/url]";

    public Vector3 Position
    {
        get;
        set
        {
            OnPositionChanged?.Invoke(value, field);
            field = value;
        }
    }

    public virtual float Rotation
    {
        get;
        set
        {
            OnRotationChanged?.Invoke(value, field);
            field = value;
        }
    }
    [JsonIgnore]
    public Vector2 Direction => new(MathF.Cos(Rotation), MathF.Sin(Rotation));

    [JsonIgnore]
    public int FloorIndex => (int)Position.Z;
    [JsonIgnore]
    public Floor Floor => Board.GetFloor(FloorIndex);
    [JsonIgnore]
    public bool IsGrounded => Position.Z % 1 == 0;

    public Vector3 Size
    {
        get;
        set
        {
            OnSizeChanged?.Invoke(value, field);
            field = value;
        }
    }
    [JsonIgnore]
    public Vector2 PixelSize => new(Floor.TileSize.X * Size.X, Floor.TileSize.Y * Size.Y);
    

    public Midia Display
    {
        get;
        set
        {
            OnDisplayChanged?.Invoke(value);
            field = value;
        }
    } = new();

    [JsonInclude]
    protected Dictionary<string, Stat> stats = new();

    [JsonIgnore]
    public IEnumerable<Stat> Stats => stats.Values;
    [JsonIgnore]
    public OBB Hitbox => new(new Vector2(Position.X, Position.Y), new Vector2(Size.X/2, Size.Y/2), Rotation);

    public Board Board { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected Entity()
    {
        Id = new Random().Next();
        Name = "Entity" + Id;
        Position = new Vector3(0);
        Size = new Vector3(1, 1, 0.56f);
        Rotation = 0;
    }

    protected Entity(Stream stream)
    {
        Id = stream.ReadInt32();
        Name = stream.ReadString();
        Position = stream.ReadVec3();
        Size = stream.ReadVec3();
        Rotation = stream.ReadFloat();
        CustomDataFromBytes(stream);
        FeaturesFromBytes(stream);
        StatsFromBytes(stream);

        Display = new Midia(stream);
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public virtual void Tick()
    {
        foreach (var feature in EnabledFeatures)
            feature.OnTick(this);
    }

    public abstract EntityType GetEntityType();

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteByte((byte)GetEntityType());
        stream.WriteInt32(Id);
        stream.WriteString(Name);
        stream.WriteVec3(Position);
        stream.WriteVec3(Size);
        stream.WriteFloat(Rotation);
        CustomDataToBytes(stream);
        FeaturesToBytes(stream);
        StatsToBytes(stream);
        Display.ToBytes(stream);
    }

    public bool CanSee(Vector2 target)
    {
        var targetDir = Vector2.Normalize(target - Position.XY());
        if (Vector2.Dot(Direction, targetDir) <= 0)
            return false;
        OBB LOS = new((Position.XY() + target) / 2, new Vector2((target - Position.XY()).Length() / 2, 0.1f), MathF.Atan2(targetDir.Y, targetDir.X));
        return Floor.OBBWallIntersection(LOS);
    }

    public Stream? GetCustomDataStream(string id)
    {
        if (customData.TryGetValue(id, out byte[]? data))
            return new MemoryStream(data);
        return null;
    }

    public virtual void ClearEvents()
    {
        OnDisplayChanged = null;
        OnFeatureAdded = null;
        OnFeatureEnabled = null;
        OnFeatureDisabled = null;
        OnFeatureRemoved = null;
        OnDisplayChanged = null;
        OnNameChange = null;
        OnStatCreated = null;
        OnPositionChanged = null;
        OnRotationChanged = null;
        OnSizeChanged = null;

        foreach (Stat stat in Stats)
        {
            stat.ClearEvents();
        }
    }

    public static Entity FromBytes(Stream stream)
    {
        var type = (EntityType)stream.ReadByte();
        return type switch
        {
            EntityType.Creature => new Creature(stream),
            EntityType.Item => new ItemEntity(stream),
            EntityType.Door => new DoorEntity(stream),
            EntityType.Light => new LightEntity(stream),
            EntityType.Prop => new PropEntity(stream),
            _ => throw new Exception("Invalid entity type: " + type)
        };
    }
}

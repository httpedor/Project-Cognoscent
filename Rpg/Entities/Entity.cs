using System.Numerics;

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

public abstract class Entity : ISerializable, IFeatureSource
{
    public int Id { get; }

    //First vector is new, second is old
    public event Action<Vector3, Vector3>? OnPositionChanged;
    public event Action<string, string>? OnNameChange;
    public event Action<float, float>? OnRotationChanged;
    public event Action<Vector3, Vector3>? OnSizeChanged;
    public event Action<Midia>? OnDisplayChanged;
    public event Action<Stat>? OnStatCreated;
    public event Action<Feature>? OnFeatureAdded;
    public event Action<Feature>? OnFeatureRemoved;
    public event Action<Feature>? OnFeatureEnabled;
    public event Action<Feature>? OnFeatureDisabled;

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
    public Vector2 Direction => new(MathF.Cos(Rotation), MathF.Sin(Rotation));

    public int FloorIndex => (int)Position.Z;
    public Floor Floor => Board.GetFloor(FloorIndex);
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

    protected Dictionary<string, Stat> stats = new();

    protected Dictionary<string, (Feature feature, bool enabled)> features = new();
    public Dictionary<string, (Feature feature, bool enabled)> FeaturesDict => features;
    public IEnumerable<Stat> Stats => stats.Values;
    public IEnumerable<Feature> Features => FeaturesDict.Values.Select(t => t.feature);
    public IEnumerable<Feature> EnabledFeatures => FeaturesDict.Values.Where(t => t.enabled).Select(t => t.feature);
    protected Dictionary<string, byte[]> customData = new();

    public OBB Hitbox => new(new Vector2(Position.X, Position.Y), new Vector2(Size.X/2, Size.Y/2), Rotation);

    public Board Board { get; set; }

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
        byte count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            string name = stream.ReadString();
            byte[] data = new byte[stream.ReadUInt32()];
            stream.ReadExactly(data);
            customData[name] = data;
        }
        count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            bool enabled = stream.ReadByte() == 1;
            Feature feature = Feature.FromBytes(stream);
            features[feature.GetId()] = (feature, enabled);
        }

        count = (byte)stream.ReadByte();
        for (int i = 0; i < count; i++)
        {
            var stat = new Stat(stream);
            stats[stat.Id] = stat;
        }
        Display = new Midia(stream);
    }

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
        stream.WriteByte((byte)customData.Count);
        foreach (var pair in customData)
        {
            stream.WriteString(pair.Key);
            stream.WriteUInt32((uint)pair.Value.Length);
            stream.Write(pair.Value);
        }
        stream.WriteByte((byte)features.Count);
        foreach (var feature in features)
        {
            stream.WriteByte((byte)(feature.Value.enabled ? 1 : 0));
            feature.Value.feature.ToBytes(stream);
        }
        stream.WriteByte((byte)stats.Count);
        foreach (Stat stat in stats.Values)
            stat.ToBytes(stream);
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

    public Stat? GetStat(string id)
    {
        return stats.GetValueOrDefault(id);
    }

    public float GetStatValue(string id, float defaultValue = 0)
    {
        if (stats.TryGetValue(id, out Stat? stat))
            return stat.FinalValue;
        return defaultValue;
    }
    public float this[string id, float defaultValue=0] => GetStatValue(id, defaultValue);

    public Stat CreateStat(Stat stat)
    {
        var id = stat.Id;
        stats[id] = stat;
        OnStatCreated?.Invoke(stats[id]);
        return stats[id];
    }

    public void AddFeature(Feature feature)
    {
        if (((IFeatureSource)this).HasFeature(feature))
            return;
        ((IFeatureSource)this).AddFeature(feature);
        OnFeatureAdded?.Invoke(feature);
    }

    public Feature? RemoveFeature(string id)
    {
        var removed = ((IFeatureSource)this).RemoveFeature(id);
        if (removed != null)
            OnFeatureRemoved?.Invoke(removed);
        
        return removed;
    }

    public Feature? RemoveFeature(Feature feature)
    {
        return RemoveFeature(feature.GetId());
    }
    
    public void EnableFeature(string id)
    {
        if (((IFeatureSource)this).EnableFeature(id))
            OnFeatureEnabled?.Invoke(features[id].feature);
    }

    public void DisableFeature(string id)
    {
        if (((IFeatureSource)this).DisableFeature(id))
            OnFeatureDisabled?.Invoke(features[id].feature);
    }

    public Stream? GetCustomDataStream(string id)
    {
        if (customData.TryGetValue(id, out byte[]? data))
            return new MemoryStream(data);
        return null;
    }

    public byte[]? GetCustomData(string id)
    {
        return customData.GetValueOrDefault(id);
    }

    public void SetCustomData(string id, byte[]? data)
    {
        if (data == null)
            customData.Remove(id);
        else
            customData[id] = data;
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

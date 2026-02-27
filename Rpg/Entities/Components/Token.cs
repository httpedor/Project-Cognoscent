using System.Numerics;
using System.Text.Json.Serialization;
using Rpg.Entities.Interfaces;

namespace Rpg.Entities.Components;

public class TokenUpdateEvent : ComponentEvent
{
    public Vector3 OldPosition;
    public float OldRotation;
    public Vector3 OldSize;
    public Midia? OldMidia;
    public TokenUpdateEvent(Component component, Vector3 oldPosition, float oldRotation, Vector3 oldSize, Midia? oldMidia) : base(component)
    {
        OldPosition = oldPosition;
        OldRotation = oldRotation;
        OldSize = oldSize;
        OldMidia = oldMidia;
    }
}
public partial class Token : Component, ITickableComponent, ICopyable<Token>
{
    private Vector3 oldPosition;
    private float oldRotation;
    private Vector3 oldSize;
    private Midia? oldMidia;

    public Vector3 Position;
    public float Rotation;
    public Vector3 Size = Vector3.One;
    public Midia? Midia;

    [JsonIgnore]
    public bool IsPlaced => Board != null;
    [JsonIgnore]
    public Vector2 Direction => new(MathF.Cos(Rotation), MathF.Sin(Rotation));
    [JsonIgnore]
    public int FloorIndex => (int)Position.Z;
    [JsonIgnore]
    public Floor? Floor => Board?.GetFloor(FloorIndex);
    [JsonIgnore]
    public bool IsGrounded => Position.Z % 1 == 0;
    [JsonIgnore]
    public Vector2? PixelSize => Floor == null ? null : new Vector2(Floor.TileSize.X * Size.X, Floor.TileSize.Y * Size.Y);

    [JsonIgnore]
    public OBB Hitbox => new(new Vector2(Position.X, Position.Y), new Vector2(Size.X/2, Size.Y/2), Rotation);

    public Token()
    {

    }


    public void PreTick()
    {
        oldPosition = Position;
        oldRotation = Rotation;
        oldSize = Size;
        oldMidia = Midia;
    }
    public void PostTick()
    {
        if (Position != oldPosition || Rotation != oldRotation || Size != oldSize || Midia != oldMidia)
        {
            Entity.DispatchEvent(new TokenUpdateEvent(this, oldPosition, oldRotation, oldSize, oldMidia));
        }
    }

    public bool CanSee(Vector2 target)
    {
        if (Floor == null)
            return false;
        var targetDir = Vector2.Normalize(target - Position.XY());
        if (Vector2.Dot(Direction, targetDir) <= 0)
            return false;
        OBB LOS = new((Position.XY() + target) / 2, new Vector2((target - Position.XY()).Length() / 2, 0.1f), MathF.Atan2(targetDir.Y, targetDir.X));
        return Floor.OBBWallIntersection(LOS);
    }

    public void CopyFrom(Token other)
    {
        Position = other.Position;
        Rotation = other.Rotation;
        Size = other.Size;
        Midia = other.Midia;
    }

    public Token(Stream stream) : base(stream)
    {
        Position = stream.ReadVec3();
        Rotation = stream.ReadFloat();
        Size = stream.ReadVec3();
        bool hasMidia = stream.ReadBoolean();
        if (hasMidia)
            Midia = new Midia(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteVec3(Position);
        stream.WriteFloat(Rotation);
        stream.WriteVec3(Size);
        stream.WriteBoolean(Midia != null);
        if (Midia != null)
            Midia.ToBytes(stream);
    }
}
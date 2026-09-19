using System.Numerics;

namespace Rpg.Entities.Components;

[RegisterComponent]
public partial class Door : Component, ICopyable<Door>
{
    [RequiredComponent(typeof(Token))]
    private Token? token;

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

    public Door()
    {
        Bounds = Array.Empty<Vector2>();
        Closed = true;
        BlocksVision = true;
        Locked = false;
        Slide = false;
    }
    public Door(Stream stream) : base(stream)
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

    public void CopyFrom(Door other)
    {
        Bounds = other.Bounds;
        Closed = other.Closed;
        BlocksVision = other.BlocksVision;
        Locked = other.Locked;
        Slide = other.Slide;
    }
}
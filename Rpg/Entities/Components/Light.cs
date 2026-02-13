namespace Rpg.Entities.Components;

public partial class Light : Component
{
    public float Range;
    public float Intensity;
    public float MinIntensity;
    public float MaxIntensity;
    public UInt32 Color;
    public bool Shadows;

    [RequiredComponent(typeof(Token))]
    public Token token;

    public Light()
    {

    }
    public Light(Stream stream) : base(stream)
    {
        Range = stream.ReadFloat();
        Intensity = stream.ReadFloat();
        Color = stream.ReadUInt32();
        Shadows = stream.ReadByte() != 0;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteFloat(Range);
        stream.WriteFloat(Intensity);
        stream.WriteUInt32(Color);
        stream.WriteByte((byte)(Shadows ? 1 : 0));
    }
}
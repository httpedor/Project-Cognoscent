namespace Rpg;

public class LightEntity : Entity
{
    public float Range;
    public float Intensity;
    public float MinIntensity;
    public float MaxIntensity;
    public UInt32 Color;
    public bool Shadows;
    public override EntityType GetEntityType()
    {
        return EntityType.Light;
    }

    public LightEntity() : base()
    {

    }
    public LightEntity(Stream stream) : base(stream)
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

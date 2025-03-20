namespace Rpg.Entities;

public class PropEntity : Entity
{
    public byte[] MidiaToShow;
    public bool IsShownMidiaVideo;
    public override EntityType GetEntityType()
    {
        return EntityType.Prop;
    }

    public PropEntity() : base()
    {
        MidiaToShow = new byte[0];
        IsShownMidiaVideo = false;
    }
    public PropEntity(Stream stream) : base(stream)
    {
        MidiaToShow = stream.ReadExactly(stream.ReadUInt32());
        IsShownMidiaVideo = stream.ReadByte() != 0;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteUInt32((UInt32)MidiaToShow.Length);
        stream.Write(MidiaToShow);
        stream.WriteByte((Byte)(IsShownMidiaVideo ? 1 : 0));
    }
}

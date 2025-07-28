namespace Rpg;

public class PropEntity : Entity
{
    public Midia? ShownMidia;
    public override EntityType GetEntityType()
    {
        return EntityType.Prop;
    }

    public PropEntity() : base()
    {
        ShownMidia = null;
    }
    public PropEntity(Stream stream) : base(stream)
    {
        if (stream.ReadByte() != 0)
            ShownMidia = new Midia(stream);
        else
            ShownMidia = null;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        if (ShownMidia.HasValue)
        {
            stream.WriteByte(1);
            ShownMidia.Value.ToBytes(stream);
        }
        else
            stream.WriteByte(0);
    }
}

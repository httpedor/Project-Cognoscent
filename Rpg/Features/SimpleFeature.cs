namespace Rpg;

public class SimpleFeature : Feature
{
    protected readonly string id;
    protected readonly string description;
    protected readonly bool toggleable;
    public SimpleFeature(string id, string name, string description, bool toggleable = false)
    {
        this.id = id;
        CustomName = name;
        this.description = description;
        this.toggleable = toggleable;
    }

    public SimpleFeature(Stream stream) : base(stream)
    {
        id = stream.ReadString();
        description = stream.ReadLongString();
        toggleable = stream.ReadBoolean();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(id);
        stream.WriteLongString(description);
        stream.WriteBoolean(toggleable);
    }

    public override bool IsToggleable(Entity entity)
    {
        return toggleable;
    }

    public override string GetId()
    {
        return id;
    }
    public override string GetDescription()
    {
        return description;
    }
}
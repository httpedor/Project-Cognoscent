namespace Rpg;

public class SimpleCondition : ConditionFeature
{
    private readonly string id;
    private readonly string description;
    private readonly bool toggleable;
    
    public SimpleCondition(string id, string name, string description, bool toggleable, uint ticks = 0) : base(ticks)
    {
        this.id = id;
        CustomName = name;
        this.description = description;
        this.toggleable = toggleable;
    }

    public SimpleCondition(Stream stream) : base(stream)
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

    public SimpleCondition WithDuration(uint ticks)
    {
        return new SimpleCondition(id, CustomName!, description, toggleable, ticks);
    }

    public override string GetId()
    {
        return id;
    }

    public override string GetDescription()
    {
        return description;
    }

    public override bool IsToggleable(Entity entity)
    {
        return toggleable;
    }
}
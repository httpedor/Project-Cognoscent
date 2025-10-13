namespace Rpg;

public class SimpleCondition : ConditionFeature
{
    private readonly string id;
    private readonly string description;
    private readonly bool toggleable;
    private readonly bool hidden;
    
    public SimpleCondition(string id, string name, string description, bool toggleable, uint ticks = 0, bool hidden = false) : base(ticks)
    {
        this.id = id;
        CustomName = name;
        this.description = description;
        this.toggleable = toggleable;
        this.hidden = hidden;
    }

    public SimpleCondition(Stream stream) : base(stream)
    {
        id = stream.ReadString();
        description = stream.ReadLongString();
        toggleable = stream.ReadBoolean();
        hidden = stream.ReadBoolean();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(id);
        stream.WriteLongString(description);
        stream.WriteBoolean(toggleable);
        stream.WriteBoolean(hidden);
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

    public override bool CanBeSeenBy(Entity viewer)
    {
        return !hidden;
    }
}
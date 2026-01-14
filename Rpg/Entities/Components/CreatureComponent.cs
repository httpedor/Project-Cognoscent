namespace Rpg.Entities;

public class ActionLayer(string name, string id, uint startTick, uint delay, uint duration, uint cooldown, float concentration, bool cancelable = true) : ISerializable
{
    public string Name = name;
    public string Id = id;
    public uint StartTick = startTick;
    public uint Delay = delay;
    public uint Duration = duration;
    public uint Cooldown = cooldown;
    public bool Cancelable = cancelable;

    public float Concentration = concentration;
    
    public uint ExecutionStartTick => StartTick + Delay + 1;
    public uint ExecutionEndTick => ExecutionStartTick + Duration;
    public uint EndTick => StartTick + Delay + Duration + Cooldown;

    public ActionLayer(Stream stream) : this(
        stream.ReadString(),
        stream.ReadString(),
        stream.ReadUInt32(),
        stream.ReadUInt32(),
        stream.ReadUInt32(),
        stream.ReadUInt32(),
        stream.ReadFloat(),
        stream.ReadByte() != 0
        )
    {
        
    }
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Name);
        stream.WriteString(Id);
        stream.WriteUInt32(StartTick);
        stream.WriteUInt32(Delay);
        stream.WriteUInt32(Duration);
        stream.WriteUInt32(Cooldown);
        stream.WriteFloat(Concentration);
        stream.WriteByte((byte)(Cancelable ? 1 : 0));
    }
}
public partial class CreatureComponent : Component
{
    [RequiredComponent(typeof(TokenComponent))]
    public TokenComponent Token;
    [RequiredComponent(typeof(StatsComponent))]
    public StatsComponent Stats;
    [RequiredComponent(typeof(FeaturesComponent))]
    public FeaturesComponent Features;
    [RequiredComponent(typeof(BodyComponent))]
    public BodyComponent Body;

    public string? Owner;

    public CreatureComponent()
    {

    }
    public CreatureComponent(Stream stream) : base(stream)
    {
        Owner = stream.ReadString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Owner ?? "");
    }

    public static implicit operator Entity(CreatureComponent creature)
    {
        return creature.Entity;
    }
}
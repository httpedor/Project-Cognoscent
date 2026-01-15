namespace Rpg.Entities;

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
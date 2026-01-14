namespace Rpg.Entities;

public abstract partial class ItemPropertyComponent : Component
{
    [RequiredComponent(typeof(ItemComponent))]
    public required ItemComponent Item;

    public ItemPropertyComponent()
    {

    }
    public ItemPropertyComponent(Stream stream) : base(stream)
    {

    }
}
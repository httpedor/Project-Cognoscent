namespace Rpg.Entities.Components.Inventory;

public abstract partial class ItemProperty : Component
{
    [RequiredComponent(typeof(Item))]
    public Item Item;

    public ItemProperty()
    {

    }
    public ItemProperty(Stream stream) : base(stream)
    {

    }
}
namespace Rpg.Inventory;

public interface IInventoryHolder
{
    public virtual void AddItem(Item item)
    {
        item.Holder = this;
    }
    public virtual void RemoveItem(Item item)
    {
        item.Holder = null;
    }

    public IEnumerable<Item> Items
    {
        get;
    }
}

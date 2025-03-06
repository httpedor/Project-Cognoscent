namespace Rpg.Inventory;

public interface IInventoryHolder
{
    public List<Item> Inventory
    {
        get;
    }

    public void AddItem(Item item)
    {
        Inventory.Add(item);
        item.Holder = this;
    }
    public void RemoveItem(Item item)
    {
        item.Holder = null;
        Inventory.Remove(item);
    }
}

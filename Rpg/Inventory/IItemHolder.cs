namespace Rpg.Inventory;

public interface IItemHolder
{
    public abstract bool CanAddItem(Item item);
    public void AddItem(Item item);
    public void RemoveItem(Item item);

    public IEnumerable<Item> Items
    {
        get;
    }

    public Board? Board
    {
        get;
    }
}

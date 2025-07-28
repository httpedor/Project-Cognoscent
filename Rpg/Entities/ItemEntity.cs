using System.Buffers.Text;
using System.Numerics;
using Rpg.Inventory;

namespace Rpg;

public class ItemEntity : Entity, IItemHolder
{
    public Item Item
    {
        get => field;
        set
        {
            field = value;
        }
    }

    public IEnumerable<Item> Items => new []{Item};
    Board? IItemHolder.Board => Board;


    public ItemEntity(Item item) : base()
    {
        Item = item;
        Display = new Midia(Convert.FromBase64String(B64Images.Box));
    }

    public ItemEntity(Stream stream) : base(stream)
    {
        Item = new Item(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Item.ToBytes(stream);
    }

    public override EntityType GetEntityType()
    {
        return EntityType.Item;
    }

    public bool CanAddItem(Item item)
    {
        return Item == null;
    }
    public void AddItem(Item item)
    {
        Item = item;
    }
    public void RemoveItem(Item item)
    {
        if (Item == item)
        {
            Item = null;
            Board.RemoveEntity(this);
        }
    }
}

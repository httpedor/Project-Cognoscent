
namespace Rpg.Inventory;

public class ItemHolderProperty : ItemProperty, IItemHolder
{
    public ushort Size;
    public Item?[] Items;
    public ItemHolderProperty(Item item, ushort size) : base(item)
    {
        Items = new Item[size];
        Size = size;
    }
    public ItemHolderProperty(Stream stream) : base(stream)
    {
        Size = stream.ReadUInt16();
        Items = new Item[Size];
        for (int i = 0; i < Size; i++)
        {
            if (stream.ReadByte() != 0)
            {
                Items[i] = new Item(stream);
            }
            else
                Items[i] = null;
        }

    }

    public bool CanAddItem(Item item)
    {
        return Array.IndexOf(Items, null) >= 0;
    }
    public void AddItem(Item item)
    {
        Items[Array.IndexOf(Items, null)] = item;
    }
    public void RemoveItem(Item item)
    {
        Items[Array.IndexOf(Items, item)] = null;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteUInt16((UInt16)Size);
        for (int i = 0; i < Size; i++)
        {
            if (Items[i] == null)
                stream.WriteByte(0);
            else
            {
                stream.WriteByte(1);
                Items[i].ToBytes(stream);
            }
        }
    }

    IEnumerable<Item> IItemHolder.Items {
        get {
            foreach (var item in Items)
                if (item != null)
                    yield return item;
        }
    }

    Board? IItemHolder.Board => Item.Holder?.Board;
}

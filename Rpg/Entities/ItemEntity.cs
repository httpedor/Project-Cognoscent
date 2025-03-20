using System.Buffers.Text;
using System.Numerics;
using Rpg.Inventory;

namespace Rpg.Entities;

public class ItemEntity : Entity
{
    private Item _item;
    public Item Item
    {
        get => _item;
        set
        {
            _item = value;
            //Size = new Vector3(value.Size, 0.01f);
        }
    }
    public ItemEntity(Item item) : base()
    {
        Item = item;
        Display = new Midia(Convert.FromBase64String(B64Images.Box));
    }

    public ItemEntity(Stream stream) : base(stream)
    {
        Item = Item.FromBytes(stream);
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
}

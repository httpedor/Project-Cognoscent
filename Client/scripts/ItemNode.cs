using Rpg;
using Rpg.Inventory;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

public partial class ItemNode : EntityNode
{
    public readonly ItemEntity ItemEnt;
    public readonly Item Item;
    public ItemNode(ItemEntity ent, ClientBoard board) : base(ent, board)
    {
        ItemEnt = ent;
        Item = ItemEnt.Item;
    }

    public override void AddContextMenuOptions()
    {
        base.AddContextMenuOptions();

        InputManager.Instance.PopulateContextMenuWithItem(Item);
    }

}

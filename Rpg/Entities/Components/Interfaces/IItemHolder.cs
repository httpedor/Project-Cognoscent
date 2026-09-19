using Rpg.Entities.Components.Inventory;

namespace Rpg.Entities.Interfaces;

[RegisterComponent]
public interface IItemHolder
{
    bool HasItem(Item item);
    void AddItem(Item item);
    void RemoveItem(Item item);
}
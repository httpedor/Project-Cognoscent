using Rpg;

namespace Server.Network;

public static class NetworkHooks
{
    public static void HookStat(Stat stat, Entity entity)
    {
        stat.BaseValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, newValue), entity.Board.Name);
        stat.ModifierUpdated += modifier => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, modifier), entity.Board.Name);
        stat.ModifierRemoved += modifier => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, modifier.Id), entity.Board.Name);
        stat.MinValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, newValue, StatValueType.Min), entity.Board.Name);
        stat.MaxValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, newValue, StatValueType.Max), entity.Board.Name);
    }

    public static void HookEntity(Entity entity)
    {
    }
}

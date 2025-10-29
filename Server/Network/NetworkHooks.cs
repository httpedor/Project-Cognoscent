using Rpg;

namespace Server.Network;

public static class NetworkHooks
{
    public static void HookStat(Stat stat, Entity entity)
    {
        stat.BaseValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, newValue), Name);
        stat.ModifierUpdated += modifier => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, modifier), Name);
        stat.ModifierRemoved += modifier => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, modifier.Id), Name);
        stat.MinValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, newValue, Rpg.StatValueType.Min), Name);
        stat.MaxValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatPacket(entity, stat.Id, newValue, Rpg.StatValueType.Max), Name);
    }
}

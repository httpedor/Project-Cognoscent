using Rpg;

namespace Server.Network;

public static class NetworkHooks
{
    public static void HookBodyPart(BodyPart part)
    {
        part.OnChildAdded += grandChild =>
        {
            Network.Manager.SendIfBoardValid(new EntityBodyPartPacket(grandChild), part.Creature?.Board.Name);
            HookBodyPart(grandChild);
        };

        part.OnChildRemoved += grandChild => {
            if (part.Creature == null)
                return;
            Network.Manager.SendIfBoardValid(new EntityBodyPartPacket(part.Creature, part.Path + "/" + grandChild.Name), part.Creature.Board.Name);
        };

        part.OnInjuryAdded += condition => {
            Network.Manager.SendIfBoardValid(new EntityBodyPartInjuryPacket(part, condition, EntityBodyPartInjuryPacket.InjuryPacketType.ADD), part.Creature?.Board.Name);
        };
        part.OnInjuryRemoved += condition => {
            Network.Manager.SendIfBoardValid(new EntityBodyPartInjuryPacket(part, condition, EntityBodyPartInjuryPacket.InjuryPacketType.REMOVE), part.Creature?.Board.Name);
        };
        part.OnInjuryChanged += (newCondition, oldCondition) => {
            Network.Manager.SendIfBoardValid(new EntityBodyPartInjuryPacket(part, newCondition, oldCondition), part.Creature?.Board.Name);
        };
        part.OnEquipped += (equipment, slot) => {
            Network.Manager.SendIfBoardValid(new CreatureEquipItemPacket(part, slot, equipment.Item), part.Creature?.Board.Name);
        };
        part.OnUnequipped += equipment => {
            Network.Manager.SendIfBoardValid(new CreatureEquipItemPacket(equipment.Item), part.Creature?.Board.Name);
        };
        part.OnFeatureAdded += feature =>
        {
        
        };
    }
}

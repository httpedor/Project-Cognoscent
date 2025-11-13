using Rpg;

namespace Server.Network;

public static class NetworkHooks
{
    private static HashSet<WeakReference<BodyPart>> hookedBodyParts = new();
    
    public static void ClearDestroyedObjects()
    {
        hookedBodyParts.RemoveWhere(wr => !wr.TryGetTarget(out _));
    }

    public static void HookEntity(Entity entity)
    {
    }

    public static void HookBodyPart(BodyPart part)
    {
        if (hookedBodyParts.Any(wr => wr.TryGetTarget(out var bp) && bp == part))
            return;

        hookedBodyParts.Add(new WeakReference<BodyPart>(part));

        part.OnChildAdded += grandChild =>
        {
            Network.Manager.SendIfBoardValid(new EntityBodyPartPacket(grandChild), part.Owner?.Board.Name);
            HookBodyPart(grandChild);
        };

        part.OnChildRemoved += grandChild => {
            if (part.Owner == null)
                return;
            Network.Manager.SendIfBoardValid(new EntityBodyPartPacket(part.Owner, part.Path + "/" + grandChild.Name), part.Owner.Board.Name);
        };

        part.OnInjuryAdded += condition => {
            Network.Manager.SendIfBoardValid(new EntityBodyPartInjuryPacket(part, condition, EntityBodyPartInjuryPacket.InjuryPacketType.ADD), part.Owner?.Board.Name);
        };
        part.OnInjuryRemoved += condition => {
            Network.Manager.SendIfBoardValid(new EntityBodyPartInjuryPacket(part, condition, EntityBodyPartInjuryPacket.InjuryPacketType.REMOVE), part.Owner?.Board.Name);
        };
        part.OnInjuryChanged += (newCondition, oldCondition) => {
            Network.Manager.SendIfBoardValid(new EntityBodyPartInjuryPacket(part, newCondition, oldCondition), part.Owner?.Board.Name);
        };
        part.OnEquipped += (equipment, slot) => {
            Network.Manager.SendIfBoardValid(new CreatureEquipItemPacket(part, slot, equipment.Item), part.Owner?.Board.Name);
        };
        part.OnUnequipped += equipment => {
            Network.Manager.SendIfBoardValid(new CreatureEquipItemPacket(equipment.Item), part.Owner?.Board.Name);
        };
        part.OnFeatureAdded += feature =>
        {
        
        };
    }
}

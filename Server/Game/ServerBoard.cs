using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Rpg;
using Rpg.Entities;
using Rpg.Inventory;
using Server.Network;

namespace Server.Game;

public class ServerBoard : Board, ISerializable
{
    private readonly LinkedList<(CreatureComponent executor, ActionLayer layer)> actionQueue = [];

    public ServerBoard(string name)
    {
        Name = name;
    }

    public ServerBoard(Stream stream)
    {
        Name = stream.ReadString();
        ushort chatHistoryCount = stream.ReadUInt16();
        for (int i = 0; i < chatHistoryCount; i++)
        {
            chatHistory.Add(stream.ReadLongString());
        }
        floors = new ServerFloor[stream.ReadByte()];
        for (int i = 0; i < floors.Length; i++)
        {
            floors[i] = new ServerFloor(stream);
        }

        ushort entityCount = stream.ReadUInt16();
        for (int i = 0; i < entityCount; i++)
        {
            Entity entity = new Entity(stream);
            AddEntity(entity);
        }
        foreach (var entity in entityCache.Values)
        {
            entity.Initialize();
        }
    }

    public override void PauseAt(uint tick)
    {
        base.PauseAt(tick);
        
        Manager.SendToBoard(new CombatModePacket(this), this);
    }

    public override void Tick()
    {
        foreach (Entity entity in entityCache.Values)
        {
            entity.Tick();
            if (entity.TryGetComponent<StatsComponent>(out var stats) && entity.ExistanceTicks % 20 == 0)
            {
                Manager.SendToBoard(new StatHolderUpdatePacket(stats), this);
            }
        }
        if (TurnMode || CurrentTick % 50 == 0)
            Manager.SendToBoard(new CombatModePacket(this), this);
        if (actionQueue.Count > 0)
        {
            uint firstInQueue = actionQueue.First!.Value.layer.EndTick;
            if (pauseTick > firstInQueue)
                PauseAt(firstInQueue);
            if (firstInQueue <= CurrentTick)
                actionQueue.RemoveFirst();
            
        }
        base.Tick();

        if (CurrentTick % 600 == 0)
            NetworkHooks.ClearDestroyedObjects();
    }

    public override void AddEntity(Entity entity)
    {
        base.AddEntity(entity);
        Network.Manager.SendToBoard(new EntityCreatePacket(this, entity), Name);
        if (entity is Creature creature)
        {
            creature.OnSkillStart += skill => {
                Network.Manager.SendToBoard(new CreatureSkillUpdatePacket(creature, skill), Name);
            };
            creature.OnSkillCancel += skill => {
                Network.Manager.SendToBoard(new CreatureSkillRemovePacket(creature, skill.Id), Name);
            };

            creature.ActionLayerChanged += layer =>
            {
                Network.Manager.SendToBoard(new ActionLayerUpdatePacket(creature, layer), Name);
                if (!TurnMode)
                    return;
                
                bool foundOld = false;
                
                LinkedListNode<(Creature executor, ActionLayer layer)>? chosenPrev = null;
                var node = actionQueue.First;
                while (node != null)
                {
                    var tuple = node.Value;
                    if (!foundOld && tuple.layer.Name == layer.Name && tuple.executor == creature)
                    {
                        actionQueue.Remove(tuple);
                        foundOld = true;
                        continue;
                    }

                    if (chosenPrev == null && tuple.layer.StartTick > layer.StartTick)
                        chosenPrev = node;
                    
                    if (foundOld && chosenPrev != null)
                        break;
                    
                    node = node.Next;
                }
                if (chosenPrev == null)
                    actionQueue.AddLast(new LinkedListNode<(Creature executor, ActionLayer layer)>((creature, layer)));
                else
                    actionQueue.AddBefore(chosenPrev, new LinkedListNode<(Creature executor, ActionLayer layer)>((creature, layer)));
                
            };
            creature.ActionLayerRemoved += layer =>
            {
                Network.Manager.SendToBoard(new ActionLayerRemovePacket(creature, layer), Name);
            };

            foreach (BodyPart part in creature.Body.Parts)
            {
                NetworkHooks.HookBodyPart(part);
            }
        }

    }

    public override void HandleEvent(ComponentEvent e)
    {
        base.HandleEvent(e);

        switch (e)
        {
            case TokenUpdateEvent tue:
                var token = (TokenComponent)tue.Component;
                Network.Manager.SendToBoard(new TokenUpdatePacket(token), this);
                if (tue.OldPosition != token.Position && token.IsPlaced)
                {
                    (token.Floor as ServerFloor)?.UpdateEntityCollisionGrid(token);
                }
                break;
            case FeatureAddedEvent fae:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Add(new EntityWith<FeaturesComponent>(fae.Component.Entity), fae.Feature),
                    this);
                break;
            case FeatureRemovedEvent fre:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Remove(new EntityWith<FeaturesComponent>(fre.Component.Entity), fre.Feature),
                    this
                );
                break;
            case FeatureEnabledEvent fee:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Enable(new EntityWith<FeaturesComponent>(fee.Component.Entity), fee.Feature),
                    this
                );
                break;
            case FeatureDisabledEvent fde:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Disable(new EntityWith<FeaturesComponent>(fde.Component.Entity), fde.Feature),
                    this
                );
                break;
        }
    }

    public override void RemoveEntity(Entity? entity)
    {
        if (entity != null)
        {
            Network.Manager.SendToBoard(new EntityRemovePacket(entity), Name);
        }

        base.RemoveEntity(entity);
    }

    public override void BroadcastMessage(string message)
    {
        Network.Manager.SendToBoard(new ChatPacket(this, message), this);
        AddChatMessage(message);
    }

    public override void StartTurnMode()
    {
        base.StartTurnMode();
        Network.Manager.SendToBoard(new CombatModePacket(this), this);
    }
    public override void EndTurnMode()
    {
        base.EndTurnMode();
        Network.Manager.SendToBoard(new CombatModePacket(this), this);
    }

    public void ToBytes(Stream stream){
        stream.WriteString(Name);
        stream.WriteUInt16((ushort)chatHistory.Count);
        foreach (string message in chatHistory)
        {
            stream.WriteLongString(message);
        }
        stream.WriteByte((byte)floors.Length);
        foreach (ServerFloor floor in floors.Cast<ServerFloor>())
        {
            floor.ToBytes(stream);
        }

        stream.WriteUInt16((ushort)entityCache.Count);
        foreach (Entity entity in entityCache.Values)
        {
            entity.ToBytes(stream);
        }
    }
}

using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using Server.Network;

namespace Server.Game;

public class ServerBoard : Board, ISerializable
{
    private HashSet<ComponentEvent> handledEvents = new();
    private readonly LinkedList<(SkillExecutor executor, ActionLayer layer)> actionQueue = [];

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
        InitializeEntities();
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
            if (entity.TryGetComponent<StatsContainer>(out var stats) && entity.ExistanceTicks % 20 == 0)
            {
                Manager.SendToBoard(new StatsUpdatePacket(stats), this);
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
        handledEvents.Clear();
    }

    public override void AddEntity(Entity entity)
    {
        base.AddEntity(entity);
        Network.Manager.SendToBoard(new EntityCreatePacket(this, entity), Name);
    }

    public override void HandleEvent(ComponentEvent e)
    {
        base.HandleEvent(e);
        if (handledEvents.Contains(e))
            return;
        handledEvents.Add(e);

        switch (e)
        {
            case TokenUpdateEvent tue:
                var token = (Token)tue.Component;
                Network.Manager.SendToBoard(new TokenUpdatePacket(token), this);
                if (tue.OldPosition != token.Position && token.IsPlaced)
                {
                    (token.Floor as ServerFloor)?.UpdateEntityCollisionGrid(token);
                }
                break;
            case FeatureAddedEvent fae:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Add((FeaturesContainer)fae.Component, fae.Feature),
                    this);
                break;
            case FeatureRemovedEvent fre:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Remove((FeaturesContainer)fre.Component, fre.Feature),
                    this
                );
                break;
            case FeatureEnabledEvent fee:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Enable((FeaturesContainer)fee.Component, fee.Feature),
                    this
                );
                break;
            case FeatureDisabledEvent fde:
                Network.Manager.SendToBoard(
                    FeatureUpdatePacket.Disable((FeaturesContainer)fde.Component, fde.Feature),
                    this
                );
                break;
            case SkillStartEvent sse:
                Network.Manager.SendToBoard(new SkillUpdatePacket((SkillExecutor)sse.Component, sse.SkillData), this);
                break;
            case SkillCancelEvent sce:
                Network.Manager.SendToBoard(new SkillRemovePacket((SkillExecutor)sce.Component, sce.SkillData.Id), this);
                break;
            case ActionLayerChangedEvent alce:
            {
                var exec = (SkillExecutor)alce.Component;
                Network.Manager.SendToBoard(new ActionLayerUpdatePacket(exec, alce.Layer), this);
                if (!TurnMode)
                    return;
                
                bool foundOld = false;
                
                var layer = alce.Layer;
                LinkedListNode<(SkillExecutor executor, ActionLayer layer)>? chosenPrev = null;
                var node = actionQueue.First;
                while (node != null)
                {
                    var tuple = node.Value;
                    if (!foundOld && tuple.layer.Name == layer.Name && tuple.executor == exec)
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
                    actionQueue.AddLast(new LinkedListNode<(SkillExecutor executor, ActionLayer layer)>((exec, layer)));
                else
                    actionQueue.AddBefore(chosenPrev, new LinkedListNode<(SkillExecutor executor, ActionLayer layer)>((exec, layer)));
                
                break;
            }
            case ActionLayerRemovedEvent alre:
            {
                Network.Manager.SendToBoard(new ActionLayerRemovePacket((SkillExecutor)alre.Component, alre.LayerName), this);
                break;
            }

        }
    }

    public override void RemoveEntity(Entity? entity)
    {
        if (entity != null)
            Network.Manager.SendToBoard(new EntityRemovePacket(entity), Name);

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

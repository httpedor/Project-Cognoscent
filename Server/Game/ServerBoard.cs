using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Rpg;
using Rpg.Inventory;
using Server.Network;

namespace Server.Game;

public class ServerBoard : Board, ISerializable
{
    private readonly LinkedList<(Creature executor, ActionLayer layer)> actionQueue = [];

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
            Entity entity = Entity.FromBytes(stream);
            AddEntity(entity);
        }
    }

    public override void PauseAt(uint tick)
    {
        base.PauseAt(tick);
        
        Manager.SendToBoard(new CombatModePacket(this), this);
    }

    public override void Tick()
    {
        foreach (Entity entity in entities)
        {
            entity.Tick();
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
    }

    [SuppressMessage("ReSharper", "RedundantNameQualifier")]
    public override void AddEntity(Entity entity)
    {
        base.AddEntity(entity);
        Network.Manager.SendToBoard(new EntityCreatePacket(this, entity), Name);
        entity.OnPositionChanged += (pos, _) => Manager.SendToBoard(new EntityPositionPacket(entity, pos), entity.Board.Name);
        entity.OnRotationChanged += (newRot, _) => Network.Manager.SendToBoard(new EntityRotationPacket(entity, newRot), Name);
        entity.OnDisplayChanged += newDisplay => Network.Manager.SendToBoard(new EntityMidiaPacket(entity, newDisplay), Name);
        entity.OnFeatureAdded += feat => Network.Manager.SendToBoard(FeatureUpdatePacket.Add(entity, feat), Name);
        entity.OnFeatureRemoved += feat => Network.Manager.SendToBoard(FeatureUpdatePacket.Remove(entity, feat), Name);
        entity.OnFeatureEnabled += feat => Network.Manager.SendToBoard(FeatureUpdatePacket.Enable(entity, feat), Name);
        entity.OnFeatureDisabled += feat => Network.Manager.SendToBoard(FeatureUpdatePacket.Disable(entity, feat), Name);
        entity.OnPositionChanged += (_, _) => (entity.Floor as ServerFloor)?.UpdateEntityCollisionGrid(entity);
        
        void addStatEvents(Stat stat)
        {
            stat.BaseValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatBasePacket(entity, stat.Id, newValue), Name);
            stat.ModifierUpdated += modifier => Network.Manager.SendToBoard(new EntityStatModifierPacket(entity, stat.Id, modifier), Name);
            stat.ModifierRemoved += modifier => Network.Manager.SendToBoard(new EntityStatModifierRemovePacket(entity, stat.Id, modifier.Id), Name);
            stat.MinValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatBasePacket(entity, stat.Id, newValue, Rpg.StatValueType.Min), Name);
            stat.MaxValueChanged += (newValue, _) => Network.Manager.SendToBoard(new EntityStatBasePacket(entity, stat.Id, newValue, Rpg.StatValueType.Max), Name);
        }
        foreach (Stat stat in entity.Stats)
        {
            addStatEvents(stat);
        }
        entity.OnStatCreated += stat => {
            addStatEvents(stat);
            Network.Manager.SendToBoard(new EntityStatCreatePacket(entity, stat), this);
        };
        
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

            void OnBodyPartChildAdd(BodyPart child)
            {
                child.OnChildAdded += grandChild =>
                {
                    Network.Manager.SendToBoard(new EntityBodyPartPacket(grandChild), this);
                    OnBodyPartChildAdd(grandChild);
                };

                child.OnChildRemoved += grandChild => {
                    Network.Manager.SendToBoard(new EntityBodyPartPacket(grandChild.Owner, grandChild.Path), Name);
                    grandChild.ClearEvents();
                };

                child.OnInjuryAdded += condition => {
                    Network.Manager.SendToBoard(new EntityBodyPartInjuryPacket(child, condition), Name);
                };
                child.OnInjuryRemoved += condition => {
                    Network.Manager.SendToBoard(new EntityBodyPartInjuryPacket(child, condition, true), Name);
                };
                child.OnEquipped += (equipment, slot) => {
                    Network.Manager.SendToBoard(new CreatureEquipItemPacket(child, slot, equipment.Item), this);
                };
                child.OnUnequipped += equipment => {
                    Network.Manager.SendToBoard(new CreatureEquipItemPacket(equipment.Item), this);
                };
                child.OnFeatureAdded += feature =>
                {
                
                };
            }
            foreach (BodyPart part in creature.Body.Parts)
            {
                OnBodyPartChildAdd(part);
            }
        }

    }

    public override void RemoveEntity(Entity? entity)
    {
        if (entity != null)
        {
            Network.Manager.SendToBoard(new EntityRemovePacket(entity), Name);
            entity.ClearEvents();
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

        stream.WriteUInt16((ushort)entities.Count);
        foreach (Entity entity in entities)
        {
            entity.ToBytes(stream);
        }
    }
}

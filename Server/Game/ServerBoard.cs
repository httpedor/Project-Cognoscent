using System.Diagnostics;
using System.Numerics;
using Rpg;
using Rpg.Entities;

namespace Server.Game;

public class ServerBoard : Board, ISerializable
{
    private void onBodyPartChildAdd(BodyPart child)
    {
        Network.Manager.SendToBoard(new EntityBodyPartPacket(child), Name);
        child.OnChildAdded += onBodyPartChildAdd;

        child.OnChildRemoved += (BodyPart child) => {
            Network.Manager.SendToBoard(new EntityBodyPartPacket(child.Owner, child.Path), Name);
            child.ClearEvents();
        };

        child.OnInjuryAdded += (Injury condition) => {
            Network.Manager.SendToBoard(new EntityBodyPartInjuryPacket(child, condition), Name);
        };
        child.OnInjuryRemoved += (Injury condition) => {
            Network.Manager.SendToBoard(new EntityBodyPartInjuryPacket(child, condition, true), Name);
        };
    }

    public ServerBoard(string name) : base(){
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

    public override void AddEntity(Entity entity)
    {
        base.AddEntity(entity);
        Network.Manager.SendToBoard(new EntityCreatePacket(this, entity), Name);
        entity.OnRotationChanged += (float newRot, float oldRot) => {
            Network.Manager.SendToBoard(new EntityRotationPacket(entity, newRot), Name);
        };
        if (entity is Creature creature)
        {
            /*creature.OnSkillStart += (Rpg.Skill action) => {
                Network.Manager.SendToBoard(new CreatureActionExecutePacket(action, creature), Name);
            };
            creature.OnSkillCancel += (Rpg.Skill action) => {
                Network.Manager.SendToBoard(new CreatureActionCancelPacket(creature, action), Name);
            };*/

            foreach (BodyPart part in creature.Body.Parts)
            {
                part.OnChildAdded += onBodyPartChildAdd;
                part.OnChildRemoved += (BodyPart child) => {
                    Network.Manager.SendToBoard(new EntityBodyPartPacket(creature, child.Path), Name);
                    part.ClearEvents();
                };

                part.OnInjuryAdded += (Injury condition) => {
                    Network.Manager.SendToBoard(new EntityBodyPartInjuryPacket(part, condition), Name);
                };
                part.OnInjuryRemoved += (Injury condition) => {
                    Network.Manager.SendToBoard(new EntityBodyPartInjuryPacket(part, condition, true), Name);
                };
            }
        }

        foreach (Stat stat in entity.Stats)
        {
            stat.BaseValueChanged += (float newValue, float oldValue) => {
                Network.Manager.SendToBoard(new EntityStatBasePacket(entity, stat.Id, newValue), Name);
            };
            stat.ModifierUpdated += (StatModifier modifier) => {
                Network.Manager.SendToBoard(new EntityStatModifierPacket(entity, stat.Id, modifier), Name);
            };
            stat.ModifierRemoved += (StatModifier modifier) => {
                Network.Manager.SendToBoard(new EntityStatModifierRemovePacket(entity, stat.Id, modifier.Id), Name);
            };
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

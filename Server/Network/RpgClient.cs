using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json.Nodes;
using Rpg;
using Rpg.Inventory;
using Server.Game;

namespace Server.Network;

public class RpgClient
{
    public bool IsGm => Username.Equals("httpedor");
    public Socket socket;

    public string Username
    {
        get;
        private set;
    }
    public DeviceType Device
    {
        get;
        private set;
    }
    public HashSet<string> LoadedBoards
    {
        get;
        private set;
    }
    public RpgClient(Socket client)
    {
        socket = client;
        Username = "";
        LoadedBoards = new HashSet<string>();
    }

    public void Disconnect()
    {
        Manager.Disconnect(Username);
    }

    public void HandlePacket(Packet packet)
    {
        switch (packet.Id){
            case ProtocolId.HANDSHAKE:
            {
                var loginPacket = (LoginPacket)packet;
                Username = loginPacket.Username;
                Device = loginPacket.Device;
                Manager.Disconnect(Username);
                Manager.Clients.Add(Username, this);
                Logger.Log(loginPacket.Username + " logged in with ip " + socket.RemoteEndPoint);
                foreach (string folder in Compendium.Folders)
                {
                    foreach (var entry in Compendium.GetEntryNames(folder))
                    {
                        var json = Compendium.GetEntryJsonOrNull(folder, entry);
                        if (json == null)
                            continue;
                        Send(CompendiumUpdatePacket.AddEntry(folder, entry, json));
                    }
                }
                if (Game.Game.GetBoards().Count == 0)
                    break;
                ServerBoard board = Game.Game.GetBoards()[0];

                SendBoard(board);
                Manager.SendToAll(new ChatPacket(board, Username + " joined the game."));
                break;
            }
            case ProtocolId.DISCONNECT:
            {
                Manager.Disconnect(Username, false);
                break;
            }
            case ProtocolId.COMBAT_MODE:
            {
                var turnModePacket = (CombatModePacket) packet;
                ServerBoard? board = Game.Game.GetBoard(turnModePacket.BoardName);
                if (board == null || !IsGm)
                    break;
                if (turnModePacket.CombatMode)
                    board.StartTurnMode();
                else
                    board.EndTurnMode();
                break;
            }
            case ProtocolId.CHAT:
            {
                var chatPacket = (ChatPacket) packet;
                ServerBoard? board = Game.Game.GetBoard(chatPacket.BoardName);
                if (board == null)
                    break;
                
                Manager.SendToAll(new ChatPacket(board, $"{Username}: {chatPacket.Message}"));
                break;
            }
            case ProtocolId.DOOR_INTERACT:
            {
                var dip = (DoorInteractPacket)packet;
                DoorEntity? door = dip.Door.Door;
                if (door == null)
                    return;
                if (!door.Locked)
                {
                    door.Closed = !door.Closed;
                    Manager.SendToBoard(new DoorUpdatePacket(door), door.Board.Name);
                }
                break;
            }
            case ProtocolId.DOOR_UPDATE:
            {
                var dup = (DoorUpdatePacket)packet;
                if (!IsGm)
                    return;
                dup.@ref.Door?.CopyFrom(dup.Door);
                if (dup.@ref.Door != null)
                    Manager.SendToOthersInBoard(dup, dup.@ref.Board, Username);
                break;
            }
            case ProtocolId.ENTITY_REMOVE:
            {
                var edp = (EntityRemovePacket) packet;

                if (!IsGm)
                    break;

                EntityRef entRef = edp.Ref;
                ServerBoard? board = Game.Game.GetBoard(entRef.Board);
                Entity? entity = board?.GetEntityById(entRef.Id);
                if (entity == null)
                    break;
                board!.RemoveEntity(entity);
                break;
            }
			case ProtocolId.ENTITY_MOVE:
			{
				var emp = (EntityMovePacket)packet;
				Entity? entity = emp.EntityRef.Entity;
				if (entity == null)
					break;
                if (entity is Creature c && entity.Board.TurnMode)
                    c.TargetPos = emp.Position;
                else
                {
                    var targetOBB = new OBB(emp.Position, (entity.Size.XY() / 2f) * 0.8f, entity.Rotation);
                    var doorLines = entity.Board.GetEntities<DoorEntity>().Select((door) => new Line(door.Bounds[0], door.Closed ? door.Bounds[1] : door.OpenBound2));
                    IEnumerable<Line> stairLines = new List<Line>();
                    foreach (Line wall in entity.Board.GetFloor(entity.FloorIndex).BroadPhaseOBB(targetOBB).Union(doorLines).Union(stairLines))
                    {
                        if (Geometry.OBBLineIntersection(targetOBB, wall, out Vector2 _))
                        {
                            return;
                        }
                    }
                    entity.Position = new Vector3(emp.Position.X, emp.Position.Y, entity.Position.Z);
                }
				break;
			}
            case ProtocolId.ENTITY_POSITION:
            {
                var epp = (EntityPositionPacket) packet;
                Entity? entity = epp.EntityRef.Entity;
                if (entity == null || !IsGm)
                    break;

                entity.Position = new Vector3(epp.Position.X, epp.Position.Y, entity.Position.Z);
                break;
            }
            case ProtocolId.ENTITY_ROTATION:
            {
                var erp = (EntityRotationPacket) packet;
                Entity? entity = erp.EntityRef.Entity;
                if (entity == null)
                    break;
                if (IsGm || (entity is Creature creature && creature.Owner.Equals(Username)))
                    entity.Rotation = erp.Rotation;
                break;
            }
            case ProtocolId.ENTITY_BODY_PART_INJURY:
            {
				var ebpcp = (EntityBodyPartInjuryPacket)packet;
				Creature? entity = ebpcp.CreatureRef.Creature;
				if (entity == null)
					break;

				BodyPart? part = entity.BodyRoot.GetChildByPath(ebpcp.Path);
				if (part == null)
					break;

				if (ebpcp.Remove)
					part.RemoveInjury(ebpcp.Injury);
				else
					part.AddInjury(ebpcp.Injury);
                break;
            }
            case ProtocolId.ENTITY_CREATE:
            {
                var ecp = (EntityCreatePacket)packet;
                if (!IsGm)
                    return;
                ServerBoard? board = Game.Game.GetBoard(ecp.BoardName);
                if (board == null)
                    return;

                board.AddEntity(ecp.Entity);
                break;
            }
            case ProtocolId.ENTITY_MIDIA:
            {
                var emp = (EntityMidiaPacket)packet;
                if (!IsGm)
                    return;
                var entity = emp.Ref.Entity;
                if (entity == null)
                    return;
                entity.Display = emp.Midia;
                break;
            }
            case ProtocolId.CREATURE_EQUIP_ITEM:
            {
                var cei = (CreatureEquipItemPacket)packet;
                BodyPart? bp = cei.BPRef.BodyPart;
                if (bp == null)
                    return;
                Item? item = cei.ItemRef.Item;
                if (item == null || !item.HasProperty<EquipmentProperty>())
                    return;
                if (cei.Equipped)
                {
                    bp.Equip(item, cei.Slot!);
                }
                else
                    bp.RemoveItem(item);
                break;
            }
            case ProtocolId.CREATURE_SKILL_UPDATE:
            {
                var csu = (CreatureSkillUpdatePacket)packet;
                Creature? creature = csu.CreatureRef.Creature;
                if (creature == null || (creature.Owner != Username && !IsGm))
                    return;
                
                if (creature.ActiveSkills.ContainsKey(csu.Data.Id))
                    creature.ActiveSkills[csu.Data.Id] = csu.Data;
                else
                {
                    ISkillSource? source = csu.Data.Source.SkillSource;
                    if (source == null)
                    {
                        Logger.LogWarning($"Execute Skill from {Username} doesn't have a valid SkillSource.");
                        return;
                    }
                    creature.ExecuteSkill(csu.Data.Skill, csu.Data.Arguments, source);
                }
                break;
            }
            case ProtocolId.CREATURE_SKILL_REMOVE:
            {
                var csr = (CreatureSkillRemovePacket)packet;
                Creature? creature = csr.CreatureRef.Creature;
                if (creature == null)
                    break;
                
                creature.CancelSkill(csr.SkillId);
                break;
            }
            case ProtocolId.CREATURE_ACTION_LAYER_REMOVE:
            {
                var calr = (ActionLayerRemovePacket)packet;
                Creature? creature = calr.CreatureRef.Creature;
                creature?.CancelActionLayer(calr.LayerId);
                break;
            }
            case ProtocolId.CREATURE_SKILLTREE_UPDATE:
            {
                var csu = (SkillTreeUpdatePacket)packet;
                var entry = csu.EntryRef.Entry;
                if (entry is not { CanEnable: true })
                    break;
                var creature = csu.EntryRef.Creature.Creature!;
                if (!IsGm && creature.Owner != Username)
                    break;
                
                if (csu.Enabled && !entry.Enabled)
                    entry.Enable();
                if (!csu.Enabled && entry.Enabled)
                    entry.Disable();
                Network.Manager.Broadcast(packet);
                break;
            }
            case ProtocolId.EXECUTE_COMMAND:
            {
                var ecp = (ExecuteCommandPacket)packet;
                foreach (string cmd in ecp.Commands)
                    Command.ExecuteCommand(this, cmd);
                break;
            }
            case ProtocolId.COMPENDIUM_UPDATE:
            {
                var drp = (CompendiumUpdatePacket)packet;
                if (!IsGm)
                    break;
                string type = drp.RegistryName;
                string name = drp.DataName;
                var data = drp.Json;
                if (drp.Remove)
                {
                    Compendium.RemoveEntry(type, name);
                    File.Delete("Data/" + type + "/" + name + ".json");
                }
                else
                {
                    Compendium.RegisterEntry(type, name, data!);
                    File.WriteAllText("Data/" + type + "/" + name + ".json", data!.ToJsonString());
                }
                
                break;
            }
            case ProtocolId.SHOW_MIDIA:
            {
                var smp = (ShowMidiaPacket)packet;
                if (!IsGm)
                    break;
                Manager.SendToBoard(smp, smp.Board.Name);
                break;
            }
            default:
                Logger.LogError("Unknown/unsupported packet type " + packet.Id);
                break;
        }
    }

    public void Send(Packet packet)
    {
        try
        {
            byte[] buffer = Packet.PreProcessPacket(packet);
            //Console.WriteLine("Sending " + buffer.Length + " bytes(Id:  " + packet.Id +") to " + Username);
            socket.Send(buffer);
        } catch (Exception e)
        {
            Logger.LogError("Failed to send packet to " + Username + ": " + e);
        }
    }

    public void SendBoard(ServerBoard board)
    {
        Send(new BoardAddPacket(board));
        foreach (Entity e in board.GetEntities())
            Send(new EntityCreatePacket(board, e));
        for (int i = 0; i < board.GetFloorCount(); i++)
            Send(new FloorImagePacket(board.Name, i, board.GetFloor(i).GetMidia()));
        
        LoadedBoards.Add(board.Name);
    }
}

using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using System.Text.Json;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Components.Inventory;
using Server.Game;

namespace Server.Network;

public class RpgClient
{
    public bool IsGm => Username.Equals("httpedor");
    public Either<Socket, WebSocket> socket;

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

    public bool Connected => socket.Left?.Connected ?? socket.Right?.State == WebSocketState.Open;
    public string IpAddress
    {
        get
        {
            if (socket.IsLeft)
            {
                return ((System.Net.IPEndPoint)socket.Left!.RemoteEndPoint!).Address.ToString();
            }
            else
            {
                return "WebSocketClient";
            }
        }
    }

    public RpgClient(Socket client)
    {
        socket = client;
        Username = "";
        LoadedBoards = new HashSet<string>();
    }
    public RpgClient(WebSocket webSocket)
    {
        socket = webSocket;
        Username = "";
        LoadedBoards = new HashSet<string>();
    }

    public void Disconnect(bool sendDisconnect = true)
    {
        if (sendDisconnect)
        {
            try
            {
                Send(new DisconnectPacket());
            }
            catch (Exception) { }
        }
        if (socket.IsLeft)
        {
            try
            {
                socket.Left.Disconnect(false);
                socket.Left.Shutdown(SocketShutdown.Both);
                socket.Left.Close();
            } catch (Exception) {}
        }
        else
        {
            socket.Right.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
        }
        if (Username != null)
        {
            Manager.Clients.Remove(Username);
            Logger.Log(Username + " disconnected");
        }
    }

    public bool OwnsEntity(Entity? entity)
    {
        return IsGm || (entity?.Owner?.Equals(Username) ?? false);
    }

    public void HandlePacket(Packet packet)
    {
        switch (packet.Id){
            case ProtocolId.HANDSHAKE:
            {
                var loginPacket = (LoginPacket)packet;
                Username = loginPacket.Username;
                Device = loginPacket.Device;
                if (Username == "" || Username == null)
                {
                    Disconnect();
                    return;
                }
                Manager.GetClient(Username)?.Disconnect();
                Manager.Clients.Add(Username, this);
                Logger.Log(loginPacket.Username + " logged in: " + IpAddress);
                foreach (string folder in Compendium.Folders)
                {
                    foreach (var entry in Compendium.GetEntryNames(folder))
                    {
                        var json = Compendium.GetEntryJsonOrNull(folder, entry);
                        if (!json.HasValue)
                            continue;
                        Send(CompendiumUpdatePacket.AddEntry(folder, entry, json.Value));
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
                var door = dip.Door.Component;
                if (door == null)
                    return;
                if (!door.Locked)
                {
                    door.Closed = !door.Closed;
                    Manager.SendToBoard(new DoorUpdatePacket(door), door.Board!.Name);
                }
                break;
            }
            case ProtocolId.DOOR_UPDATE:
            {
                var dup = (DoorUpdatePacket)packet;
                if (!IsGm)
                    return;
                var door = dup.@ref.Component;
                if (door == null)
                    return;
                door.CopyFrom(dup.Door);
                Manager.SendToOthersInBoard(dup, door.Board.Name, Username);
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
			case ProtocolId.TOKEN_MOVE:
			{
				var emp = (TokenMovePacket)packet;
				var token = emp.TokenRef.Component;
				if (token == null)
					break;
                if (!OwnsEntity(token))
                    break;
                if (token.Board?.TurnMode ?? false)
                {
                    //token.TargetPos = emp.Position;
                    break;
                }
                token.Rotation = emp.NewRotation;
                var targetOBB = new OBB(emp.NewPos, (token.Size.XY() / 2f) * 0.8f, token.Rotation);
                var doorLines = token.Board!.GetComponents<Door>().Select((door) => new Line(door.Bounds[0], door.Closed ? door.Bounds[1] : door.OpenBound2));
                IEnumerable<Line> stairLines = new List<Line>();
                foreach (Line wall in token.Board.GetFloor(token.FloorIndex).BroadPhaseOBB(targetOBB).Union(doorLines).Union(stairLines))
                {
                    if (Geometry.OBBLineIntersection(targetOBB, wall, out Vector2 _))
                    {
                        return;
                    }
                }
                token.Position = new Vector3(emp.NewPos.X, emp.NewPos.Y, token.Position.Z);
				break;
			}
            case ProtocolId.TOKEN_UPDATE:
            {
                var emp = (TokenUpdatePacket)packet;
                if (!IsGm)
                    break;

                var token = emp.TokenRef.Component;
                if (token == null)
                    break;
                token.CopyFrom(emp.NewToken);
                Manager.SendToOthersInBoard(emp, token.Board!.Name, Username);
                break;
            }
            case ProtocolId.ENTITY_BODY_PART_INJURY:
            {
				var ebpcp = (EntityBodyPartInjuryPacket)packet;
                if (!IsGm)
                    break;
				BodyPart? part = ebpcp.BpRef.Component;
				if (part == null)
					break;
                switch (ebpcp.Type)
                {
                    case EntityBodyPartInjuryPacket.InjuryPacketType.ADD:
                        part.AddInjury(ebpcp.Injury);
                        break;
                    case EntityBodyPartInjuryPacket.InjuryPacketType.REMOVE:
                        part.RemoveInjury(ebpcp.Injury);
                        break;
                    case EntityBodyPartInjuryPacket.InjuryPacketType.REPLACE:
                        if (ebpcp.OldInjury.HasValue)
                            part.ChangeInjury(ebpcp.OldInjury.Value, ebpcp.Injury);
                        break;
                }
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
                board.AddEntities(ecp.Entities, true);
                Manager.SendToBoard(packet, board.Name);
                break;
            }
            case ProtocolId.BODY_EQUIP_ITEM:
            {
                var cei = (BodyEquipItemPacket)packet;
                BodyPart? bp = cei.BPRef.Component;
                if (bp == null)
                    return;
                if (!OwnsEntity(bp.OwnerEntity))
                    return;
                var equipment = cei.ItemRef.Component;
                if (equipment == null)
                    return;

                if (cei.Equipped)
                {
                    bp.Equip(equipment.Item, cei.Slot!);
                }
                else
                    bp.RemoveItem(equipment.Item);
                break;
            }
            case ProtocolId.SKILL_UPDATE:
            {
                var csu = (SkillUpdatePacket)packet;
                var exec = csu.Ref.Component;
                if (exec == null || !OwnsEntity(exec.Entity))
                    return;
                
                if (exec.ActiveSkills.ContainsKey(csu.Data.Id))
                    exec.ActiveSkills[csu.Data.Id] = csu.Data;
                else
                {
                    exec.ExecuteSkill(csu.Data.Skill, csu.Data.Arguments);
                }
                break;
            }
            case ProtocolId.SKILL_REMOVE:
            {
                var csr = (SkillRemovePacket)packet;
                var exec = csr.Ref.Component;
                if (exec == null || !OwnsEntity(exec.Entity))
                    break;
                
                exec.CancelSkill(csr.SkillId);
                break;
            }
            case ProtocolId.CREATURE_ACTION_LAYER_REMOVE:
            {
                var calr = (ActionLayerRemovePacket)packet;
                var exec = calr.Ref.Component;
                if (exec == null || !OwnsEntity(exec.Entity))
                    break;
                exec.CancelActionLayer(calr.LayerId);
                break;
            }
            case ProtocolId.CREATURE_SKILLTREE_UPDATE:
            {
                var csu = (SkillTreeUpdatePacket)packet;
                var entry = csu.EntryRef.Entry;
                if (entry is not { CanEnable: true })
                    break;
                var skillTree = csu.EntryRef.SkillTree.Component;
                if (skillTree == null || !OwnsEntity(skillTree?.Entity))
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
                    if (!data.HasValue)
                    {
                        Logger.LogError("No data provided for compendium entry " + type + "/" + name);
                        break;
                    }
                    Compendium.RegisterEntry(type, name, data.Value);
                    File.WriteAllText("Data/" + type + "/" + name + ".json", data.Value.ToString());
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
            case ProtocolId.PRIVATE_MESSAGE:
            {
                var pmp = (PrivateMessagePacket)packet;
                var target = pmp.Recipient?.Entity;
                Manager.SendToSome(pmp, (client) => (target != null && client.Username == target.Owner) || client.IsGm);
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
            if (socket.IsLeft)
            {
                byte[] buffer = Packet.PreProcessPacket(packet);
                //Console.WriteLine("Sending " + buffer.Length + " bytes(Id:  " + packet.Id +") to " + Username);
                try
                {
                    socket.Left.Send(buffer);
                } catch (SocketException)
                {
                    Logger.LogError("Failed to send packet to " + Username + ": Socket error, disconnecting client.");
                    Disconnect(false);
                }
            }
            else
            {
                JsonSerializerOptions options = new()
                {
                    IncludeFields = true,
                    IgnoreReadOnlyFields = false,
                    IgnoreReadOnlyProperties = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string json = JsonSerializer.Serialize(packet, packet.GetType(), options);
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(json);
                socket.Right.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }            
        } catch (Exception e)
        {
            Logger.LogError("Failed to send packet to " + Username + ": " + e);
        }
    }

    public void SendBoard(ServerBoard board)
    {
        Send(new BoardAddPacket(board));
        LoadedBoards.Add(board.Name);
        if (Device == DeviceType.MOBILE)
            return;
        for (int i = 0; i < board.GetFloorCount(); i++)
            Send(new FloorImagePacket(board.Name, i, board.GetFloor(i).GetMidia()));
        
    }
}

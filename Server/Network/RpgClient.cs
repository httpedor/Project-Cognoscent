using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Numerics;
using Rpg;
using Rpg.Entities;
using Server.Game;

namespace Server.Network;

public class RpgClient
{
    public bool IsGm{
        get => Username.Equals("httpedor");
    }
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
                LoginPacket loginPacket = packet as LoginPacket;
                Username = loginPacket.Username;
                Device = loginPacket.Device;
                Manager.Clients.Remove(Username);
                Manager.Disconnect(Username);
                Manager.Clients.Add(Username, this);
                Console.WriteLine(loginPacket.Username + " logged in with ip " + socket.RemoteEndPoint);
                if (Game.Game.GetBoards().Count == 0)
                    break;
                var board = Game.Game.GetBoards()[0];

                SendBoard(board);
                Manager.SendToAll(new ChatPacket(board, Username + " joined the game."));
                break;
            }
            case ProtocolId.DISCONNECT:
            {
                Manager.Disconnect(Username, false);
                break;
            }
            case ProtocolId.TURN_MODE:
            {
                TurnModePacket turnModePacket = (TurnModePacket) packet;
                ServerBoard? board = Game.Game.GetBoard(turnModePacket.BoardName);
                if (board == null || !IsGm)
                    break;
                board.CombatMode = turnModePacket.TurnMode;
                Manager.SendToAll(new TurnModePacket(board, turnModePacket.TurnMode));
                break;
            }
            case ProtocolId.CHAT:
            {
                ChatPacket chatPacket = (ChatPacket) packet;
                ServerBoard? board = Game.Game.GetBoard(chatPacket.BoardName);
                if (board == null)
                    break;
                
                Manager.SendToAll(new ChatPacket(board, $"{Username}: {chatPacket.Message}"));
                break;
            }
            case ProtocolId.ENTITY_REMOVE:
            {
                EntityRemovePacket edp = (EntityRemovePacket) packet;

                if (!IsGm)
                    break;

                var entRef = edp.Ref;
                ServerBoard? board = Game.Game.GetBoard(entRef.Board);
                if (board == null)
                    break;
                Entity? entity = board.GetEntityById(entRef.Id);
                if (entity == null)
                    break;
                board.RemoveEntity(entity);
                break;
            }
			case ProtocolId.ENTITY_MOVE:
			{
				EntityMovePacket emp = (EntityMovePacket)packet;
				var entity = emp.EntityRef.Entity;
				if (entity == null)
					break;
                var targetOBB = new OBB(emp.Position, (entity.Size.XY() / 2f) * 0.8f, entity.Rotation);
                foreach (var wall in entity.Board.GetFloor(entity.FloorIndex).BroadPhaseOBB(targetOBB))
                {
                    if (Geometry.OBBLineIntersection(targetOBB, wall, out var _))
                    {
                        return;
                    }
                }
                entity.Position = new Vector3(emp.Position.X, emp.Position.Y, entity.Position.Z);
                Manager.SendToBoard(new EntityPositionPacket(entity, entity.Position), entity.Board.Name);
				break;
			}
            case ProtocolId.ENTITY_POSITION:
            {
                EntityPositionPacket epp = (EntityPositionPacket) packet;
                Entity? entity = epp.EntityRef.Entity;
                if (entity == null || !IsGm)
                    break;

                entity.Position = new Vector3(epp.Position.X, epp.Position.Y, entity.Position.Z);
                Manager.SendToBoard(new EntityPositionPacket(entity, entity.Position), entity.Board.Name);
                break;
            }
            case ProtocolId.ENTITY_ROTATION:
            {
                EntityRotationPacket erp = (EntityRotationPacket) packet;
                Entity? entity = erp.EntityRef.Entity;
                if (entity == null)
                    break;
                if (IsGm || (entity is Creature creature && creature.Owner.Equals(Username)))
                    entity.Rotation = erp.Rotation;
                break;
            }
            case ProtocolId.ENTITY_BODY_PART_INJURY:
            {
				EntityBodyPartInjuryPacket ebpcp = (EntityBodyPartInjuryPacket)packet;
				var entity = ebpcp.CreatureRef.Creature;
				if (entity == null)
					break;

				var part = entity.BodyRoot.GetChildByPath(ebpcp.Path);
				if (part == null)
					break;

				if (ebpcp.Remove)
					part.RemoveInjury(ebpcp.Injury);
				else
					part.AddInjury(ebpcp.Injury);
                break;
            }
            default:
                Console.WriteLine("Unknown packet type " + packet.Id);
                break;
            /*case PacketType.CompendiumQuery:
                CompendiumQueryRequestPacket cqr = (CompendiumQueryRequestPacket) packet;
                List<CreatureModel> results = new List<CreatureModel>();
                foreach (CreatureModel model in CreatureModel.Models.Values)
                {
                    if (model.name.ToLower().Contains(cqr.query.ToLower()) || model.id.ToLower().Contains(cqr.query.ToLower()))
                        results.Add(model);
                    
                }
                Send(new CompendiumQueryResponsePacket(results));
                break;*/
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
            Console.WriteLine("Failed to send packet to " + Username + ": " + e);
        }
    }

    public void SendBoard(ServerBoard board)
    {
        Send(new BoardAddPacket(board));
        foreach (Entity e in board.GetEntities())
            Send(new EntityCreatePacket(board, e));
        for (int i = 0; i < board.GetFloorCount(); i++)
            Send(new FloorImagePacket(board.Name, i, board.GetFloor(i).GetImage()));
        
        LoadedBoards.Add(board.Name);
    }
}

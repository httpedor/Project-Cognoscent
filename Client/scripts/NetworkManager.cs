using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using Rpg;
using Rpg.Entities;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
[GlobalClass]
public partial class NetworkManager : Node
{
	private static NetworkManager instance;
	public static NetworkManager Instance{
		get{
			return instance;
		}
	}
	public NetworkManager(){
		instance = this;
	}

	private StreamPeerTcp? stream = null;
	private MemoryStream buffer = new MemoryStream();
	private uint expectedBufferLength = 0;
	private bool shouldHandshake = true;

	public Error ConnectToHost(string ip, int port){
		stream = new StreamPeerTcp();
		Error err = stream.ConnectToHost(ip, port);

		return err; 
	}

	public void Disconnect(bool sendDisconnect = true){
		if (sendDisconnect){
            try
            {
				SendPacket(new DisconnectPacket());
			} catch (Exception) { }
		}
		if (stream != null)
			stream.DisconnectFromHost();
	}

    public override void _Ready()
	{
		ClientSidedLogic.Init();
	}
	public override void _Process(double delta)
	{
		if (stream == null)
			return;
		stream.Poll();
		if (shouldHandshake && stream.GetStatus() == StreamPeerTcp.Status.Connected){
			OnConnect();
			shouldHandshake = false;
		}
		else if (stream.GetStatus() != StreamPeerTcp.Status.Connected && stream.GetStatus() != StreamPeerTcp.Status.Connecting){
			GD.PrintErr("Error connecting to host, status: " + stream.GetStatus());
			stream = null;
			buffer.SetLength(0);
			shouldHandshake = true;
			GameManager.Instance.ClearBoards();
			GameManager.Instance.ShowMenu();
			return;
		}

		int available = stream.GetAvailableBytes();
		if (available > 0){
			var data = stream.GetData(available);
			ProcessBytes(data[1].As<Godot.Collections.Array<byte>>().ToArray());
		}
	}
	private void OnConnect(){
		GD.Print("Connected to host");

		SendPacket(new LoginPacket(GameManager.Instance.Username, DeviceType.DESKTOP));
	}

	public void SendPacket(Packet packet){
		byte[] packetBuffer = Packet.PreProcessPacket(packet);

		Error err = stream.PutData(packetBuffer);
		if (err != Error.Ok)
			GD.PrintErr(err);
	}

	private void ProcessPacket(Packet packet){
		//GD.Print("Processing packet with id " + packet.Id);
        switch (packet.Id){
			case ProtocolId.DISCONNECT:
			{
				GD.Print("Received disconnect packet");
				Disconnect(false);
				break;
			}
			case ProtocolId.CHAT:
			{
				ChatPacket chatPacket = (ChatPacket)packet;
				var board = GameManager.Instance.GetBoard(chatPacket.BoardName);
				if (board == null)
					break;
				board.AddChatMessage(chatPacket.Message);
				if (GameManager.Instance.CurrentBoard == board)
					ChatControl.Instance.AddMessage(chatPacket.Message);
				break;
			}
            case ProtocolId.BOARD_ADD:
			{
                BoardAddPacket boardState = (BoardAddPacket)packet;
                ClientBoard board = boardState.Board as ClientBoard;
                GD.Print("Received board with name " + board.Name + ", " + board.GetFloorCount() + " floors, " + board.GetEntities().Count + " entities");
				GameManager.Instance.AddBoard(board);
				break;
			}
			case ProtocolId.BOARD_REMOVE:
			{
				BoardRemovePacket brp = (BoardRemovePacket)packet;
				GD.Print("Removing board with name: " + brp.Name);
				GameManager.Instance.RemoveBoard(brp.Name);
				break;
			}
			case ProtocolId.FLOOR_IMAGE:
			{
				FloorImagePacket fip = (FloorImagePacket)packet;
				GD.Print("Received floor image for " + fip.BoardName + " floor " + fip.FloorIndex + " with " + fip.Data.Length + " bytes");
				var board = GameManager.Instance.GetBoard(fip.BoardName);
				if (board == null){
					GD.PrintErr("Board not found for floor image packet");
					break;
				}
				var floor = board.GetFloor(fip.FloorIndex);
				floor.SetImage(fip.Data);
				break;
			}
			case ProtocolId.ENTITY_CREATE:
			{

				EntityCreatePacket ecp = (EntityCreatePacket)packet;
				GD.Print("Received entity packet for " + ecp.BoardName + " with id " + ecp.Entity.Id);
				var board = GameManager.Instance.GetBoard(ecp.BoardName);
				if (board == null)
					break;
				board.AddEntity(ecp.Entity);
				break;
			}
			case ProtocolId.ENTITY_REMOVE:
			{
				EntityRemovePacket edp = (EntityRemovePacket)packet;
				var entRef = edp.Ref;
				var board = GameManager.Instance.GetBoard(entRef.Board);
				if (board == null)
					break;
				var entity = board.GetEntityById(entRef.Id);
				if (entity == null)
					break;
				board.RemoveEntity(entity);
				break;
			}
			case ProtocolId.ENTITY_POSITION:
			{
				EntityPositionPacket epp = (EntityPositionPacket)packet;
				var entity = epp.EntityRef.Entity;
				if (entity == null)
					break;
				entity.Position = epp.Position;
				break;
			}
			case ProtocolId.ENTITY_ROTATION:
			{
				EntityRotationPacket erp = (EntityRotationPacket)packet;
				var entity = erp.EntityRef.Entity;
				if (entity == null)
					break;
				entity.Rotation = erp.Rotation;
				break;
			}
			case ProtocolId.TURN_MODE:
			{
				TurnModePacket turnModePacket = (TurnModePacket)packet;
				var board = GameManager.Instance.GetBoard(turnModePacket.BoardName);
				if (board == null)
					break;
				board.CombatMode = turnModePacket.TurnMode;
				break;
			}
			case ProtocolId.ENTITY_BODY_PART:
			{
				EntityBodyPartPacket ebpp = (EntityBodyPartPacket)packet;
				var entity = ebpp.CreatureRef.Creature;
				if (entity == null)
					break;

				var part = ebpp.Part;
				var parent = entity.BodyRoot.GetChildByPath(ebpp.Path[..ebpp.Path.LastIndexOf('/')]);
				if (parent == null)
					return;

				parent.RemoveChild(ebpp.Path[(ebpp.Path.LastIndexOf('/') + 1)..]);
				if (part != null)
					parent.AddChild(part);
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
			case ProtocolId.ENTITY_STAT_BASE:
			{
				EntityStatBasePacket esup = (EntityStatBasePacket)packet;
				var entity = esup.EntityRef.Entity;
				if (entity == null)
					break;
				var stat = entity.GetStat(esup.StatId);
				if (stat == null)
					break;

				stat.BaseValue = esup.Value;
				break;
			}
			case ProtocolId.ENTITY_STAT_MODIFIER_UPDATE:
			{
				EntityStatModifierPacket esmp = (EntityStatModifierPacket)packet;
				var entity = esmp.EntityRef.Entity;
				if (entity == null)
					break;

				entity.GetStat(esmp.StatId)?.SetModifier(esmp.Modifier);
				break;
			}
			case ProtocolId.ENTITY_STAT_MODIFIER_REMOVE:
			{
				EntityStatModifierRemovePacket esmrp = (EntityStatModifierRemovePacket)packet;
				var entity = esmrp.EntityRef.Entity;
				if (entity == null)
					break;
				
				entity.GetStat(esmrp.StatId)?.RemoveModifier(esmrp.ModifierId);
				break;
			}
            default:
                GD.Print("Unknown packet with id " + packet.Id);
                break;
        }
	}
	private void ProcessBytes(byte[] data){
		buffer.Seek(0, SeekOrigin.End);
		buffer.Write(data);

		while ((expectedBufferLength != 0 && buffer.Length >= expectedBufferLength) || (expectedBufferLength == 0 && buffer.Length >= 5)){
			if (expectedBufferLength == 0){
				buffer.Seek(0, SeekOrigin.Begin);
				UInt32 length = buffer.ReadUInt32();
				byte id = (byte)buffer.ReadByte();
				expectedBufferLength = length;
			}

			if (buffer.Length >= expectedBufferLength){
				//Read the packet
				buffer.Seek(0, SeekOrigin.Begin);
				byte[] packet = new byte[expectedBufferLength];
				buffer.Read(packet, 0, (int)expectedBufferLength);
				
				//Remove the packet from the buffer
				byte[] under = buffer.GetBuffer();
				Array.Copy(under, expectedBufferLength, under, 0, under.Length - expectedBufferLength);
				buffer.SetLength(buffer.Length - expectedBufferLength);

				//Process the packet
				try
				{
					ProcessPacket(Packet.ReadPacket(packet));
				} catch (Exception e){
					GD.PrintErr("Error processing packet: " + e);
				}

				//Reset the expected buffer length
				expectedBufferLength = 0;
			}
		}
	}


}

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
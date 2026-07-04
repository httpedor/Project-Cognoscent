using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Components.Inventory;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;
using Exception = System.Exception;

namespace TTRpgClient.scripts;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
[GlobalClass]
public partial class NetworkManager : Node
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private static NetworkManager instance;
    public static NetworkManager Instance => instance;

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

		stream?.DisconnectFromHost();
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
			Compendium.Clear();
			GameManager.Instance.ClearBoards();
			GameManager.Instance.ShowMenu();
			return;
		}

		int available = stream.GetAvailableBytes();
		if (available > 0){
			Godot.Collections.Array? data = stream.GetData(available);
			ProcessBytes(data[1].As<Godot.Collections.Array<byte>>().ToArray());
		}
	}
	private void OnConnect(){
		GD.Print("Connected to host");

		SendPacket(new LoginPacket(GameManager.Username, DeviceType.DESKTOP));
	}

	public void SendPacket(Packet packet){
		if (stream == null)
        {
			GD.PrintErr("Cannot send packet before connection is established.");
			return;
        }
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
				var chatPacket = (ChatPacket)packet;
				ClientBoard? board = GameManager.Instance.GetBoard(chatPacket.BoardName);
				if (board == null)
					break;
				board.AddChatMessage(chatPacket.Message);
				if (GameManager.Instance.CurrentBoard == board)
					ChatControl.Instance.AddMessage(chatPacket.Message);
				break;
			}
            case ProtocolId.BOARD_ADD:
			{
                var boardState = (BoardAddPacket)packet;
                var board = boardState.Board as ClientBoard;
				if (board == null)
				{
					GD.PrintErr("Received board add packet with invalid board");
					break;
				}
				GameManager.Instance.AddBoard(board);
				break;
			}
			case ProtocolId.BOARD_REMOVE:
			{
				var brp = (BoardRemovePacket)packet;
				GD.Print("Removing board with name: " + brp.Name);
				GameManager.Instance.RemoveBoard(brp.Name);
				break;
			}
			case ProtocolId.FLOOR_IMAGE:
			{
				var fip = (FloorImagePacket)packet;
				ClientBoard? board = GameManager.Instance.GetBoard(fip.BoardName);
				if (board == null){
					GD.PrintErr("Board not found for floor image packet");
					break;
				}
				ClientFloor floor = board.GetFloor(fip.FloorIndex);
				floor?.SetMidia(fip.Data);
				break;
			}
			case ProtocolId.DOOR_UPDATE:
			{
				var dup = (DoorUpdatePacket)packet;
				dup.@ref.Component?.CopyFrom(dup.Door);
				break;
			}
			case ProtocolId.ENTITY_CREATE:
			{

				var ecp = (EntityCreatePacket)packet;
				GD.Print("Received " + ecp.Entities.Length + " entities for board " + ecp.BoardName);
				ClientBoard? board = GameManager.Instance.GetBoard(ecp.BoardName);
				board?.AddEntities(ecp.Entities, true);
				break;
			}
			case ProtocolId.TOKEN_UPDATE:
			{
				var tup = (TokenUpdatePacket)packet;
				var token = tup.TokenRef.Component;
				if (token == null)
				{
					GD.PushWarning("Received token update packet for unknown token: " + tup.TokenRef.EntityRef.Id);
					break;
				}
				token.CopyFrom(tup.NewToken);
				break;
			}
			case ProtocolId.ENTITY_REMOVE:
			{
				var edp = (EntityRemovePacket)packet;
				EntityRef entRef = edp.Ref;
				ClientBoard? board = GameManager.Instance.GetBoard(entRef.Board);
				Entity? entity = board?.GetEntityById(entRef.Id);
				if (entity == null || board == null)
					break;
				board.RemoveEntity(entity);
				break;
			}
			case ProtocolId.COMBAT_MODE:
			{
				var turnModePacket = (CombatModePacket)packet;
				ClientBoard? board = GameManager.Instance.GetBoard(turnModePacket.BoardName);
				if (board == null)
					break;
				if (turnModePacket.CombatMode)
					board.StartTurnMode();
				else
					board.EndTurnMode();
				int diff = (int)board.CurrentTick - (int)turnModePacket.Tick;
				if (Math.Abs(diff) > 50)
					GD.PushWarning("Client was " + diff + " ticks desynced to server");
				board.CurrentTick = turnModePacket.Tick;
				break;
			}
			case ProtocolId.BODY_MOVEMENT_UPDATE:
			{
				var bmu = (BodyMovementUpdatePacket)packet;
				var body = bmu.BodyRef.Component;
				if (body == null)
					break;
				body.MovementIntensity = bmu.NewMovementIntensity;
				break;
			}
			case ProtocolId.BODY_STABILITY_UPDATE:
			{
				var bsu = (BodyStabilityUpdatePacket)packet;
				var body = bsu.BodyRef.Component;
				if (body == null)
					break;
				body.Stability = bsu.NewStability;
				break;
			}
			case ProtocolId.BODY_POSTURE_UPDATE:
			{
				var bpu = (BodyPostureUpdatePacket)packet;
				var body = bpu.BodyRef.Component;
				if (body == null)
					break;
				body.ChangePosture(bpu.NewPosture);
				break;
			}
			case ProtocolId.ENTITY_BODY_PART_INJURY:
			{
				var ebpcp = (EntityBodyPartInjuryPacket)packet;

				BodyPart? part = ebpcp.BpRef.Component;
				if (part == null)
					break;

				var layer = part.FirstLayer;
				switch (ebpcp.Type)
				{
					case EntityBodyPartInjuryPacket.InjuryPacketType.ADD:
						layer.AddInjury(ebpcp.Injury);
						break;
					case EntityBodyPartInjuryPacket.InjuryPacketType.REMOVE:
						layer.RemoveInjury(ebpcp.Injury);
						break;
					case EntityBodyPartInjuryPacket.InjuryPacketType.REPLACE:
						layer.RemoveInjury(ebpcp.OldInjury!.Value);
						layer.AddInjury(ebpcp.Injury);
						break;
				}
				break;
			}
			case ProtocolId.STAT_UPDATE:
			{
				var sup = (StatsUpdatePacket)packet;
				var stats = sup.StatsRef.Component;
				if (stats == null)
					break;

				foreach (var incoming in sup.Stats)
				{
					Stat? existing = stats.GetStat(incoming.Id);
					if (existing == null)
					{
						stats.CreateStat(incoming);
						continue;
					}

					// Update values
					existing.BaseValue = incoming.BaseValue;
					existing.MinValue = incoming.MinValue;
					existing.MaxValue = incoming.MaxValue;

					existing.ClearModifiers();

					// Sync modifiers: add/update incoming
					foreach (var mod in incoming.GetModifiers())
						existing.SetModifier(mod);
				}
				break;
			}
			case ProtocolId.FEATURE_UPDATE:
			{
				var efu = (FeatureUpdatePacket)packet;
				var container = efu.ContainerRef.Component;
				if (container == null)
					break;

				switch (efu.UpdateType)
				{
					case FeatureUpdatePacket.FeatureUpdateType.ADD:
					{
						container.AddFeature(efu.Feature!);
						break;
					}
					case FeatureUpdatePacket.FeatureUpdateType.REMOVE:
					{
						container.RemoveFeature(efu.FeatureId!);
						break;
					}
					case FeatureUpdatePacket.FeatureUpdateType.ENABLE:
					{
						container.EnableFeature(efu.FeatureId!);
						break;
					}
					case FeatureUpdatePacket.FeatureUpdateType.DISABLE:
					{
						container.DisableFeature(efu.FeatureId!);
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}
				break;
			}
            case ProtocolId.BODY_EQUIP_ITEM:
            {
                var cei = (BodyEquipItemPacket)packet;
                BodyPart? bp = cei.BPRef.Component;
                if (bp == null)
                    return;

                var equipment = cei.ItemRef.Component;
                if (equipment == null)
                    return;

                if (cei.Equipped)
                    bp.Equip(equipment.Item, cei.Slot!);
                else
                    bp.RemoveItem(equipment.Item);
                break;
            }
			case ProtocolId.SKILL_UPDATE:
			{
				var csu = (SkillUpdatePacket)packet;
				var exec = csu.Ref.Component;
				if (exec == null)
					break;
				exec.ActiveSkills[csu.Data.Id] = csu.Data;
				break;
			}
			case ProtocolId.SKILL_REMOVE:
			{
				var csr = (SkillRemovePacket)packet;
				var exec = csr.Ref.Component;
				exec?.CancelSkill(csr.SkillId);
				break;
			}
			case ProtocolId.CREATURE_ACTION_LAYER_UPDATE:
			{
				var calup = (ActionLayerUpdatePacket)packet;
				var exec = calup.Ref.Component;
				if (exec == null)
					break;
				if (exec.GetActionLayer(calup.Layer.Name) != null)
					exec.UpdateActionLayer(calup.Layer);
				else
					exec.TriggerActionLayer(calup.Layer);
				break;
			}
			case ProtocolId.CREATURE_ACTION_LAYER_REMOVE:
			{
				var calrp = (ActionLayerRemovePacket)packet;
				var exec = calrp.Ref.Component;
				exec?.CancelActionLayer(calrp.LayerId);
				break;
			}
			case ProtocolId.CREATURE_SKILLTREE_UPDATE:
			{
				var csu = (SkillTreeUpdatePacket)packet;
				var entry = csu.EntryRef.Entry;
				if (entry == null)
					break;
                
				if (csu.Enabled && !entry.Enabled)
					entry.Enable();
				if (!csu.Enabled && entry.Enabled)
					entry.Disable();
				break;
			}
			case ProtocolId.EXECUTE_COMMAND:
			{
				var ecp = (ExecuteCommandPacket)packet;
				foreach (string cmd in ecp.Commands)
					GameManager.Instance.ExecuteCommand(cmd);
				break;
			}
            case ProtocolId.COMPENDIUM_UPDATE:
            {
                var drp = (CompendiumUpdatePacket)packet;
                string type = drp.RegistryName;
                string name = drp.DataName;
                var data = drp.Json;
                if (drp.Remove)
                    Compendium.RemoveEntry(type, name);
                else
                {
					Compendium.RegisterEntry(type, name, data!.Value);
	                GD.Print("Registered " + type + "/" + name);
                }
                
                break;
            }
            case ProtocolId.SHOW_MIDIA:
            {
	            var smp = (ShowMidiaPacket)packet;
	            switch (smp.Midia.Type)
	            {
		            case MidiaType.Video:
		            case MidiaType.Image:
			            Modal.OpenMedia(smp.Midia);
			            break;
		            case MidiaType.Audio:
			            GameManager.Instance.PlayAudio(smp.Midia);
			            break;
	            }

	            break;
            }
			case ProtocolId.PRIVATE_MESSAGE:
			{
				var pmp = (PrivateMessagePacket)packet;
				ToastParty.Show(new ToastParty.Config()
				{
					Text = (pmp.Sender?.Entity?.Name ?? "Alguém") + " sussurrou para " + (pmp.Recipient?.Entity?.Name ?? "Deus") + ": " + pmp.Message,
				});
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
				uint length = buffer.ReadUInt32();
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
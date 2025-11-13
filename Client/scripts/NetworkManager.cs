using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Godot;
using Rpg;
using Rpg.Inventory;
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
				dup.@ref.Door?.CopyFrom(dup.Door);
				break;
			}
			case ProtocolId.ENTITY_CREATE:
			{

				var ecp = (EntityCreatePacket)packet;
				GD.Print("Received entity packet for " + ecp.BoardName + " with id " + ecp.Entity.Id);
				ClientBoard? board = GameManager.Instance.GetBoard(ecp.BoardName);
				board?.AddEntity(ecp.Entity);
				break;
			}
			case ProtocolId.ENTITY_MIDIA:
			{
				var emp = (EntityMidiaPacket)packet;
				var entity = emp.Ref.Entity;
				if (entity == null)
				{
					GD.PushWarning("Received midia packet for unknown entity: " + emp.Ref.Id);
					break;
				}
				entity.Display = emp.Midia;
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
			case ProtocolId.ENTITY_POSITION:
			{
				var epp = (EntityPositionPacket)packet;
				Entity? entity = epp.EntityRef.Entity;
				if (entity == null)
					break;
				entity.Position = epp.Position;
				break;
			}
			case ProtocolId.ENTITY_ROTATION:
			{
				var erp = (EntityRotationPacket)packet;
				Entity? entity = erp.EntityRef.Entity;
				if (entity == null)
					break;
				entity.Rotation = erp.Rotation;
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
			case ProtocolId.ENTITY_BODY_PART:
			{
				var ebpp = (EntityBodyPartPacket)packet;
				Creature? entity = ebpp.CreatureRef.Creature;
				if (entity == null)
					break;

				BodyPart? part = ebpp.Part;
				int lastSlash = ebpp.Path.LastIndexOf('/');
				string path = lastSlash != -1 ? ebpp.Path[..lastSlash] : ebpp.Path;
				BodyPart? parent = entity.BodyRoot.GetChildByPath(path);
				if (parent == null)
					return;

				parent.RemoveChild(ebpp.Path[(ebpp.Path.LastIndexOf('/') + 1)..]);
				if (part != null)
					parent.AddChild(part);
				break;
			}
			case ProtocolId.ENTITY_BODY_PART_INJURY:
			{
				var ebpcp = (EntityBodyPartInjuryPacket)packet;
				Creature? entity = ebpcp.CreatureRef.Creature;

				BodyPart? part = entity?.BodyRoot.GetChildByPath(ebpcp.Path);
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
						part.RemoveInjury(ebpcp.OldInjury!.Value);
						part.AddInjury(ebpcp.Injury);
						break;
				}
				break;
			}
			case ProtocolId.STAT_UPDATE:
			{
				var sup = (Rpg.StatHolderUpdatePacket)packet;
				IStatHolder? holder = sup.HolderRef.Holder;
				if (holder == null)
					break;

				foreach (var incoming in sup.Stats)
				{
					Stat? existing = holder.GetStat(incoming.Id);
					if (existing == null)
					{
						holder.CreateStat(incoming);
						continue;
					}

					// Update values
					existing.BaseValue = incoming.BaseValue;
					existing.MinValue = incoming.MinValue;
					existing.MaxValue = incoming.MaxValue;

					// Sync modifiers: add/update incoming
					var incomingMods = incoming.GetModifiers().ToList();
					var incomingIds = new HashSet<string>(incomingMods.Select(m => m.Id));
					foreach (var mod in incomingMods)
						existing.SetModifier(mod);

					// Remove modifiers that are not present in incoming
					var existingMods = existing.GetModifiers().Select(m => m.Id).ToList();
					foreach (var id in existingMods)
					{
						if (!incomingIds.Contains(id))
							existing.RemoveModifier(id);
					}
				}
				break;
			}
			case ProtocolId.FEATURE_UPDATE:
			{
				var efu = (FeatureUpdatePacket)packet;
				IFeatureContainer? source = efu.SourceRef.FeatureSource;
				if (source == null)
					break;

				switch (efu.UpdateType)
				{
					case FeatureUpdatePacket.FeatureUpdateType.ADD:
					{
						if (efu.Feature == null)
							throw new Exception("Feature is null");
						source.AddFeature(efu.Feature);
						break;
					}
					case FeatureUpdatePacket.FeatureUpdateType.REMOVE:
					{
						source.RemoveFeature(efu.FeatureId!);
						break;
					}
					case FeatureUpdatePacket.FeatureUpdateType.ENABLE:
					{
						source.EnableFeature(efu.FeatureId!);
						break;
					}
					case FeatureUpdatePacket.FeatureUpdateType.DISABLE:
					{
						source.DisableFeature(efu.FeatureId!);
						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}
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
                    bp.Equip(item, cei.Slot!);
                else
                    bp.RemoveItem(item);
                break;
            }
			case ProtocolId.CREATURE_SKILL_UPDATE:
			{
				var csu = (CreatureSkillUpdatePacket)packet;
				Creature? creature = csu.CreatureRef.Creature;
				if (creature == null)
					break;
				creature.ActiveSkills[csu.Data.Id] = csu.Data;
				break;
			}
			case ProtocolId.CREATURE_SKILL_REMOVE:
			{
				var csr = (CreatureSkillRemovePacket)packet;
				Creature? creature = csr.CreatureRef.Creature;
				creature?.CancelSkill(csr.SkillId);
				break;
			}
			case ProtocolId.CREATURE_ACTION_LAYER_UPDATE:
			{
				var calup = (ActionLayerUpdatePacket)packet;
				Creature? creature = calup.CreatureRef.Creature;
				if (creature == null)
					break;
				if (creature.GetActionLayer(calup.Layer.Name) != null)
					creature.UpdateActionLayer(calup.Layer);
				else
					creature.TriggerActionLayer(calup.Layer);
				break;
			}
			case ProtocolId.CREATURE_ACTION_LAYER_REMOVE:
			{
				var calrp = (ActionLayerRemovePacket)packet;
				Creature? creature = calrp.CreatureRef.Creature;
				creature?.CancelActionLayer(calrp.LayerId);
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
					Compendium.RegisterEntry(type, name, data!);
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
					Text = (pmp.Sender?.Creature?.Name ?? "Alguém") + " sussurrou para " + (pmp.Recipient?.Creature?.Name ?? "Deus") + ": " + pmp.Message,
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
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Rpg;
using Rpg.Inventory;
// ReSharper disable UnusedMember.Global

namespace Rpg;

public enum ProtocolId
{
    HANDSHAKE = 0x00,
    DISCONNECT,
    CHAT,
    BOARD_ADD,
    BOARD_REMOVE,
    FLOOR_IMAGE,
    DOOR_UPDATE,
    DOOR_INTERACT,
    COMBAT_MODE,
    ENTITY_CREATE,
    ENTITY_MIDIA,
    ENTITY_REMOVE,
    ENTITY_MOVE,
    ENTITY_POSITION,
    ENTITY_ROTATION,
    ENTITY_VELOCITY,
    ENTITY_BODY_PART,
    ENTITY_BODY_PART_INJURY,
    ENTITY_STAT,
    FEATURE_UPDATE,
    CREATURE_EQUIP_ITEM,
    CREATURE_SKILL_UPDATE,
    CREATURE_SKILL_REMOVE,
    CREATURE_ACTION_LAYER_UPDATE,
    CREATURE_ACTION_LAYER_REMOVE,
    CREATURE_SKILLTREE_UPDATE,
    EXECUTE_COMMAND,
    COMPENDIUM_UPDATE,
    SHOW_MIDIA,
    PRIVATE_MESSAGE
}

public enum DeviceType {
    DESKTOP,
    MOBILE
}

public enum StatValueType {
    Base = 0,
    Min = 1,
    Max = 2
}

public abstract class Packet : ISerializable {
    private static readonly Dictionary<ProtocolId, Type> packetTypes = new();
    static Packet(){
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsSubclassOf(typeof(Packet))) continue;

            // Create an uninitialized instance without invoking constructors using RuntimeHelpers
            if (RuntimeHelpers.GetUninitializedObject(type) is Packet instance)
                packetTypes.Add(instance.Id, type);
        }
    }

    public abstract ProtocolId Id {get;}

    public virtual void ToBytes(Stream stream){
        stream.WriteByte((byte)Id);
    }

    public static byte[] PreProcessPacket(Packet packet){
        byte[] packetBuffer = (packet as ISerializable).ToBytes();
        byte[] finalBuffer = new byte[packetBuffer.Length + 4];

        BitConverter.GetBytes((uint)packetBuffer.Length + 4).CopyTo(finalBuffer, 0);
        packetBuffer.CopyTo(finalBuffer, 4);

        return finalBuffer;
    }

    public static Packet ReadPacket(byte[] data){
        using MemoryStream stream = new MemoryStream(data);
        stream.ReadUInt32(); //Skip length
        byte id = (byte)stream.ReadByte();
        ProtocolId pid = (ProtocolId)id;
        if (packetTypes.ContainsKey(pid)){
            return (Packet)Activator.CreateInstance(packetTypes[pid], [stream])!;
        }
        throw new Exception("Unknown packet id " + id);
    }
    public static Packet ReadPacketJson(string json)
    {
        JsonObject jsonObj = JsonNode.Parse(json)!.AsObject();
        Logger.Log(jsonObj.ToJsonString());
        ProtocolId pid = (ProtocolId)jsonObj["id"]!.GetValue<int>();
        var pop = new JsonPopulator();

        if (packetTypes.ContainsKey(pid))
        {
            var instance = (Packet)RuntimeHelpers.GetUninitializedObject(packetTypes[pid])!;
            pop.PopulateObject(instance, jsonObj.ToJsonString());
            return instance;
        }
        throw new Exception("Unknown packet id " + pid);
    }
}

public class LoginPacket(string username, DeviceType device) : Packet
{
    public readonly string Username = username;
    public readonly DeviceType Device = device;

    public override ProtocolId Id => ProtocolId.HANDSHAKE;


    public LoginPacket(Stream stream) : this(stream.ReadString(), (DeviceType)stream.ReadByte())
    {
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Username);
        stream.WriteByte((byte)Device);
    }
}

public class DisconnectPacket : Packet
{
    public DisconnectPacket() {}
    public DisconnectPacket(Stream stream) {}

    public override ProtocolId Id => ProtocolId.DISCONNECT;

}

public class ChatPacket : Packet
{
    public readonly string Message;
    public readonly string BoardName;
    public override ProtocolId Id => ProtocolId.CHAT;

    public ChatPacket(Board board, string message){
        Message = message;
        BoardName = board.Name;
    }
    public ChatPacket(Stream stream){
        BoardName = stream.ReadString();
        Message = stream.ReadLongString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        stream.WriteLongString(Message);
    }
}

public class BoardAddPacket : Packet
{
    public Board Board {get; }

    public override ProtocolId Id => ProtocolId.BOARD_ADD;


    public BoardAddPacket(Board board)
    {
        Board = board;
    }

    public BoardAddPacket(Stream stream)
    {
        Board = SidedLogic.Instance.NewBoard();
        Board.Name = stream.ReadString();
        Board.TurnMode = stream.ReadByte() == 1;
        ushort msgCount = stream.ReadUInt16();
        for (int i = 0; i < msgCount; i++)
            Board.AddChatMessage(stream.ReadLongString());
        byte fCount = (byte)stream.ReadByte();

        for (int i = 0; i < fCount; i++){
            Floor floor = SidedLogic.Instance.NewFloor(stream.ReadVec2(), stream.ReadVec2(), stream.ReadUInt32());

            floor.Walls = new Polygon[stream.ReadUInt16()];
            for (int j = 0; j < floor.Walls.Length; j++)
            {
                floor.Walls[j] = new Polygon(stream);
            }

            floor.LineOfSight = new Polygon[stream.ReadUInt16()];
            for (int j = 0; j < floor.LineOfSight.Length; j++)
            {
                floor.LineOfSight[j] = new Polygon(stream);
            }

            for (int j = 0; j < floor.Size.X * floor.Size.Y; j++)
            {
                floor.TileFlags[i] = stream.ReadUInt32();
            }

            floor.DefaultEntitySight = stream.ReadFloat();
            Board.AddFloor(floor);
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(Board.Name);
        stream.WriteByte(Board.TurnMode ? (byte)1 : (byte)0);
        stream.WriteUInt16((ushort)Board.GetChatHistory().Count);
        foreach (var msg in Board.GetChatHistory())
            stream.WriteLongString(msg);
        byte fCount = Board.GetFloorCount();
        stream.WriteByte(fCount);
        for (int i = 0; i < fCount; i++)
        {
            Floor floor = Board.GetFloor(i);

            stream.WriteVec2(floor.Size);
            stream.WriteVec2(floor.TileSize);
            stream.WriteUInt32(floor.AmbientLight);
            stream.WriteUInt16((ushort)floor.Walls.Length);
            foreach (var wall in floor.Walls)
                wall.ToBytes(stream);
            
            stream.WriteUInt16((ushort)floor.LineOfSight.Length);
            foreach (var vb in floor.LineOfSight)
                vb.ToBytes(stream);

            for (int j = 0; j < floor.Size.X * floor.Size.Y; j++)
            {
                stream.WriteUInt32(floor.TileFlags[j]);
            }

            stream.WriteFloat(floor.DefaultEntitySight);
        }
    }
}

public class BoardRemovePacket : Packet
{
    public readonly string Name;
    public override ProtocolId Id => ProtocolId.BOARD_REMOVE;

    public BoardRemovePacket(Board board)
    {
        Name = board.Name;
    }

    public BoardRemovePacket(Stream stream)
    {
        Name = stream.ReadString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(Name);
    }
}

public class FloorImagePacket(string boardName, int floorIndex, Midia midia) : Packet
{
    public readonly string BoardName = boardName;
    public readonly int FloorIndex = floorIndex;
    public readonly Midia Data = midia;

    public override ProtocolId Id => ProtocolId.FLOOR_IMAGE;


    public FloorImagePacket(Stream stream) : this(stream.ReadString(), stream.ReadByte(), new Midia(stream))
    {
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        stream.WriteByte((byte)FloorIndex);
        Data.ToBytes(stream);
    }
}

public class DoorUpdatePacket : Packet
{
    public readonly DoorEntity Door;
    public readonly DoorRef @ref;

    public override ProtocolId Id => ProtocolId.DOOR_UPDATE;

    public DoorUpdatePacket(DoorEntity door)
    {
        Door = door;
        @ref = new DoorRef(door);
    }
    public DoorUpdatePacket(Stream stream)
    {
        stream.ReadByte();
        Door = new DoorEntity(stream);
        @ref = new DoorRef(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Door.ToBytes(stream);
        @ref.ToBytes(stream);
    }

}

public class DoorInteractPacket : Packet
{
    public readonly DoorRef Door;

    public override ProtocolId Id => ProtocolId.DOOR_INTERACT;

    public DoorInteractPacket(DoorEntity door)
    {
        Door = new DoorRef(door);
    }
    public DoorInteractPacket(Stream stream)
    {
        Door = new DoorRef(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Door.ToBytes(stream);
    }

}

public class CombatModePacket : Packet
{
    private bool tickInfo;
    public readonly string BoardName;
    public readonly bool CombatMode;
    public readonly uint Tick;
    public readonly uint PauseAt;

    public override ProtocolId Id => ProtocolId.COMBAT_MODE;

    public CombatModePacket(Board board)
    {
        BoardName = board.Name;
        CombatMode = board.TurnMode;
        tickInfo = true;
        Tick = board.CurrentTick;
        PauseAt = board.GetWhenToPause();
    }

    public CombatModePacket(Board board, bool combatMode)
    {
        BoardName = board.Name;
        CombatMode = combatMode;
        tickInfo = false;
    }

    public CombatModePacket(Stream stream)
    {
        BoardName = stream.ReadString();
        CombatMode = stream.ReadByte() == 1;
        tickInfo = stream.ReadBoolean();
        if (tickInfo)
        {
            Tick = stream.ReadUInt32();
            PauseAt = stream.ReadUInt32();
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        stream.WriteByte(CombatMode ? (byte)1 : (byte)0);
        stream.WriteBoolean(tickInfo);
        if (tickInfo)
        {
            stream.WriteUInt32(Tick);
            stream.WriteUInt32(PauseAt);
        }
    }
}

public class EntityCreatePacket : Packet
{
    public readonly string BoardName;
    public readonly Entity Entity;

    public override ProtocolId Id => ProtocolId.ENTITY_CREATE;

    public EntityCreatePacket(Board board, Entity entity)
    {
        BoardName = board.Name;
        this.Entity = entity;
    }

    public EntityCreatePacket(Stream stream)
    {
        BoardName = stream.ReadString();
        Entity = Entity.FromBytes(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        Entity.ToBytes(stream);
    }
}

public class EntityMidiaPacket : Packet
{
    public EntityRef Ref;
    public readonly Midia Midia;

    public override ProtocolId Id => ProtocolId.ENTITY_MIDIA;

    public EntityMidiaPacket(Entity entity, Midia newMidia)
    {
        Ref = new EntityRef(entity);
        Midia = newMidia;
    }

    public EntityMidiaPacket(Stream stream)
    {
        Ref = new EntityRef(stream);
        Midia = new Midia(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Ref.ToBytes(stream);
        Midia.ToBytes(stream);
    }
}

public class EntityRemovePacket : Packet
{
    public EntityRef Ref;

    public override ProtocolId Id => ProtocolId.ENTITY_REMOVE;

    public EntityRemovePacket(Entity entity)
    {
        Ref = new EntityRef(entity);
    }

    public EntityRemovePacket(Stream stream)
    {
        Ref = new EntityRef(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        Ref.ToBytes(stream);
    }
}

public class EntityMovePacket : Packet
{
    public EntityRef EntityRef;
    public Vector2 Position;

    public override ProtocolId Id => ProtocolId.ENTITY_MOVE;


    public EntityMovePacket(Entity toMove, Vector2 position)
    {
        EntityRef = new EntityRef(toMove);
        Position = position;
    }

    public EntityMovePacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        Position = stream.ReadVec2();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        EntityRef.ToBytes(stream);
        stream.WriteVec2(Position);
    }
}

public class EntityPositionPacket : Packet
{
    public EntityRef EntityRef;
    public Vector3 Position;

    public override ProtocolId Id => ProtocolId.ENTITY_POSITION;


    public EntityPositionPacket(Entity toMove, Vector3 position)
    {
        EntityRef = new EntityRef(toMove);
        Position = position;
    }

    public EntityPositionPacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        Position = stream.ReadVec3();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        EntityRef.ToBytes(stream);
        stream.WriteVec3(Position);
    }
}

public class EntityRotationPacket : Packet
{
    public EntityRef EntityRef;
    public readonly float Rotation;

    public override ProtocolId Id => ProtocolId.ENTITY_ROTATION;


    public EntityRotationPacket(Entity toMove, float rotation)
    {
        EntityRef = new EntityRef(toMove);
        Rotation = rotation;
    }

    public EntityRotationPacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        Rotation = stream.ReadFloat();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        EntityRef.ToBytes(stream);
        stream.WriteFloat(Rotation);
    }
}

public class EntityBodyPartPacket : Packet
{
    public CreatureRef CreatureRef;
    public readonly string Path;
    public readonly BodyPart? Part;

    public override ProtocolId Id => ProtocolId.ENTITY_BODY_PART;

    public EntityBodyPartPacket(BodyPart part)
    {
        if (part.Owner == null)
            throw new ArgumentException("Part needs an owner for this packet. ", nameof(part));
        CreatureRef = new CreatureRef(part.Owner);
        Part = part;
        Path = part.Path;
    }

    public EntityBodyPartPacket(Creature creature, string path)
    {
        CreatureRef = new CreatureRef(creature);
        Path = path;
        Part = null;
    }

    public EntityBodyPartPacket(Stream stream)
    {
        CreatureRef = new CreatureRef(stream);
        Path = stream.ReadString();
        Part = stream.ReadByte() == 1 ? new BodyPart(stream, CreatureRef.Creature?.Body) : null;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        CreatureRef.ToBytes(stream);
        stream.WriteString(Path);
        if (Part == null)
        {
            stream.WriteByte(0);
            return;
        }
        Part.ToBytes(stream);
    }
}

public class EntityBodyPartInjuryPacket : Packet
{
    public CreatureRef CreatureRef;
    public readonly string Path;
    public readonly Injury Injury;
    public readonly bool Remove;

    public override ProtocolId Id => ProtocolId.ENTITY_BODY_PART_INJURY;

    public EntityBodyPartInjuryPacket(BodyPart part, Injury condition, bool remove = false)
    {
        if (part.Owner == null)
            throw new ArgumentException("Part needs a creature for this packet. ", nameof(part));
        CreatureRef = new CreatureRef(part.Owner);
        Path = part.Path;
        Injury = condition;
        Remove = remove;
    }

    public EntityBodyPartInjuryPacket(Stream stream)
    {
        CreatureRef = new CreatureRef(stream);
        Path = stream.ReadString();
        Injury = new Injury(stream);
        Remove = stream.ReadByte() == 1;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        CreatureRef.ToBytes(stream);
        stream.WriteString(Path);
        Injury.ToBytes(stream);
        stream.WriteByte(Remove ? (byte)1 : (byte)0);
    }
}

public class EntityStatPacket : Packet
{
    public enum StatOp : byte
    {
        Create = 0,
        SetValue = 1,
        SetModifier = 2,
        RemoveModifier = 3
    }

    public override ProtocolId Id => ProtocolId.ENTITY_STAT;

    public EntityRef EntityRef;
    public string StatId = string.Empty;
    public StatOp Operation;

    // Payloads
    public Stat? Stat; // for Create
    public float Value; // for SetValue
    public StatValueType ValueType; // for SetValue
    public StatModifier? Modifier; // for SetModifier
    public string? ModifierId; // for RemoveModifier

    // Constructors for convenience
    public EntityStatPacket(Entity entity, Stat stat)
    {
        EntityRef = new EntityRef(entity);
        Stat = stat.Clone();
        StatId = stat.Id;
        Operation = StatOp.Create;
    }

    public EntityStatPacket(Entity entity, string statId, float value, StatValueType type = StatValueType.Base)
    {
        EntityRef = new EntityRef(entity);
        StatId = statId;
        Value = value;
        ValueType = type;
        Operation = StatOp.SetValue;
    }

    public EntityStatPacket(Entity entity, string statId, StatModifier modifier)
    {
        EntityRef = new EntityRef(entity);
        StatId = statId;
        Modifier = modifier;
        Operation = StatOp.SetModifier;
    }

    public EntityStatPacket(Entity entity, string statId, string modifierId)
    {
        EntityRef = new EntityRef(entity);
        StatId = statId;
        ModifierId = modifierId;
        Operation = StatOp.RemoveModifier;
    }

    public EntityStatPacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        StatId = stream.ReadString();
        Operation = (StatOp)stream.ReadByte();
        switch (Operation)
        {
            case StatOp.Create:
                Stat = new Stat(stream);
                break;
            case StatOp.SetValue:
                ValueType = (StatValueType)stream.ReadByte();
                Value = stream.ReadFloat();
                break;
            case StatOp.SetModifier:
                Modifier = new StatModifier(stream);
                break;
            case StatOp.RemoveModifier:
                ModifierId = stream.ReadString();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        EntityRef.ToBytes(stream);
        stream.WriteString(StatId);
        stream.WriteByte((byte)Operation);
        switch (Operation)
        {
            case StatOp.Create:
                Stat!.ToBytes(stream);
                break;
            case StatOp.SetValue:
                stream.WriteByte((byte)ValueType);
                stream.WriteFloat(Value);
                break;
            case StatOp.SetModifier:
                Modifier!.Value.ToBytes(stream);
                break;
            case StatOp.RemoveModifier:
                stream.WriteString(ModifierId!);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public class FeatureUpdatePacket : Packet
{
    public enum FeatureUpdateType
    {
        ENABLE,
        DISABLE,
        ADD,
        REMOVE
    }

    public override ProtocolId Id => ProtocolId.FEATURE_UPDATE;
    public FeatureUpdateType UpdateType;
    public FeatureSourceRef SourceRef;
    public string? FeatureId;
    public Feature? Feature;
    private FeatureUpdatePacket(FeatureUpdateType updateType, FeatureSourceRef @ref, Feature feature)
    {
        UpdateType = updateType;
        SourceRef = @ref;
        Feature = feature;
        FeatureId = feature?.GetId();
    }
    private FeatureUpdatePacket(FeatureUpdateType updateType, FeatureSourceRef @ref, string feature)
    {
        UpdateType = updateType;
        SourceRef = @ref;
        FeatureId = feature;
    }
    public FeatureUpdatePacket(Stream stream)
    {
        UpdateType = (FeatureUpdateType)stream.ReadByte();
        SourceRef = new FeatureSourceRef(stream);
        if (UpdateType == FeatureUpdateType.ADD)
            Feature = Feature.FromBytes(stream);
        else
            FeatureId = stream.ReadString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        SourceRef.ToBytes(stream);
        stream.WriteByte((byte)UpdateType);
        if (UpdateType == FeatureUpdateType.ADD)
            Feature!.ToBytes(stream);
        else
            stream.WriteString(FeatureId);
    }

    public static FeatureUpdatePacket Enable(IFeatureContainer entity, string id)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.ENABLE,
            new FeatureSourceRef(entity),
            id
        );
    }
    public static FeatureUpdatePacket Enable(IFeatureContainer entity, Feature feature)
    {
        return Enable(entity, feature.GetId());
    }
    public static FeatureUpdatePacket Disable(IFeatureContainer entity, string id)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.DISABLE,
            new FeatureSourceRef(entity),
            id
        );
    }
    public static FeatureUpdatePacket Disable(IFeatureContainer entity, Feature feature)
    {
        return Disable(entity, feature.GetId());
    }
    public static FeatureUpdatePacket Add(IFeatureContainer entity, Feature feature)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.ADD,
            new FeatureSourceRef(entity),
            feature
        );
    }
    public static FeatureUpdatePacket Remove(Entity entity, string id)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.REMOVE,
            new FeatureSourceRef(entity),
            id
        );
    }
    public static FeatureUpdatePacket Remove(Entity entity, Feature feature)
    {
        return Remove(entity, feature.GetId());
    }
}

public class CreatureEquipItemPacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_EQUIP_ITEM;
    public readonly BodyPartRef BPRef;
    public readonly string? Slot;
    public ItemRef ItemRef;
    public readonly bool Equipped;

    public CreatureEquipItemPacket(BodyPart bp, string slot, Item item)
    {
        if (!item.HasProperty<EquipmentProperty>())
            throw new ArgumentException("Item isn't an equipment!");
        if (bp.Owner == null)
            throw new ArgumentException("Bodypart doesn't have an owner!");
        
        BPRef = new BodyPartRef(bp);
        ItemRef = new ItemRef(item);
        Slot = slot;
        Equipped = true;
    }

    public CreatureEquipItemPacket(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            throw new ArgumentException("Item isn't an equipment!");
        if (!(item.Holder is BodyPart bp))
            throw new ArgumentException("Item isn't equipped by a BodyPart");
        if (bp.Owner == null)
            throw new ArgumentException("Bodypart doesn't have an owner!");
        BPRef = new BodyPartRef(bp);
        ItemRef = new ItemRef(item);
        Equipped = false;
    }

    public CreatureEquipItemPacket(Stream stream)
    {
        ItemRef = new ItemRef(stream);
        BPRef = new BodyPartRef(stream);
        Equipped = stream.ReadByte() != 0;
        if (Equipped)
            Slot = stream.ReadString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        ItemRef.ToBytes(stream);
        BPRef.ToBytes(stream);
        stream.WriteByte((byte)(Equipped ? 1 : 0));
        if(Equipped)
            stream.WriteString(Slot);
    }

}

public class CreatureSkillUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_SKILL_UPDATE;
    public CreatureRef CreatureRef;
    public readonly SkillData Data;

    public CreatureSkillUpdatePacket(Creature entity, SkillData skill)
    {
        CreatureRef = new CreatureRef(entity);
        Data = skill;
    }

    public CreatureSkillUpdatePacket(Stream stream)
    {
        CreatureRef = new CreatureRef(stream);
        Data = new SkillData(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        CreatureRef.ToBytes(stream);
        Data.ToBytes(stream);
    }
}
public class CreatureSkillRemovePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_SKILL_REMOVE;
    public CreatureRef CreatureRef;
    public readonly int SkillId;

    public CreatureSkillRemovePacket(Creature creature, int id)
    {
        CreatureRef = new CreatureRef(creature);
        SkillId = id;
    }
    public CreatureSkillRemovePacket(Stream stream)
    {
        CreatureRef = new CreatureRef(stream);
        SkillId = stream.ReadInt32();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        CreatureRef.ToBytes(stream);
        stream.WriteInt32(SkillId);
    }
}

public class ActionLayerUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_ACTION_LAYER_UPDATE;
    public CreatureRef CreatureRef;
    public readonly ActionLayer Layer;

    public ActionLayerUpdatePacket(Creature entity, ActionLayer layer)
    {
        CreatureRef = new CreatureRef(entity);
        Layer = layer;
    }

    public ActionLayerUpdatePacket(Stream stream)
    {
        CreatureRef = new CreatureRef(stream);
        Layer = new ActionLayer(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        CreatureRef.ToBytes(stream);
        Layer.ToBytes(stream);
    }
}
public class ActionLayerRemovePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_ACTION_LAYER_REMOVE;
    public CreatureRef CreatureRef;
    public readonly string LayerId;

    public ActionLayerRemovePacket(Creature entity, string id)
    {
        CreatureRef = new CreatureRef(entity);
        LayerId = id;
    }
    public ActionLayerRemovePacket(Stream stream)
    {
        CreatureRef = new CreatureRef(stream);
        LayerId = stream.ReadString();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        CreatureRef.ToBytes(stream);
        stream.WriteString(LayerId);
    }
}

public class SkillTreeUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_SKILLTREE_UPDATE;
    public readonly SkillTreeEntryRef EntryRef;
    public readonly bool Enabled;

    public SkillTreeUpdatePacket(SkillTreeEntry entry) : this(entry, entry.Enabled) { }
    public SkillTreeUpdatePacket(SkillTreeEntry entry, bool enabled)
    {
        EntryRef = new SkillTreeEntryRef(entry);
        Enabled = enabled;
    }
    public SkillTreeUpdatePacket(Stream stream)
    {
        EntryRef = new SkillTreeEntryRef(stream);
        Enabled = stream.ReadBoolean();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        EntryRef.ToBytes(stream);
        stream.WriteBoolean(Enabled);
    }
}

public class ExecuteCommandPacket : Packet
{
    public override ProtocolId Id => ProtocolId.EXECUTE_COMMAND;

    public readonly string[] Commands;

    public ExecuteCommandPacket(params string[] commands)
    {
        Commands = commands;
    }

    public ExecuteCommandPacket(Stream stream)
    {
        int len = stream.ReadByte();
        Commands = new string[len];
        for (int i = 0; i < len; i++)
            Commands[i] = stream.ReadString();
    }
    
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteByte((byte)Commands.Length);
        foreach (string command in Commands)
            stream.WriteString(command);
    }
}

public class CompendiumUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.COMPENDIUM_UPDATE;

    public readonly bool Remove;
    public readonly string RegistryName;
    public readonly string DataName;
    public readonly JsonObject? Json;

    public static CompendiumUpdatePacket RemoveEntry(string regName, string entryName)
    {
        return new CompendiumUpdatePacket(true, regName, entryName, null);
    }

    public static CompendiumUpdatePacket AddEntry(string regName, string entryName, JsonObject json)
    {
        return new CompendiumUpdatePacket(false, regName, entryName, json);
    }

    public static CompendiumUpdatePacket UpdateEntry(string regName, string entryName, JsonObject json)
    {
        return AddEntry(regName, entryName, json);
    }

    protected CompendiumUpdatePacket(bool remove, string registryName, string dataName, JsonObject? json)
    {
        Remove = remove;
        RegistryName = registryName;
        DataName = dataName;
        Json = json;
    }

    public CompendiumUpdatePacket(Stream stream)
    {
        Remove = stream.ReadBoolean();
        RegistryName = stream.ReadString();
        DataName = stream.ReadString();
        ulong count = stream.ReadUInt64();
        if (!Remove)
        {
            Json = null;
            return;
        }
        byte[] data = stream.ReadExactly((uint)count);
        string str = new (data.Select(b => (char)b).ToArray());
        var parsed = JsonNode.Parse(str)?.AsObject();
        if (parsed == null)
            throw new Exception("Failed to parse compendium json data!");
        Json = parsed;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteBoolean(Remove);
        stream.WriteString(RegistryName);
        stream.WriteString(DataName);

        if (Remove)
        {
            string str = Json!.ToJsonString();
            stream.WriteUInt64((ulong)str.Length);
            stream.Write(str.ToBytes());
        }
    }
}

public class ShowMidiaPacket : Packet
{
    public override ProtocolId Id => ProtocolId.SHOW_MIDIA;
    public readonly Board Board;
    public readonly Midia Midia;

    public ShowMidiaPacket(Board board, Midia midia)
    {
        Board = board;
        Midia = midia;
    }

    public ShowMidiaPacket(Stream stream)
    {
        Board = new BoardRef(stream).Board!;
        Midia = new Midia(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        new BoardRef(Board).ToBytes(stream);
        Midia.ToBytes(stream);
    }
}

public class PrivateMessagePacket : Packet
{
    public override ProtocolId Id => ProtocolId.PRIVATE_MESSAGE;
    public readonly CreatureRef? Sender;
    public readonly CreatureRef? Recipient;
    public readonly string Message;

    public PrivateMessagePacket(Creature? sender, Creature? recipient, string message)
    {
        Sender = sender != null ? new CreatureRef(sender) : null;
        Recipient = recipient != null ? new CreatureRef(recipient) : null;
        Message = message;
    }

    public PrivateMessagePacket(Stream stream)
    {
        if (stream.ReadBoolean())
            Sender = new CreatureRef(stream);
        if (stream.ReadBoolean())
            Recipient = new CreatureRef(stream);
        Message = stream.ReadLongString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        if (Sender != null)
        {
            stream.WriteBoolean(true);
            Sender.Value.ToBytes(stream);
        }
        else
        {
            stream.WriteBoolean(false);
        }
        if (Recipient != null)
        {
            stream.WriteBoolean(true);
            Recipient.Value.ToBytes(stream);
        }
        else
        {
            stream.WriteBoolean(false);
        }
        stream.WriteLongString(Message);
    }
}
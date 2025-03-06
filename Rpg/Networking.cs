using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;
using Rpg.Entities;

namespace Rpg;

public enum ProtocolId{
	HANDSHAKE = 0x00,
    DISCONNECT,
    CHAT,
	BOARD_ADD,
    BOARD_REMOVE,
    FLOOR_IMAGE,
    TURN_MODE,
    COMBAT_TICK,
    ENTITY_CREATE,
    ENTITY_REMOVE,
    ENTITY_MOVE,
    ENTITY_POSITION,
    ENTITY_ROTATION,
    ENTITY_VELOCITY,
    ENTITY_BODY_PART,
    ENTITY_BODY_PART_INJURY,
    ENTITY_STAT_BASE,
    ENTITY_STAT_MODIFIER_UPDATE,
    ENTITY_STAT_MODIFIER_REMOVE
}

public enum DeviceType {
    DESKTOP,
    MOBILE
}

public abstract class Packet : ISerializable {
    private static Dictionary<ProtocolId, Type> packetTypes = new Dictionary<ProtocolId, Type>();
    static Packet(){
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()){
            if (type.IsSubclassOf(typeof(Packet))){
                var instance = FormatterServices.GetUninitializedObject(type) as Packet;
                if (instance != null)
                    packetTypes.Add(instance.Id, type);
            }
        }
    }

    public abstract ProtocolId Id {get;}

    public virtual void ToBytes(Stream stream){
        stream.WriteByte((byte)Id);
    }

    public static byte[] PreProcessPacket(Packet packet){
        byte[] packetBuffer = (packet as ISerializable).ToBytes();
        byte[] finalBuffer = new byte[packetBuffer.Length + 4];

        BitConverter.GetBytes((UInt32)packetBuffer.Length + 4).CopyTo(finalBuffer, 0);
        packetBuffer.CopyTo(finalBuffer, 4);

        return finalBuffer;
    }

    public static Packet ReadPacket(byte[] data){
        using MemoryStream stream = new MemoryStream(data);
        stream.ReadUInt32(); //Skip length
        byte id = (byte)stream.ReadByte();
        ProtocolId pid = (ProtocolId)id;
        if (packetTypes.ContainsKey(pid)){
            return (Packet)Activator.CreateInstance(packetTypes[pid], new object[]{stream});
        }
        throw new Exception("Unknown packet id " + id);
    }
}

public class LoginPacket : Packet
{
    public string Username { get; private set; }
    public DeviceType Device{ get; private set; }

    public override ProtocolId Id => ProtocolId.HANDSHAKE;


    public LoginPacket(string username, DeviceType device){
        Username = username;
        Device = device;
    }
    public LoginPacket(Stream stream){
        Username = stream.ReadString();
        Device = (DeviceType)stream.ReadByte();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteString(Username);
        stream.WriteByte((Byte)Device);
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
    public string Message;
    public string BoardName;
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
    public Board Board {get; private set;}

    public override ProtocolId Id => ProtocolId.BOARD_ADD;


    public BoardAddPacket(Board board)
    {
        Board = board;
    }

    public BoardAddPacket(Stream stream)
    {
        Board = SidedLogic.Instance.NewBoard();
        Board.Name = stream.ReadString();
        Board.CombatMode = stream.ReadByte() == 1;
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

            floor.Lights = new Light[stream.ReadUInt16()];
            for (int j = 0; j < floor.Lights.Length; j++)
            {
                floor.Lights[j] = new Light(stream);
            }

            for (int j = 0; j < floor.Size.X * floor.Size.Y; j++)
            {
                floor.TileFlags[i] = stream.ReadUInt32();
            }
            Board.AddFloor(floor);
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(Board.Name);
        stream.WriteByte(Board.CombatMode ? (byte)1 : (byte)0);
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

            stream.WriteUInt16((ushort)floor.Lights.Length);
            foreach (var light in floor.Lights)
                light.ToBytes(stream);

            for (int j = 0; j < floor.Size.X * floor.Size.Y; j++)
            {
                stream.WriteUInt32(floor.TileFlags[j]);
            }
        }
    }
}

public class BoardRemovePacket : Packet
{
    public string Name;
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

public class FloorImagePacket : Packet
{
    public string BoardName;
    public int FloorIndex;
    public byte[] Data{get; private set;}

    public override ProtocolId Id => ProtocolId.FLOOR_IMAGE;


    public FloorImagePacket(string boardName, int floorIndex, byte[] data)
    {
        BoardName = boardName;
        FloorIndex = floorIndex;
        Data = data;
    }

    public FloorImagePacket(Stream stream)
    {
        BoardName = stream.ReadString();
        FloorIndex = stream.ReadByte();
        Data = stream.ReadExactly(stream.ReadUInt32());
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        stream.WriteByte((byte)FloorIndex);
        stream.WriteUInt32((uint)Data.Length);
        stream.Write(Data);
    }
}

public class TurnModePacket : Packet
{
    public string BoardName;
    public bool TurnMode;

    public override ProtocolId Id => ProtocolId.TURN_MODE;

    public TurnModePacket(Board board, bool turnMode)
    {
        BoardName = board.Name;
        TurnMode = turnMode;
    }

    public TurnModePacket(Stream stream)
    {
        BoardName = stream.ReadString();
        TurnMode = stream.ReadByte() == 1;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        stream.WriteByte(TurnMode ? (byte)1 : (byte)0);
    }
}

public class EntityCreatePacket : Packet
{
    public string BoardName;
    public Entity Entity;

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
    public float Rotation;

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
    public string Path;
    public BodyPart? Part;

    public override ProtocolId Id => ProtocolId.ENTITY_BODY_PART;

    public EntityBodyPartPacket(BodyPart part)
    {
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
        if (stream.ReadByte() == 1)
            Part = new BodyPart(stream, CreatureRef.Creature.Body);
        else
            Part = null;
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
    public string Path;
    public Injury Injury;
    public bool Remove;

    public override ProtocolId Id => ProtocolId.ENTITY_BODY_PART_INJURY;

    public EntityBodyPartInjuryPacket(BodyPart part, Injury condition, bool remove = false)
    {
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

public class EntityStatBasePacket : Packet
{
    public EntityRef EntityRef;
    public string StatId;
    public float Value;

    public override ProtocolId Id => ProtocolId.ENTITY_STAT_BASE;

    public EntityStatBasePacket(Entity entity, string stat, float value)
    {
        EntityRef = new EntityRef(entity);
        StatId = stat;
        Value = value;
    }

    public EntityStatBasePacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        StatId = stream.ReadString();
        Value = stream.ReadFloat();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        EntityRef.ToBytes(stream);
        stream.WriteString(StatId);
        stream.WriteFloat(Value);
    }
}

public class EntityStatModifierPacket : Packet
{
    public EntityRef EntityRef;
    public string StatId;
    public StatModifier Modifier;

    public override ProtocolId Id => ProtocolId.ENTITY_STAT_MODIFIER_UPDATE;

    public EntityStatModifierPacket(Entity entity, string statid, StatModifier modifier)
    {
        EntityRef = new EntityRef(entity);
        StatId = statid;
        Modifier = modifier;
    }

    public EntityStatModifierPacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        StatId = stream.ReadString();
        Modifier = new StatModifier(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        EntityRef.ToBytes(stream);
        stream.WriteString(StatId);
        Modifier.ToBytes(stream);
    }
}

public class EntityStatModifierRemovePacket : Packet
{
    public EntityRef EntityRef;
    public string StatId;
    public string ModifierId;

    public override ProtocolId Id => ProtocolId.ENTITY_STAT_MODIFIER_REMOVE;

    public EntityStatModifierRemovePacket(Entity entity, string statid, string modifierId)
    {
        EntityRef = new EntityRef(entity);
        StatId = statid;
        ModifierId = modifierId;
    }

    public EntityStatModifierRemovePacket(Stream stream)
    {
        EntityRef = new EntityRef(stream);
        StatId = stream.ReadString();
        ModifierId = stream.ReadString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        EntityRef.ToBytes(stream);
        stream.WriteString(StatId);
        stream.WriteString(ModifierId);
    }
}
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Components.Inventory;
using Rpg.Features;
using Rpg.Health;
using Rpg.Skills;
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
    ENTITY_REMOVE,
    TOKEN_UPDATE,
    TOKEN_MOVE,
    ENTITY_BODY_PART_INJURY,
    STAT_UPDATE,
    FEATURE_UPDATE,
    BODY_EQUIP_ITEM,
    SKILL_UPDATE,
    SKILL_REMOVE,
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
    public Board Board;

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
                floor.TileFlags[j] = stream.ReadUInt32();
            }

            floor.DefaultEntitySight = stream.ReadFloat();
            Board.AddFloor(floor);
        }

        ushort entityCount = stream.ReadUInt16();
        for (int i = 0; i < entityCount; i++)
        {
            Entity entity = new Entity(stream);
            Board.AddEntity(entity);
        }

        Board.InitializeEntities();
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

        var entities = Board.GetEntities().ToArray();
        stream.WriteUInt16((ushort)entities.Length);
        foreach (var entity in entities)
        {
            entity.ToBytes(stream);
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
    public readonly Door Door;
    public readonly ComponentRef<Door> @ref;

    public override ProtocolId Id => ProtocolId.DOOR_UPDATE;

    public DoorUpdatePacket(Door door)
    {
        Door = door;
        @ref = new ComponentRef<Door>(door);
    }
    public DoorUpdatePacket(Stream stream)
    {
        stream.ReadByte();
        @ref = new ComponentRef<Door>(stream);
        Door = new Door(stream)
        {
            Entity = @ref.Component?.Entity
        };
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        @ref.ToBytes(stream);
        Door.ToBytes(stream);
    }

}

public class DoorInteractPacket : Packet
{
    public readonly ComponentRef<Door> Door;

    public override ProtocolId Id => ProtocolId.DOOR_INTERACT;

    public DoorInteractPacket(Door door)
    {
        Door = new ComponentRef<Door>(door);
    }
    public DoorInteractPacket(Stream stream)
    {
        Door = new ComponentRef<Door>(stream);
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
    public readonly Entity[] Entities;

    public override ProtocolId Id => ProtocolId.ENTITY_CREATE;

    public EntityCreatePacket(Board board, params Entity[] entities)
    {
        BoardName = board.Name;
        this.Entities = entities;
    }

    public EntityCreatePacket(Stream stream)
    {
        BoardName = stream.ReadString();
        Entities = [];
        ushort count = stream.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            Entities = Entities.Append(new Entity(stream)).ToArray();
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteString(BoardName);
        stream.WriteUInt16((ushort)Entities.Length);
        foreach (Entity entity in Entities)
        {
            entity.ToBytes(stream);
        }
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

public class TokenMovePacket : Packet
{
    public override ProtocolId Id => ProtocolId.TOKEN_MOVE;

    public readonly ComponentRef<Token> TokenRef;
    public readonly Vector2 NewPos;
    public readonly float NewRotation;

    public TokenMovePacket(Token token)
    {
        TokenRef = new ComponentRef<Token>(token);
        NewPos = new Vector2(token.Position.X, token.Position.Y);
        NewRotation = token.Rotation;
    }
    public TokenMovePacket(Token token, Vector2 pos, float rot)
    {
        TokenRef = new ComponentRef<Token>(token);
        NewPos = pos;
        NewRotation = rot;
    }
    public TokenMovePacket(Stream stream)
    {
        TokenRef = new ComponentRef<Token>(stream);
        NewPos = stream.ReadVec2();
        NewRotation = stream.ReadFloat();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        TokenRef.ToBytes(stream);
        stream.WriteVec2(NewPos);
        stream.WriteFloat(NewRotation);
    }
}

public class TokenUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.TOKEN_UPDATE;
    public readonly ComponentRef<Token> TokenRef;
    public readonly Token NewToken;

    public TokenUpdatePacket(Token token) : base()
    {
        TokenRef = new ComponentRef<Token>(token);
        NewToken = token;
    }
    public TokenUpdatePacket(Stream stream)
    {
        TokenRef = new ComponentRef<Token>(stream);
        NewToken = Component.FromBytes(stream) as Token;
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        TokenRef.ToBytes(stream);
        NewToken.ToBytes(stream);
    }

}

public class EntityBodyPartInjuryPacket : Packet
{
    public enum InjuryPacketType
    {
        ADD,
        REMOVE,
        REPLACE
    }
    public ComponentRef<BodyPart> BpRef;
    public readonly Injury? OldInjury;
    public readonly Injury Injury;
    public readonly InjuryPacketType Type;

    public override ProtocolId Id => ProtocolId.ENTITY_BODY_PART_INJURY;

    public EntityBodyPartInjuryPacket(BodyPart part, Injury condition, InjuryPacketType type)
    {
        BpRef = new ComponentRef<BodyPart>(part);
        Injury = condition;
        Type = type;
    }
    public EntityBodyPartInjuryPacket(BodyPart part, Injury condition, Injury old)
    {
        BpRef = new ComponentRef<BodyPart>(part);
        Injury = condition;
        OldInjury = old;
        Type = InjuryPacketType.REPLACE;
    }

    public EntityBodyPartInjuryPacket(Stream stream)
    {
        BpRef = new ComponentRef<BodyPart>(stream);
        Injury = new Injury(stream);
        Type = (InjuryPacketType)stream.ReadByte();
        if (Type == InjuryPacketType.REPLACE)
            OldInjury = new Injury(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        BpRef.ToBytes(stream);
        Injury.ToBytes(stream);
        stream.WriteByte((byte)Type);
        if (Type == InjuryPacketType.REPLACE)
            OldInjury!.Value.ToBytes(stream);
    }
}

//Maybe consider sepparating this into many smaller packets, so it doesn't need to send the entire stat with all modifiers
public class StatsUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.STAT_UPDATE;

    public ComponentRef<StatsContainer> StatsRef;
    public List<Stat> Stats = new List<Stat>();

    // Create packet from a stat holder: sends all stats
    public StatsUpdatePacket(StatsContainer stats)
    {
        StatsRef = new ComponentRef<StatsContainer>(stats);
        foreach (var s in stats.Stats)
            Stats.Add(s.Clone());
    }

    // Deserialize
    public StatsUpdatePacket(Stream stream)
    {
        StatsRef = new ComponentRef<StatsContainer>(stream);
        ushort statCount = stream.ReadUInt16();
        for (int i = 0; i < statCount; i++)
        {
            Stat stat = new Stat(stream);
            Stats.Add(stat);
        }
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        StatsRef.ToBytes(stream);
        stream.WriteUInt16((ushort)Stats.Count);
        foreach (var stat in Stats)
            stat.ToBytes(stream);
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
    public ComponentRef<FeaturesContainer> ContainerRef;
    public string? FeatureId;
    public Feature? Feature;
    private FeatureUpdatePacket(FeatureUpdateType updateType, ComponentRef<FeaturesContainer> @ref, Feature feature)
    {
        UpdateType = updateType;
        ContainerRef = @ref;
        Feature = feature;
        FeatureId = feature?.GetId();
    }
    private FeatureUpdatePacket(FeatureUpdateType updateType, ComponentRef<FeaturesContainer> @ref, string feature)
    {
        UpdateType = updateType;
        ContainerRef = @ref;
        FeatureId = feature;
    }
    public FeatureUpdatePacket(Stream stream)
    {
        UpdateType = (FeatureUpdateType)stream.ReadByte();
        ContainerRef = new ComponentRef<FeaturesContainer>(stream);
        if (UpdateType == FeatureUpdateType.ADD)
            Feature = Feature.FromBytes(stream);
        else
            FeatureId = stream.ReadString();
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        ContainerRef.ToBytes(stream);
        stream.WriteByte((byte)UpdateType);
        if (UpdateType == FeatureUpdateType.ADD)
            Feature!.ToBytes(stream);
        else
            stream.WriteString(FeatureId);
    }

    public static FeatureUpdatePacket Enable(FeaturesContainer component, string id)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.ENABLE,
            new ComponentRef<FeaturesContainer>(component),
            id
        );
    }
    public static FeatureUpdatePacket Enable(FeaturesContainer component, Feature feature)
    {
        return Enable(component, feature.GetId());
    }
    public static FeatureUpdatePacket Disable(FeaturesContainer component, string id)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.DISABLE,
            new ComponentRef<FeaturesContainer>(component),
            id
        );
    }
    public static FeatureUpdatePacket Disable(FeaturesContainer component, Feature feature)
    {
        return Disable(component, feature.GetId());
    }
    public static FeatureUpdatePacket Add(FeaturesContainer component, Feature feature)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.ADD,
            new ComponentRef<FeaturesContainer>(component),
            feature
        );
    }
    public static FeatureUpdatePacket Remove(FeaturesContainer component, string id)
    {
        return new FeatureUpdatePacket
        (
            FeatureUpdateType.REMOVE,
            new ComponentRef<FeaturesContainer>(component),
            id
        );
    }
    public static FeatureUpdatePacket Remove(FeaturesContainer component, Feature feature)
    {
        return Remove(component, feature.GetId());
    }
}

public class BodyEquipItemPacket : Packet
{
    public override ProtocolId Id => ProtocolId.BODY_EQUIP_ITEM;
    public readonly ComponentRef<BodyPart> BPRef;
    public readonly string? Slot;
    public ComponentRef<EquipmentProperty> ItemRef;
    public readonly bool Equipped;

    public BodyEquipItemPacket(BodyPart bp, string slot, Item item)
    {
        if (item.GetProperty<EquipmentProperty>() == null)
            throw new ArgumentException("Item isn't an equipment!");
        
        BPRef = new ComponentRef<BodyPart>(bp);
        ItemRef = new ComponentRef<EquipmentProperty>(item.GetProperty<EquipmentProperty>()!);
        Slot = slot;
        Equipped = true;
    }

    public BodyEquipItemPacket(Item item)
    {
        var ep = item.GetProperty<EquipmentProperty>();
        if (ep == null)
            throw new ArgumentException("Item isn't an equipment!");
        if (!(item.Holder is BodyPart bp))
            throw new ArgumentException("Item isn't equipped by a BodyPart");
        BPRef = new ComponentRef<BodyPart>(bp);
        ItemRef = new ComponentRef<EquipmentProperty>(item.GetProperty<EquipmentProperty>()!);
        Equipped = false;
    }

    public BodyEquipItemPacket(Stream stream)
    {
        ItemRef = new ComponentRef<EquipmentProperty>(stream);
        BPRef = new ComponentRef<BodyPart>(stream);
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

public class SkillUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.SKILL_UPDATE;
    public ComponentRef<SkillExecutor> Ref;
    public readonly SkillData Data;

    public SkillUpdatePacket(SkillExecutor executor, SkillData skill)
    {
        Ref = new ComponentRef<SkillExecutor>(executor);
        Data = skill;
    }

    public SkillUpdatePacket(Stream stream)
    {
        Ref = new ComponentRef<SkillExecutor>(stream);
        Data = new SkillData(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Ref.ToBytes(stream);
        Data.ToBytes(stream);
    }
}
public class SkillRemovePacket : Packet
{
    public override ProtocolId Id => ProtocolId.SKILL_REMOVE;
    public ComponentRef<SkillExecutor> Ref;
    public readonly int SkillId;

    public SkillRemovePacket(SkillExecutor executor, int id)
    {
        Ref = new ComponentRef<SkillExecutor>(executor);
        SkillId = id;
    }
    public SkillRemovePacket(Stream stream)
    {
        Ref = new ComponentRef<SkillExecutor>(stream);
        SkillId = stream.ReadInt32();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Ref.ToBytes(stream);
        stream.WriteInt32(SkillId);
    }
}

public class ActionLayerUpdatePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_ACTION_LAYER_UPDATE;
    public ComponentRef<SkillExecutor> Ref;
    public readonly ActionLayer Layer;

    public ActionLayerUpdatePacket(SkillExecutor executor, ActionLayer layer)
    {
        Ref = new ComponentRef<SkillExecutor>(executor);
        Layer = layer;
    }

    public ActionLayerUpdatePacket(Stream stream)
    {
        Ref = new ComponentRef<SkillExecutor>(stream);
        Layer = new ActionLayer(stream);
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Ref.ToBytes(stream);
        Layer.ToBytes(stream);
    }
}
public class ActionLayerRemovePacket : Packet
{
    public override ProtocolId Id => ProtocolId.CREATURE_ACTION_LAYER_REMOVE;
    public ComponentRef<SkillExecutor> Ref;
    public readonly string LayerId;

    public ActionLayerRemovePacket(SkillExecutor executor, string id)
    {
        Ref = new ComponentRef<SkillExecutor>(executor);
        LayerId = id;
    }
    public ActionLayerRemovePacket(Stream stream)
    {
        Ref = new ComponentRef<SkillExecutor>(stream);
        LayerId = stream.ReadString();
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Ref.ToBytes(stream);
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
    public readonly JsonElement? Json;

    public static CompendiumUpdatePacket RemoveEntry(string regName, string entryName)
    {
        return new CompendiumUpdatePacket(true, regName, entryName, null);
    }

    public static CompendiumUpdatePacket AddEntry(string regName, string entryName, JsonElement json)
    {
        return new CompendiumUpdatePacket(false, regName, entryName, json);
    }

    public static CompendiumUpdatePacket UpdateEntry(string regName, string entryName, JsonElement json)
    {
        return AddEntry(regName, entryName, json);
    }

    protected CompendiumUpdatePacket(bool remove, string registryName, string dataName, JsonElement? json)
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
        if (Remove)
        {
            Json = null;
            return;
        }
        
        ulong count = stream.ReadUInt64();
        byte[] data = stream.ReadExactly((uint)count);
        string str = new (data.Select(b => (char)b).ToArray());
        var parsed = JsonDocument.Parse(str);
        if (parsed == null)
            throw new Exception("Failed to parse compendium json data!");
        Json = parsed.RootElement;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        stream.WriteBoolean(Remove);
        stream.WriteString(RegistryName);
        stream.WriteString(DataName);

        if (!Remove)
        {
            string str = Json!.Value.ToString();
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
    public readonly EntityRef? Sender;
    public readonly EntityRef? Recipient;
    public readonly string Message;

    public PrivateMessagePacket(Entity? sender, Entity? recipient, string message)
    {
        Sender = sender != null ? new EntityRef(sender) : null;
        Recipient = recipient != null ? new EntityRef(recipient) : null;
        Message = message;
    }

    public PrivateMessagePacket(Stream stream)
    {
        if (stream.ReadBoolean())
            Sender = new EntityRef(stream);
        if (stream.ReadBoolean())
            Recipient = new EntityRef(stream);
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
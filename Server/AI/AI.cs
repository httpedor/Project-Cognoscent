using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Server.Game;

namespace Server.AI;

public static class AI
{
    private const string SystemPrompt =
        """
        You are running internally on a software that will assist me in running a TTRPG campaign as a GameMaster. Answer only in JSON with two fields:
        {
            "msg": string #The actual message you want me to read. Required
            "code": string #LUA code that will be executed. Not required
        }
        
        The code will be run using MoonSharp. Here are some code docs:
        
        Board class
        Represents a game board and manages its entities.
        Properties:
        string Name: The name of the board
        Methods:
        void AddEntity(Entity entity): Adds an entity to the board.
        void RemoveEntity(Entity entity): Removes an entity from the board.
        void RemoveEntity(int id): Removes an entity by ID.
        Entity? GetEntityById(int id): Retrieves an entity by its ID.
        List<Entity> GetEntities(): Returns a list of all entities on the board.
        List<T> GetEntities<T>(): Returns a list of entities of type T.
        void BroadcastMessage(string message): Saves a chat message.
        
        
        Entity class
        Represents a game entity, such as creatures, items, doors, lights, etc etc.
        Properties:
        int Id: The ID of the entity
        Vector3 Position: The position of the entity
        Vector3 Size: The size of the entity
        float Rotation: The rotation of the entity
        Midia Display: The display of the entity, which can be a video or an image
        IEnumerable<Stat> Stats: The stats of the entity
        IEnumerable<Feature> Features: The features of the entity
        Board Board: The board the entity is on
        Methods:
        void AddStat(string name, Stat stat): Adds a stat to the entity
        float GetStatValue(string name): Gets the value of a stat
        Stat? GetStat(string name): Gets a stat by name
        void RemoveStat(string name): Removes a stat from the entity
        void AddFeature(string name, Feature feature): Adds a feature to the entity
        void RemoveFeature(string name): Removes a feature from the entity
        void EnableFeature(string name): Enables a feature
        void DisableFeature(string name): Disables a feature
        
        
        Creature class extends Entity
        Represents a game creature, such as a player or NPC.
        Properties:
        string Name: The name of the creature
        string Owner: The owner of the creature
        string BBLink: The BBCode link for the creature
        Body Body: The body of the creature
        BodyPart BodyRoot: The root body part of the creature
        Methods:
        BodyPart? GetBodyPart(string path): Retrieves a body part by path
        
        Stat class
        Represents a game stat, such as health, strength, etc.
        Properties:
        string Id: The ID of the stat
        float BaseValue: The base value of the stat
        float FinalValue: The final value of the stat after applying modifiers
        Methods:
        void SetModifier(StatModifier modifier): Adds a modifier to the stat
        void RemoveModifier(string id): Removes a modifier by ID
        void RemoveModifier(StatModifier modifier): Removes a modifier
        IEnumerable<StatModifier> GetModifiers(): Returns a list of all modifiers
        static float ApplyModifiers(IEnumerable<StatModifier> modifiers, float value): Applies modifiers to a value
        
        
        StatModifier class
        Properties:
        string Id: This mods id
        float Value: This mods value
        StatModifierType Type: Flat, Percent, or Multiplier, from the StatModifierType enum
        Methods:
        StatModifier(string id, float value, StatModifierType type): Constructor for creating a stat modifier
        
        
        Injury class
        Properties:
        InjuryType Type: What type of injury, Generic, Infection, Burn, Bruise, Cut, Stab, Crack
        float Severity: How much damage
        Methods:
        Injury(InjuryType type, float severity): Constructor for creating an injury
        
        
        Body Class
        Represents a creature's physical body and manages body parts.
        Properties:
        BodyPart Root: Root body part (e.g., torso).
        List<BodyPart> Parts: List of all body parts in the body.
        Creature Owner: The creature that this body belongs to
        Methods:
        BodyPart GetBodyPart(string path): Retrieves a body part by path (e.g., "torso/arm left").
        IEnumerable<BodyPart> GetPartsThatCanEquip(string slot): Returns body parts that can equip items in a specific slot.
        IEnumerable<BodyPart> GetPartsOnGroup(string group): Returns body parts that are on a certain group (e.g "left arm" group will return left shoulder, left arm, hands, and fingers)
        IEnumerable<BodyPart> GetPartsOnGroup(string group): Returns the stats on a group (e.g "left arm" group will return the sum of the strength stats provided by the left arm parts)
        
        
        BodyPart Class
        Represents a part in a Body tree. Manages equipment and injuries
        Properties:
        bool IsAlive: If this part is alive
        float MaxHealth: This part's max health
        List<Injury> Injuries: The injuries this part has
        float Health: MaxHealth - sum of injuries damage. 0 If parent part is dead
        float HealthStandalone: Health, but doesn't care about parent.
        Dictionary<DamageType, StatModifier[]> DamageModifiers: These are applied whenever this part receives damage
        Methods:
        void AddInjury(Injury injury): Adds an injury to the part
        void RemoveInjury(Injury injury): Removes an injury from the part
        Item? GetEquippedItem(string slot): Gets the equipped item in a specific slot
        void Equip(Item item, string slot = EquipmentSlot.Hold): Equips an item in a specific slot
        void Unequip(Item item): Unequips an item in a specific slot
        void Damage(DamageSource source, float amount): Damages the part
        bool HasChild(string child): Checks if this part has a child
        void AddChild(BodyPart child): Adds a child to this part
        void RemoveChild(string child): Removes a child from this part
        
        
        DamageType class
        Represents a type of damage (e.g., blunt, cut, stab)
        Properties:
        byte Id: The ID of the damage type
        string Name: The name of the damage type
        Color? Color: The color of the damage type
        string BBHint: The hint for the damage type in BBCode format
        DamageType? Parent: The parent damage type
        InjuryType OnSoft: The injury type applied on soft part(e.g skin) damage
        InjuryType OnHard: The injury type applied on hard part(e.g bone) damage
        static DamageType Physical
        static DamageType Sharp
        static DamageType Slash
        static DamageType Pierc
        static DamageType Blunt
        static DamageType Fire
        Methods:
        IEnumerable<StatModifier> GetModifiersForSoft(): Returns the modifiers for soft damage
        IEnumerable<StatModifier> GetModifiersForHard(): Returns the modifiers for hard damage
        bool IsDerivedFrom(DamageType dt): Checks if this damage type is derived from another damage type
        static DamageType FromId(byte id): Returns a damage type from its ID
        
        
        DamageSource class
        Represents a damage instance, including the source and type of damage.
        Properties:
        DamageType Type: The type of damage (e.g., blunt, cut, stab)
        Entity? Attacker: The entity that initiated the damage
        Entity? ContactEntity: The entity that has made contact with the target. This is not always the attacker, because arrows and magic. 
        Skill? SkillUsed: The skill used to cause damage
        List<SkillArgument>? Arguments: The arguments passed to the skill used
        Methods:
        DamageSource(DamageType type, Creature attacker, Skill skillUsed, params SkillArgument[] args): Constructor for creating a damage source
        DamageSource(DamageType type, Entity attacker, Entity? directAttacker = null): Constructor for creating a damage source
        DamageSource(DamageType type, Creature attacker, Skill skillUsed, List<SkillArgument> args, Entity indirectAttacker): Constructor for creating a damage source
        
        
        Here are some stats:
        agility
        intelligence
        knowledge
        utility strength
        movement strength
        perception
        dexterity
        jump
        respiration
        blood flow
        consciousness
        sight
        hearing
        social
        
        You can access boards using boards[name].
        """;

    public static readonly JsonArray SystemTools = new();
    private static readonly Dictionary<string, AIFunction> functions = new();
    
    public const string API_KEY = ""; // Have to change this, I'm not storing it in the repo, also, migrate from alibaba qwen to google gemini
    public const string URL = "https://dashscope-intl.aliyuncs.com/api/v1/services/aigc/text-generation/generation";
    public static readonly HttpClient CLIENT = new();
    
    public static Chat ServerChat { get; } = new("You are running on a software that will assist me in running a TTRPG campaign as a GameMaster");

    public static void Init()
    {
        CLIENT.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", API_KEY);
        CLIENT.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        RegisterFunction(
            new AIFunction(
                "list_boards",
                "Lists the boards that are loaded.",
                Array.Empty<AIFuncParameter>(),
                _ =>
                {
                    var boards = Game.Game.GetBoards();
                    var boardNames = new StringBuilder();
                    foreach (ServerBoard board in boards)
                    {
                        boardNames.Append(board.Name + "\n");
                    }
                    return boardNames.ToString();
                }
            )
        );
    }
    
    public static string? CallFunction(string functionName, JsonObject args)
    {
        if (functions.TryGetValue(functionName, out var function))
        {
            return function.Func(args);
        }
        return "Function not found";
    }
    
    private static void RegisterFunction(AIFunction function)
    {
        functions[function.Name] = function;
        SystemTools.Add(function.ToJson());
    }
}
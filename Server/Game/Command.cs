using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Entities.Components.Inventory;
using Rpg.Skills;
using Server.Game.Import;
using Server.Network;

namespace Server.Game;

// ---------------------------------------------------------------------------
//  CommandArgs – type-safe accessor for parsed command arguments
// ---------------------------------------------------------------------------
public sealed class CommandArgs
{
    private readonly object[] _args;
    public RpgClient? Client { get; }
    public int Length => _args.Length;

    internal CommandArgs(RpgClient? client, object[] args)
    {
        Client = client;
        _args = args;
    }

    /// <summary>Get the argument at <paramref name="index"/> cast to <typeparamref name="T"/>.</summary>
    public T Get<T>(int index) => (T)_args[index];

    /// <summary>Try to get the argument at <paramref name="index"/>. Returns false when the index is out of range.</summary>
    public bool TryGet<T>(int index, out T value)
    {
        if (index < _args.Length)
        {
            value = (T)_args[index];
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Convenience: given an Entity argument at <paramref name="entityIndex"/>,
    /// retrieve the component of type <typeparamref name="T"/> from it, or null.
    /// </summary>
    public T? GetComponent<T>(int entityIndex) where T : Component
    {
        Entity e = Get<Entity>(entityIndex);
        return e.GetComponent<T>();
    }

    /// <summary>
    /// Convenience: given an Entity argument at <paramref name="entityIndex"/>,
    /// retrieve the component of type <typeparamref name="T"/> or throw a user-friendly error.
    /// </summary>
    public T RequireComponent<T>(int entityIndex) where T : Component
    {
        Entity e = Get<Entity>(entityIndex);
        return e.GetComponent<T>()
            ?? throw new CommandException($"Entity {e.Id} ({e.Name}) does not have a {typeof(T).Name} component.");
    }
}

/// <summary>Thrown by command handlers to produce a user-visible error (no stack trace noise).</summary>
public sealed class CommandException : Exception
{
    public CommandException(string message) : base(message) { }
}

// ---------------------------------------------------------------------------
//  CommandParam – describes one formal parameter of a command
// ---------------------------------------------------------------------------
public sealed class CommandParam
{
    public string Name { get; }
    public Type Type { get; }
    public bool Required { get; }
    public Func<IEnumerable<string>>? SuggestionProvider { get; }

    public CommandParam(string name, Type type, bool required, Func<IEnumerable<string>>? suggestionProvider = null)
    {
        Name = name;
        Type = type;
        Required = required;
        SuggestionProvider = suggestionProvider;
    }

    /// <summary>Display token for usage string.</summary>
    public string UsageToken => Required ? $"<{Name}>" : $"[{Name}]";
}

// ---------------------------------------------------------------------------
//  Command – the core command registration
// ---------------------------------------------------------------------------
public sealed class Command
{
    // ---- Static registry ---------------------------------------------------
    private static int _registeredCount;
    private static readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> _nonAliases = new();
    private const int MaxSuggestionCount = 8;

    // ---- Instance properties -----------------------------------------------
    public string Name { get; }
    public string Description { get; }
    public string[] Aliases { get; }
    public CommandParam[] Params { get; }
    public Func<CommandArgs, string> Callback { get; }

    public string Usage
    {
        get
        {
            if (Params.Length == 0) return "";
            return string.Join(" ", Params.Select(p => p.UsageToken));
        }
    }

    internal Command(string name, string description, string[] aliases, CommandParam[] parameters, Func<CommandArgs, string> callback)
    {
        Name = name;
        Description = description;
        Aliases = aliases;
        Params = parameters;
        Callback = callback;
    }

    // ============================== BUILDER =================================
    public class Builder
    {
        private readonly string _name;
        private string _description = "";
        private readonly List<string> _aliases = new();
        private readonly List<CommandParam> _params = new();
        private Func<CommandArgs, string> _callback = _ => "";

        public Builder(string name) => _name = name;

        public Builder Desc(string description) { _description = description; return this; }
        public Builder Alias(params string[] aliases) { _aliases.AddRange(aliases); return this; }

        // ---- Parameter helpers ----------------------------------------------

        /// <summary>Add a required parameter.</summary>
        public Builder Arg<T>(string name, Func<IEnumerable<string>>? suggestions = null)
        {
            _params.Add(new CommandParam(name, typeof(T), required: true, suggestions));
            return this;
        }

        /// <summary>Add an optional parameter.</summary>
        public Builder OptArg<T>(string name, Func<IEnumerable<string>>? suggestions = null)
        {
            _params.Add(new CommandParam(name, typeof(T), required: false, suggestions));
            return this;
        }

        /// <summary>
        /// Add a required parameter for a specific Component type.
        /// Resolves Entity→Component automatically. At suggestion time, only entities
        /// that actually have this component will be shown.
        /// </summary>
        public Builder ComponentArg<T>(string? name = null) where T : Component
        {
            name ??= typeof(T).Name.ToLower();
            _params.Add(new CommandParam(name, typeof(T), required: true));
            return this;
        }

        /// <summary>Variadic trailing parameter (string). The last param repeats for extra args.</summary>
        public Builder Variadic(string name, Func<IEnumerable<string>>? suggestions = null)
        {
            _params.Add(new CommandParam(name + "...", typeof(string), required: false, suggestions));
            return this;
        }

        // ---- Callback -------------------------------------------------------
        public Builder Runs(Func<CommandArgs, string> cb) { _callback = cb; return this; }

        // ---- Build & register -----------------------------------------------
        public Command Build() => new(_name, _description, _aliases.ToArray(), _params.ToArray(), _callback);

        public Command Register()
        {
            var cmd = Build();
            Command.RegisterCommand(cmd);
            return cmd;
        }
    }

    /// <summary>Fluent entry-point for defining a new command.</summary>
    public static Builder Define(string name) => new(name);

    // ========================= REGISTRATION =================================
    private static readonly HashSet<Type> _acceptedPrimitives =
    [
        typeof(ServerBoard),
        typeof(Entity),
        typeof(string),
        typeof(double),
        typeof(bool),
        typeof(int),
        typeof(byte)
    ];

    private static bool IsAcceptedType(Type t) =>
        _acceptedPrimitives.Contains(t) || t.IsSubclassOf(typeof(Component));

    public static void RegisterCommand(Command cmd)
    {
        foreach (var p in cmd.Params)
        {
            if (!IsAcceptedType(p.Type))
                throw new ArgumentException($"Invalid argument type {p.Type.Name} in command '{cmd.Name}'");
        }
        _commands[cmd.Name] = cmd;
        _nonAliases.Add(cmd.Name.ToLower());
        foreach (string alias in cmd.Aliases)
            _commands[alias] = cmd;
        _registeredCount++;
    }

    public static void UnregisterCommand(Command cmd)
    {
        _commands.Remove(cmd.Name);
        _nonAliases.Remove(cmd.Name.ToLower());
        foreach (string alias in cmd.Aliases)
            _commands.Remove(alias);
    }

    public static void UnregisterCommand(string name)
    {
        if (_commands.TryGetValue(name, out var cmd))
            UnregisterCommand(cmd);
    }

    // ========================= EXECUTION ====================================
    public static void ExecuteCommand(RpgClient? client, string? cmdString)
    {
        string[] parts = cmdString?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length == 0) return;

        string name = parts[0];
        if (!_commands.TryGetValue(name, out var cmd))
        {
            Logger.LogError($"Unknown command '{name}'");
            return;
        }

        string[] argStrings = parts.Length > 1 ? parts[1..] : [];
        int requiredCount = cmd.Params.Count(p => p.Required);
        if (argStrings.Length < requiredCount)
        {
            Logger.LogError($"Not enough arguments. Usage: {cmd.Name} {cmd.Usage}");
            return;
        }

        // Parse arguments
        object[] parsed = new object[argStrings.Length];
        for (int i = 0; i < argStrings.Length; i++)
        {
            CommandParam param = GetParam(cmd, i);
            Type t = param.Type;

            try
            {
                parsed[i] = ParseArgument(t, argStrings[i], parsed, i, cmd);
            }
            catch (CommandException ce)
            {
                Logger.LogError(ce.Message);
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Invalid argument '{argStrings[i]}' for <{param.Name}> ({t.Name}): {ex.Message}");
                return;
            }
        }

        try
        {
            var args = new CommandArgs(client, parsed);
            string result = cmd.Callback(args);
            if (!string.IsNullOrEmpty(result))
                Logger.Log(result);
        }
        catch (CommandException ce)
        {
            Logger.LogError(ce.Message);
        }
        catch (Exception e)
        {
            Logger.LogError("Error executing command: " + e.Message);
            Logger.LogError(e.ToString());
        }
    }

    private static CommandParam GetParam(Command cmd, int index)
    {
        if (cmd.Params.Length == 0)
            throw new CommandException("No arguments expected.");
        if (index < cmd.Params.Length)
            return cmd.Params[index];
        return cmd.Params[^1]; // variadic: repeat last param type
    }

    private static object ParseArgument(Type t, string raw, object[] previouslyParsed, int currentIndex, Command cmd)
    {
        // Primitives
        if (t == typeof(string)) return raw;
        if (t == typeof(int)) return int.Parse(raw);
        if (t == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);
        if (t == typeof(byte)) return byte.Parse(raw);
        if (t == typeof(bool)) return raw is "true" or "1" or "yes" or "y" or "t";

        // ServerBoard
        if (t == typeof(ServerBoard))
        {
            if (raw == "" && Game.GetBoards().Count == 1)
                return Game.GetBoards()[0];
            return Game.GetBoard(raw)
                ?? throw new CommandException($"Unknown board '{raw}'");
        }

        // Entity
        if (t == typeof(Entity))
        {
            return ResolveEntity(raw, previouslyParsed);
        }

        // Component subclass – resolve as Entity arg, then extract the component
        if (t.IsSubclassOf(typeof(Component)))
        {
            Entity entity = ResolveEntity(raw, previouslyParsed);
            uint compId = Component.GetComponentId(t);
            var comp = entity.GetComponent(compId);
            if (comp == null)
                throw new CommandException($"Entity {entity.Id} ({entity.Name}) does not have a {t.Name} component.");
            return comp;
        }

        throw new CommandException($"Unsupported argument type {t.Name}");
    }

    private static Entity ResolveEntity(string raw, object[] previouslyParsed)
    {
        ServerBoard? board = null;
        // Find board context from previously parsed args
        for (int j = 0; j < previouslyParsed.Length; j++)
        {
            if (previouslyParsed[j] is ServerBoard sb) { board = sb; break; }
        }
        board ??= Game.GetBoards().Count == 1 ? Game.GetBoards()[0] : null;
        if (board == null)
            throw new CommandException("No board context found for entity argument. Specify a board first.");

        string idPart = raw;
        int colon = raw.IndexOf(':');
        if (colon >= 0) idPart = raw[..colon];
        if (!int.TryParse(idPart, out int entId))
            throw new CommandException($"Invalid entity id '{raw}'. Expected <id> or <id>:<name>.");
        return board.GetEntityById(entId)
            ?? throw new CommandException($"Entity {entId} not found in board '{board.Name}'.");
    }

    // ========================= SUGGESTIONS ==================================
    /// <summary>Returns the description for a registered command name (or alias), or null if not found.</summary>
    public static string? GetDescription(string name)
        => _commands.TryGetValue(name, out var cmd) ? cmd.Description : null;

    public static IReadOnlyList<string> GetSuggestions(string? input)
    {
        input ??= string.Empty;
        string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool endsWithSpace = input.EndsWith(' ');

        int tokenIndex;
        string currentToken;

        if (tokens.Length == 0)          { tokenIndex = 0; currentToken = input; }
        else if (endsWithSpace)          { tokenIndex = tokens.Length; currentToken = ""; }
        else                             { tokenIndex = tokens.Length - 1; currentToken = tokens[^1]; }

        // Completing the command name itself
        if (tokenIndex == 0 && !endsWithSpace)
            return FilterCandidates(_nonAliases.Concat(_commands.Keys).Distinct(StringComparer.OrdinalIgnoreCase), currentToken);

        if (tokens.Length == 0)
            return FilterCandidates(_nonAliases.Concat(_commands.Keys).Distinct(StringComparer.OrdinalIgnoreCase), currentToken);

        if (!_commands.TryGetValue(tokens[0], out var command))
            return FilterCandidates(_nonAliases, currentToken);

        int argIndex = tokenIndex - 1;
        if (argIndex < 0)
            return [];

        return GetArgumentSuggestions(command, argIndex, currentToken, tokens);
    }

    private static IReadOnlyList<string> GetArgumentSuggestions(Command command, int argIndex, string currentToken, string[] tokens)
    {
        CommandParam? param = (argIndex < command.Params.Length)
            ? command.Params[argIndex]
            : command.Params.Length > 0 ? command.Params[^1] : null;

        if (param == null) return [];

        Type t = param.Type;

        // Custom suggestion provider takes priority
        if (param.SuggestionProvider != null)
            return FilterCandidates(param.SuggestionProvider(), currentToken);

        // ServerBoard
        if (t == typeof(ServerBoard))
            return FilterCandidates(Game.GetBoards().Select(b => b.Name), currentToken);

        // Bool
        if (t == typeof(bool))
            return FilterCandidates(["false", "true"], currentToken);

        // Entity
        if (t == typeof(Entity))
        {
            ServerBoard? board = ResolveBoardFromTokens(command, tokens, argIndex);
            if (board != null)
                return FilterCandidates(board.GetEntities().Select(e => $"{e.Id}:{e.Name}"), currentToken);
            return [];
        }

        // Component types – show only entities that have this component
        if (t.IsSubclassOf(typeof(Component)))
        {
            ServerBoard? board = ResolveBoardFromTokens(command, tokens, argIndex);
            if (board != null)
            {
                uint compId = Component.GetComponentId(t);
                return FilterCandidates(
                    board.GetEntitiesWithComponent(compId).Select(e => $"{e.Id}:{e.Name}"),
                    currentToken);
            }
            return [];
        }

        // string with no custom provider - check legacy special cases
        if (t == typeof(string))
        {
            if (command.Name.Equals("uvttload", StringComparison.OrdinalIgnoreCase) && argIndex == 2)
            {
                if (tokens.Length > 2 && tokens[2].Equals("append", StringComparison.OrdinalIgnoreCase))
                    return FilterCandidates(Game.GetBoards().Select(b => b.Name), currentToken);
            }
            return [];
        }

        return [];
    }

    private static ServerBoard? ResolveBoardFromTokens(Command command, string[] tokens, int currentArgIndex)
    {
        int argsAvailable = Math.Min(tokens.Length - 1, currentArgIndex);
        for (int pos = 0; pos < argsAvailable; pos++)
        {
            var p = (pos < command.Params.Length) ? command.Params[pos] : null;
            if (p?.Type != typeof(ServerBoard)) continue;

            string boardName = tokens[pos + 1];
            if (string.IsNullOrEmpty(boardName)) continue;
            var board = Game.GetBoard(boardName);
            if (board != null) return board;
        }
        // Fallback: single board loaded
        if (Game.GetBoards().Count == 1) return Game.GetBoards()[0];
        return null;
    }

    private static IReadOnlyList<string> FilterCandidates(IEnumerable<string> source, string prefix)
    {
        prefix ??= string.Empty;
        return source
            .Where(s => !string.IsNullOrEmpty(s) && (prefix.Length == 0 || MatchesCandidate(s, prefix)))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestionCount)
            .ToList();
    }

    /// <summary>
    /// Checks whether a candidate matches the user's typed token.
    /// Matches by prefix on the full string, or — for "id:name" candidates — also
    /// matches if the token is a prefix of the name part (after the colon).
    /// </summary>
    private static bool MatchesCandidate(string candidate, string token)
    {
        if (candidate.Contains(token, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // ========================= INIT =========================================
    public static void Init()
    {
        Define("help")
            .Desc("Displays a list of commands, or help for a specific command.")
            .OptArg<string>("command", () => _nonAliases)
            .Runs(args =>
            {
                if (args.Length == 0)
                {
                    string ret = "";
                    foreach (string name in _nonAliases)
                    {
                        if (_commands.TryGetValue(name, out var c))
                            ret += $"{name} {c.Usage} - {c.Description}\n";
                    }
                    return ret;
                }
                string cmdName = args.Get<string>(0);
                if (!_commands.TryGetValue(cmdName, out var cmd))
                    return $"Unknown command '{cmdName}'";
                return $"{cmd.Name} {cmd.Usage} - {cmd.Description}";
            })
            .Register();

        Define("who")
            .Desc("Displays a list of connected clients.")
            .Alias("online", "players")
            .Runs(_ =>
            {
                string ret = "";
                foreach (var client in Network.Manager.Clients.Values)
                    ret += $"{client.Username} - {client.IpAddress}\n";
                return ret;
            })
            .Register();

        Define("boardlist")
            .Desc("Lists all boards loaded.")
            .Alias("showboards", "listboards")
            .Runs(_ =>
            {
                string ret = "";
                foreach (var board in Game.GetBoards())
                    ret += board.Name + "\n";
                return ret;
            })
            .Register();

        Define("boardrename")
            .Desc("Renames a board.")
            .Alias("renameboard", "boardname")
            .Arg<ServerBoard>("board")
            .Arg<string>("name")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                string newName = args.Get<string>(1);
                string oldName = board.Name;
                if (string.IsNullOrWhiteSpace(newName))
                    throw new CommandException("Invalid name.");
                Game.RemoveBoard(oldName);
                Game.AddBoard(board, newName);
                return $"Board '{oldName}' renamed to '{newName}'";
            })
            .Register();

        Define("boardsave")
            .Desc("Saves a board to a file.")
            .Alias("saveboard", "save")
            .Arg<ServerBoard>("board")
            .OptArg<string>("filename")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                string fileName = args.TryGet<string>(1, out var f)
                    ? f
                    : board.Name.ToLower().FirstCharToUpper() + ".board";
                Directory.CreateDirectory("Boards");
                try
                {
                    using var fs = File.OpenWrite("Boards/" + fileName);
                    board.ToBytes(fs);
                    fs.Flush();
                }
                catch (IOException e)
                {
                    throw new CommandException("Error saving board: " + e.Message);
                }
                return $"Board '{fileName}' saved.";
            })
            .Register();

        Define("boardload")
            .Desc("Loads a board from a file.")
            .Alias("loadboard", "load")
            .Arg<string>("filename")
            .Runs(args =>
            {
                string fileName = args.Get<string>(0).Replace(".board", "");
                try
                {
                    using var fs = File.OpenRead("Boards/" + fileName + ".board");
                    var b = new ServerBoard(fs);
                    Game.AddBoard(b);
                    return $"Board '{b.Name}' loaded!";
                }
                catch (IOException e)
                {
                    throw new CommandException("Error loading board: " + e.Message);
                }
            })
            .Register();

        Define("boardunload")
            .Desc("Unloads a board from memory.")
            .Alias("unloadboard", "unload")
            .Arg<ServerBoard>("board")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                Game.RemoveBoard(board.Name);
                return $"Board '{board.Name}' unloaded.";
            })
            .Register();

        Define("uvttloadmultiple")
            .Desc("Loads a board from multiple UVTT floors.")
            .Alias("loaduvttmultiple", "uvttmultiple", "uvttloadmany", "loaduvttmany", "uvttmany")
            .Arg<string>("name")
            .Variadic("floors")
            .Runs(args =>
            {
                string boardName = args.Get<string>(0);
                var board = new ServerBoard(boardName);
                for (int i = 1; i < args.Length; i++)
                {
                    string fileName = args.Get<string>(i);
                    string json;
                    try { json = File.ReadAllText(fileName); }
                    catch (FileNotFoundException) { throw new CommandException($"File '{fileName}' not found."); }
                    catch (Exception e) { throw new CommandException($"Error reading '{fileName}': {e.Message}"); }

                    var ents = new List<Entity>();
                    Floor? f = Uvtt.LoadFloorFromUvttJson(json, ents);
                    if (f == null) throw new CommandException($"Error loading floor '{fileName}'");

                    board.AddFloor(f);
                    foreach (var entity in ents)
                    {
                        if (entity.TryGetComponent<Token>(out var token))
                            token.Position = new Vector3(token.Position.X, token.Position.Y, board.GetFloorCount() - 1);
                        board.AddEntity(entity);
                    }
                }
                Game.AddBoard(board);
                return $"Loaded board '{boardName}' with {board.GetFloorCount()} floors.";
            })
            .Register();

        Define("uvttload")
            .Desc("Loads a board from a UVTT file. Mode: new (default) or append.")
            .Alias("loaduvtt", "uvtt")
            .Arg<string>("file", () => Directory.EnumerateFiles(".")
                .Where(file => file.EndsWith("json") || file.EndsWith("uvtt"))
                .Select(file => file.Substring(2)))
            .OptArg<string>("mode", () => ["new", "append"])
            .OptArg<string>("board")
            .Runs(args =>
            {
                string fileName = args.Get<string>(0);
                string json;
                try { json = File.ReadAllText(fileName); }
                catch (FileNotFoundException) { throw new CommandException($"File '{fileName}' not found."); }
                catch (Exception e) { throw new CommandException($"Error reading file: {e.Message}"); }

                string mode = args.TryGet<string>(1, out var m) ? m : "new";
                switch (mode)
                {
                    case "new":
                    {
                        var b = Uvtt.LoadBoardFromUvttJson(json, fileName.FirstCharToUpper().Replace(".uvtt", ""));
                        if (b == null) throw new CommandException("Error loading board from UVTT.");
                        Game.AddBoard(b);
                        return $"Loaded board '{b.Name}'";
                    }
                    case "append":
                    {
                        var ents = new List<Entity>();
                        Floor? f = Uvtt.LoadFloorFromUvttJson(json, ents);
                        if (f == null) throw new CommandException("Error loading floor from UVTT.");
                        if (Game.GetBoards().Count == 0) throw new CommandException("No boards loaded.");
                        if (!args.TryGet<string>(2, out var boardName)) throw new CommandException("No board specified for append.");
                        var board = Game.GetBoard(boardName) ?? throw new CommandException($"Board '{boardName}' not found.");
                        board.AddFloor(f);
                        foreach (var entity in ents)
                        {
                            if (entity.TryGetComponent<Token>(out var token))
                                token.Position = new Vector3(token.Position.X, token.Position.Y, board.GetFloorCount() - 1);
                            board.AddEntity(entity);
                        }
                        return $"Loaded floor to index {board.GetFloorCount() - 1} in board '{board.Name}'";
                    }
                    default:
                        throw new CommandException($"Unknown mode '{mode}'. Expected 'new' or 'append'.");
                }
            })
            .Register();

        Define("entitylist")
            .Desc("Lists all entities on a board.")
            .Alias("listentities", "showentities", "entities", "entityshow")
            .Arg<ServerBoard>("board")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                string ret = "";
                foreach (var e in board.GetEntities())
                {
                    string desc = "";
                    if (e.TryGetComponent<Body>(out var body))
                    {
                        if (body.IsAlive)
                            desc = "Creature";
                        else
                            desc = $"Body";
                    }
                    else if (e.HasComponent(Item.ID))
                        desc = $"Item";
                    else if (e.HasComponent(BodyPart.ID))
                        desc = $"Body Part";
                    else if (e.HasComponent(Light.ID))
                        desc = $"Light Source";
                    else if (e.HasComponent(Door.ID))
                        desc = $"Door";
                    if (e.TryGetComponent<Token>(out var token))
                        desc += $" at {token.Position}";
                    ret += $"{e.Id} - {e.Name} - {desc}";
                    ret += "\n";
                }
                return ret;
            })
            .Register();

        Define("entityremove")
            .Desc("Removes an entity from a board.")
            .Alias("removeentity", "deleteentity", "entitydelete", "entitydestroy", "destroyentity")
            .Arg<ServerBoard>("board")
            .Arg<Entity>("entity")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                var ent = args.Get<Entity>(1);
                ent.Destroy();
                return "Entity destroyed.";
            })
            .Register();

        Define("chatclear")
            .Desc("Clears the chat.")
            .Arg<ServerBoard>("board")
            .Runs(args =>
            {
                args.Get<ServerBoard>(0).GetChatHistory().Clear();
                return "Chat cleared.";
            })
            .Register();

        Define("entityowner")
            .Desc("Sets or displays the owner of an entity.")
            .Alias("entitysetowner", "creatureowner", "creaturesetowner", "setcreatureowner", "creatureownerset", "entityownerset")
            .Arg<ServerBoard>("board")
            .Arg<Entity>("entity")
            .OptArg<string>("owner")
            .Runs(args =>
            {
                var e = args.Get<Entity>(1);
                if (args.TryGet<string>(2, out var owner))
                {
                    e.Owner = owner;
                    return "Owner set.";
                }
                return e.Owner == null ? $"{e.Id} has no owner" : $"{e.Id}'s owner is: {e.Owner}";
            })
            .Register();

        // ---- entitypos: uses Token component arg for type safety ----
        Define("entitypos")
            .Desc("Sets or displays an entity's position.")
            .Alias("pos", "entitysetpos", "setentitypos")
            .Arg<ServerBoard>("board")
            .ComponentArg<Token>("entity")
            .OptArg<double>("x")
            .OptArg<double>("y")
            .OptArg<double>("z")
            .Runs(args =>
            {
                var token = args.Get<Token>(1);
                if (args.Length == 2)
                    return token.Position.ToString();
                if (args.Length == 5)
                {
                    token.Position = new Vector3((float)args.Get<double>(2), (float)args.Get<double>(3), (float)args.Get<double>(4));
                    return $"Position set to {token.Position}";
                }
                if (args.Length == 4)
                {
                    token.Position = new Vector3((float)args.Get<double>(2), (float)args.Get<double>(3), token.Position.Z);
                    return $"Position set to {token.Position}";
                }
                throw new CommandException("Invalid argument count. Provide 0, 2, or 3 coordinates.");
            })
            .Register();

        Define("playerkick")
            .Desc("Kicks a player from the server.")
            .Alias("kick")
            .Arg<string>("player", () => Network.Manager.Clients.Keys)
            .Runs(args =>
            {
                string username = args.Get<string>(0);
                var client = Network.Manager.GetClient(username)
                    ?? throw new CommandException($"Player '{username}' not found.");
                client.Disconnect();
                return $"{client.Username} kicked.";
            })
            .Register();

        // ---- entityimage: uses Token component arg ----
        Define("entityimage")
            .Desc("Sets the image of an entity.")
            .Alias("entitysetimage", "setentityimage")
            .Arg<ServerBoard>("board")
            .ComponentArg<Token>("entity")
            .Arg<string>("image")
            .Runs(args =>
            {
                var token = args.Get<Token>(1);
                string str = args.Get<string>(2);
                if (string.IsNullOrWhiteSpace(str))
                    throw new CommandException("Invalid image.");
                token.Midia = new Midia(str);
                return "Image set.";
            })
            .Register();

        // ---- entityrotation: uses Token component arg ----
        Define("entityrotation")
            .Desc("Sets or reads the rotation of an entity.")
            .Alias("entitysetrotation", "setentityrotation", "rotationentity", "rotationsetentity")
            .Arg<ServerBoard>("board")
            .ComponentArg<Token>("entity")
            .OptArg<int>("rotation")
            .Runs(args =>
            {
                var token = args.Get<Token>(1);
                if (args.Length == 2)
                    return $"Entity rotation: {token.Rotation}";
                token.Rotation = args.Get<int>(2);
                return "Set entity rotation.";
            })
            .Register();

        Define("boardtick")
            .Desc("Ticks a board, or checks which tick it's on.")
            .Alias("tick")
            .Arg<ServerBoard>("board")
            .OptArg<int>("ticks")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                if (args.Length < 2)
                    return $"Board is at tick {board.CurrentTick}";
                int ticks = args.Get<int>(1);
                for (int i = 0; i < ticks; i++) board.Tick();
                return $"Board ticked {ticks} times.";
            })
            .Register();

        Define("boardcombat")
            .Desc("Checks or toggles the board's combat mode.")
            .Alias("combat", "combatmode", "turn", "turnmode")
            .Arg<ServerBoard>("board")
            .OptArg<bool>("state")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                if (args.Length < 2)
                    return $"Combat mode {(board.TurnMode ? "enabled" : "disabled")}";
                bool enabled = args.Get<bool>(1);
                if (enabled) board.StartTurnMode(); else board.EndTurnMode();
                return $"Combat mode {(board.TurnMode ? "enabled" : "disabled")}";
            })
            .Register();

        // ---- entityskills: uses SkillExecutor component arg directly ----
        Define("entityskills")
            .Desc("Lists current skills of an entity.")
            .Alias("skills", "listentityskills", "listskills")
            .Arg<ServerBoard>("board")
            .ComponentArg<SkillExecutor>("entity")
            .Runs(args =>
            {
                var exec = args.Get<SkillExecutor>(1);
                string ret = "";
                foreach (var action in exec.ActiveSkills.Values)
                    ret += $"{action.Skill.GetName()} - {action.Skill.GetDescription()}\n";
                return string.IsNullOrEmpty(ret) ? "No active skills." : ret;
            })
            .Register();

        Define("entitystats")
            .Desc("Lists entity stats.")
            .Alias("liststats", "stats")
            .Arg<ServerBoard>("board")
            .ComponentArg<StatsContainer>("entity")
            .Runs(args =>
            {
                var holder = args.Get<StatsContainer>(1);
                string ret = "";
                foreach (var stat in holder.Stats)
                {
                    ret += $"{stat.Id} - {stat.FinalValue} (base:{stat.BaseValue}); ";
                    var mods = stat.GetModifiers().ToList();
                    ret += $"{mods.Count} mods: ";
                    foreach (var mod in mods)
                        ret += $"({mod.Id},{mod.Type},{mod.Value},{mod.DisplayName}), ";
                    ret += "\n";
                }
                return string.IsNullOrEmpty(ret) ? "No stats." : ret;
            })
            .Register();
        Define("localstats")
            .Desc("Calculate local stats from a body's groups")
            .Alias("lstat", "groupstat")
            .Arg<ServerBoard>("board")
            .ComponentArg<Body>("body")
            .Runs(args =>
            {
                var body = args.Get<Body>(1);
                var ret = new StringBuilder();
                foreach (var stat in body.Stats)
                {
                    if (!stat.IsLocal)
                        continue;
                    ret.Append(stat.Id).Append($"(base = {body.Entity.Stats!.GetStat(stat.Id)!.BaseValue}):").AppendLine();
                    foreach (var group in body.Groups)
                    {
                        ret.Append("  ").Append(group).Append(": ");
                        var statVal = body.GetLocalStat(group, stat.Id, out var mods, out var baseUsed);
                        ret.Append(statVal).Append(" -");
                        foreach (var mod in mods)
                        {
                            ret.Append($" ({mod.Id},{mod.DisplayName},{mod.Value},{mod.Type})");
                        }
                        ret.AppendLine();
                    }
                }

                return ret.ToString();
            })
            .Register();

        Define("testcollision")
            .Desc("Tests collision between two points.")
            .Alias("collision")
            .Arg<ServerBoard>("board")
            .Arg<double>("x1").Arg<double>("y1")
            .Arg<double>("x2").Arg<double>("y2")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                Vector2 start = new((float)args.Get<double>(1), (float)args.Get<double>(2));
                Vector2 end = new((float)args.Get<double>(3), (float)args.Get<double>(4));
                var intersection = board.GetFloor(0).GetIntersection(start, end);
                return intersection == null ? "No intersection" : $"Intersection at {intersection}";
            })
            .Register();

        Define("floorambientlight")
            .Desc("Sets the ambient light of a floor.")
            .Alias("ambientlight", "setambientlight")
            .Arg<ServerBoard>("board")
            .Arg<int>("floor")
            .Arg<byte>("r").Arg<byte>("g").Arg<byte>("b")
            .OptArg<byte>("a")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                byte r = args.Get<byte>(2), g = args.Get<byte>(3), b = args.Get<byte>(4);
                byte a = args.TryGet<byte>(5, out var av) ? av : (byte)255;
                board.GetFloor(args.Get<int>(1)).AmbientLight = BitConverter.ToUInt32([r, g, b, a], 0);
                return "Ambient light set.";
            })
            .Register();

        Define("dumpfloorimage")
            .Desc("Dumps a floor image to a file.")
            .Alias("floordumpimage", "floordump", "dumpfloor")
            .Arg<ServerBoard>("board")
            .Arg<int>("floor")
            .Arg<string>("filename")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                int floor = args.Get<int>(1);
                string fileName = args.Get<string>(2);
                try { File.WriteAllBytes(fileName, board.GetFloor(floor).GetMidia().Bytes); }
                catch (IOException e) { throw new CommandException("Error writing file: " + e.Message); }
                return $"Image dumped to '{fileName}'.";
            })
            .Register();

        Define("tilemap")
            .Desc("Shows the tile-flag map of a floor.")
            .Alias("floormap")
            .Arg<ServerBoard>("board")
            .Arg<int>("floor")
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                Floor floorObj = board.GetFloor(args.Get<int>(1));
                string ret = "";
                for (int y = 0; y < floorObj.Size.Y; y++)
                {
                    for (int x = 0; x < floorObj.Size.X; x++)
                        ret += floorObj.TileHasFlag(new Vector2(x, y), Floor.TileFlag.FLOOR) ? "X" : ".";
                    ret += "\n";
                }
                return ret;
            })
            .Register();

        Define("exectime")
            .Desc("Benchmarks a function: intersection, vIntersection, obbIntersection.")
            .Alias("time")
            .Arg<string>("function", () => ["intersection", "vIntersection", "obbIntersection"])
            .Arg<int>("amount")
            .OptArg<ServerBoard>("board")
            .OptArg<int>("floor")
            .Runs(args =>
            {
                string code = args.Get<string>(0);
                var sw = new Stopwatch();
                switch (code)
                {
                    case "intersection":
                    {
                        if (args.Length < 4) throw new CommandException("Usage: exectime intersection <times> <board> <floor>");
                        var board = args.Get<ServerBoard>(2);
                        var floor = board.GetFloor(args.Get<int>(3));
                        sw.Start();
                        var rng = new Random();
                        for (int i = 0; i < args.Get<int>(1); i++)
                        {
                            Vector2 start = new(rng.Next(0, (int)floor.Size.X), rng.Next(0, (int)floor.Size.Y));
                            Vector2 end = new(rng.Next(0, (int)floor.Size.X), rng.Next(0, (int)floor.Size.Y));
                            floor.GetIntersection(start, end, out _);
                        }
                        sw.Stop();
                        int verts = floor.Walls.Sum(w => w.points.Length);
                        return $"Execution time: {sw.ElapsedMilliseconds}ms in a floor with {verts} vertices";
                    }
                    case "vIntersection":
                    {
                        if (args.Length < 3) throw new CommandException("Usage: exectime vIntersection <times> <board>");
                        var board = args.Get<ServerBoard>(2);
                        var rng = new Random();
                        sw.Start();
                        for (int i = 0; i < args.Get<int>(1); i++)
                        {
                            Vector3 pos = new(rng.Next(0, (int)board.GetFloor(0).Size.X), rng.Next(0, (int)board.GetFloor(0).Size.Y), rng.Next(0, board.GetFloorCount()));
                            board.GetVerticalIntersection(pos, rng.Next(0, board.GetFloorCount()));
                        }
                        sw.Stop();
                        return $"Execution time: {sw.ElapsedMilliseconds}ms in a board with {board.GetFloorCount()} floors";
                    }
                    case "obbIntersection":
                    {
                        if (args.Length < 3) throw new CommandException("Usage: exectime obbIntersection <times> <board>");
                        var board = args.Get<ServerBoard>(2);
                        var floor = board.GetFloor(0);
                        var rng = new Random();
                        for (int i = 0; i < args.Get<int>(1); i++)
                        {
                            OBB obb = new(new Vector2(rng.Next(0, (int)floor.Size.X), rng.Next(0, (int)floor.Size.Y)),
                                         new Vector2(rng.Next(0, 4), rng.Next(0, 4)),
                                         (float)(rng.NextDouble() * MathF.PI));
                            sw.Start();
                            foreach (var wall in floor.BroadPhaseOBB(obb))
                                Geometry.OBBLineIntersection(obb, wall, out _);
                            sw.Stop();
                        }
                        int verts = floor.Walls.Sum(w => w.points.Length);
                        return $"Execution time: {sw.ElapsedMilliseconds}ms on a floor with {verts} walls";
                    }
                    default:
                        throw new CommandException($"Unknown function '{code}'.");
                }
            })
            .Register();

        // ---- dooredit: uses Door component arg directly ----
        Define("dooredit")
            .Desc("Edits properties of a door entity.")
            .Alias("door", "editdoor")
            .Arg<ServerBoard>("board")
            .ComponentArg<Door>("entity")
            .Arg<string>("operation", () => ["vision", "flip", "open", "close"])
            .Runs(args =>
            {
                var board = args.Get<ServerBoard>(0);
                var door = args.Get<Door>(1);
                string op = args.Get<string>(2);
                switch (op)
                {
                    case "vision":
                        door.BlocksVision = !door.BlocksVision;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return $"Door {door.Entity.Id} blocks vision: {door.BlocksVision}";
                    case "flip":
                        (door.Bounds[0], door.Bounds[1]) = (door.Bounds[1], door.Bounds[0]);
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return $"Door {door.Entity.Id} flipped.";
                    case "open":
                        door.Closed = false;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return $"Door {door.Entity.Id} opened.";
                    case "close":
                        door.Closed = true;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return $"Door {door.Entity.Id} closed.";
                    default:
                        throw new CommandException($"Unknown operation '{op}'. Expected: vision, flip, open, close.");
                }
            })
            .Register();

        // ---- dumpbody: uses Body component arg directly ----
        Define("dumpbody")
            .Desc("Returns the body of a creature as JSON.")
            .Alias("jsonbody", "body", "bodydump")
            .Arg<ServerBoard>("board")
            .ComponentArg<Body>("entity")
            .Runs(args =>
            {
                var body = args.Get<Body>(1);
                return body.Model?.GetOriginalJson().ToString() ?? "No JSON data for body.";
            })
            .Register();

        Define("loadjson")
            .Desc("Loads a JSON file into the compendium.")
            .Alias("json", "compendiumadd", "compendium", "addjson", "jsonload", "jsonadd", "compendiumload")
            .Arg<string>("compendiumFolder", () => Compendium.Folders)
            .Arg<string>("path")
            .Runs(args =>
            {
                string folder = args.Get<string>(0);
                string fPath = args.Get<string>(1).Replace('/', '\\');
                if (!File.Exists(fPath))
                    throw new CommandException($"File '{fPath}' not found.");
                if (!Compendium.Folders.Contains(folder))
                    throw new CommandException($"Compendium folder '{folder}' not found.");

                string fName = fPath.Substring(fPath.LastIndexOf('\\'), fPath.LastIndexOf('.') - fPath.LastIndexOf('\\'));
                if (Compendium.GetEntryJsonOrNull(folder, fName) != null)
                    throw new CommandException($"Entry '{fName}' already exists in folder '{folder}'.");

                var json = JsonDocument.Parse(File.ReadAllText(fPath)).RootElement;
                if (json.ValueKind != JsonValueKind.Object)
                    throw new CommandException("Invalid JSON data (expected object).");
                var obj = Compendium.RegisterEntry(folder, fName, json);
                if (obj == null) throw new CommandException("Failed to register entry.");
                return $"Registered entry '{fName}'.";
            })
            .Register();

        Define("openbrowser")
            .Desc("Opens the web interface in the default browser.")
            .Alias("openweb", "webopen", "browseropen", "browser", "url", "web")
            .Runs(_ =>
            {
                string url = "http://localhost:5000";
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    return $"Opened web interface at {url}";
                }
                catch (Exception e)
                {
                    throw new CommandException($"Failed to open browser: {e.Message}");
                }
            })
            .Register();

        _nonAliases.Sort();
        Logger.Log($"Registered {_registeredCount} commands and {_commands.Count - _registeredCount} aliases.");
    }
}

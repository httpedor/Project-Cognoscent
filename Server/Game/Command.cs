using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg;
using Rpg.Inventory;
using Server.Game.Import;
using Server.Network;

namespace Server.Game;

public class Command
{
    private static int registeredCommands = 0;
    private static Dictionary<string, Command?> commands = new Dictionary<string, Command?>();
    private static List<string> nonAliases = new List<string>();
    private const int MaxSuggestionCount = 8;
    public string Name
    {
        get;
        private set;
    }

    public string Description
    {
        get;
        private set;
    }

    public string Usage
    {
        get;
        private set;
    }

    public string[] Aliases
    {
        get;
        private set;
    }

    public Type[] Arguments
    {
        get;
        private set;
    }
    
    // Optional, per-command string-argument suggestion providers.
    // Key: argument index (0-based), Value: provider that returns candidates
    public Dictionary<int, Func<IEnumerable<string>>>? Suggestions { get; private set; }
    
    public Func<RpgClient?, object[], string> Callback
    {
        get;
        private set;
    }
    
    public Command(string name, string description, string? usage, string[]? aliases, Type[]? arguments, Func<RpgClient?, object[], string> callback, Dictionary<int, Func<IEnumerable<string>>>? suggestions = null)
    {
        Name = name;
        Description = description;
        usage ??= "";
        Usage = usage;
        aliases ??= Array.Empty<string>();
        Aliases = aliases;
        arguments ??= Type.EmptyTypes;
        Arguments = arguments;
        Suggestions = suggestions;
        Callback = callback;
    }

    private static Command? GetCommand(string name)
    {
        return commands.TryGetValue(name, out Command? command) ? command : null;
    }

    public static IReadOnlyList<string> GetSuggestions(string? input)
    {
        input ??= string.Empty;
        string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool endsWithSpace = input.EndsWith(' ');

        string currentToken;
        int tokenIndex;

        if (tokens.Length == 0)
        {
            tokenIndex = 0;
            currentToken = input;
        }
        else if (endsWithSpace)
        {
            tokenIndex = tokens.Length;
            currentToken = string.Empty;
        }
        else
        {
            tokenIndex = tokens.Length - 1;
            currentToken = tokens[^1];
        }

        if (tokenIndex == 0 && !endsWithSpace)
        {
            return GetCommandNameSuggestions(currentToken);
        }

        if (tokens.Length == 0)
        {
            return GetCommandNameSuggestions(currentToken);
        }

        string commandToken = tokens[0].ToLowerInvariant();

        if (!endsWithSpace && tokenIndex == 0)
        {
            return GetCommandNameSuggestions(currentToken);
        }

        Command? command = GetCommand(commandToken);
        if (command == null)
        {
            return GetCommandNameSuggestions(currentToken);
        }

        int argumentIndex = tokenIndex - 1;
        if (argumentIndex < 0)
        {
            return GetCommandNameSuggestions(currentToken);
        }

        var argumentSuggestions = GetArgumentSuggestions(command, argumentIndex, currentToken, tokens);

        // Do NOT fall back to command name suggestions when asking for argument completions.
        // If there are no argument suggestions available, return an empty list so the UI shows nothing.
        return argumentSuggestions;
    }

    private static IReadOnlyList<string> GetCommandNameSuggestions(string prefix)
    {
        prefix ??= string.Empty;
        var candidates = nonAliases
            .Concat(commands.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return FilterCandidates(candidates, prefix);
    }

    private static IReadOnlyList<string> GetArgumentSuggestions(Command command, int argumentIndex, string currentToken, string[] tokens)
    {
        Type argumentType = GetArgumentType(command, argumentIndex);

        if (argumentType == typeof(ServerBoard))
        {
            return FilterCandidates(Game.GetBoards().Select(b => b.Name), currentToken);
        }

        if (argumentType == typeof(bool))
        {
            return FilterCandidates(new[] { "false", "true" }, currentToken);
        }

        if (argumentType == typeof(Entity))
        {
            ServerBoard? board = ResolveBoardContext(command, tokens, argumentIndex);
            if (board != null)
            {
                return FilterCandidates(board.GetEntities().Select(e => $"{e.Id}:{e.GetType().Name}"), currentToken);
            }
            return Array.Empty<string>();
        }

        if (argumentType == typeof(string))
        {
            return GetStringSuggestionsForCommand(command, argumentIndex, currentToken, tokens);
        }

        return Array.Empty<string>();
    }

    private static Type GetArgumentType(Command command, int argumentIndex)
    {
        if (command.Arguments.Length == 0)
            return typeof(string);
        if (argumentIndex < command.Arguments.Length)
            return command.Arguments[argumentIndex];
        return command.Arguments[^1];
    }

    private static ServerBoard? ResolveBoardContext(Command command, string[] tokens, int currentArgumentIndex)
    {
        int argsAvailable = Math.Min(tokens.Length - 1, currentArgumentIndex);
        for (int argPosition = 0; argPosition < argsAvailable; argPosition++)
        {
            Type type = GetArgumentType(command, argPosition);
            if (type != typeof(ServerBoard))
                continue;

            string boardName = tokens[argPosition + 1];
            if (string.IsNullOrEmpty(boardName))
                continue;

            ServerBoard? board = Game.GetBoard(boardName);
            if (board != null)
                return board;
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringSuggestionsForCommand(Command command, int argumentIndex, string currentToken, string[] tokens)
    {
        if (command.Suggestions != null && command.Suggestions.TryGetValue(argumentIndex, out var provider))
        {
            return FilterCandidates(provider(), currentToken);
        }

        if (command.Name.Equals("uvttload", StringComparison.OrdinalIgnoreCase) && argumentIndex == 2)
        {
            if (tokens.Length > 2 && tokens[2].Equals("append", StringComparison.OrdinalIgnoreCase))
            {
                return FilterCandidates(Game.GetBoards().Select(b => b.Name), currentToken);
            }
        }

        if (command.Name.Equals("help", StringComparison.OrdinalIgnoreCase) && argumentIndex == 0)
        {
            return FilterCandidates(nonAliases, currentToken);
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> FilterCandidates(IEnumerable<string> source, string prefix)
    {
        prefix ??= string.Empty;
        return source
            .Where(s => !string.IsNullOrEmpty(s) && (prefix.Length == 0 || s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestionCount)
            .ToList();
    }

    public static void RegisterCommand(Command cmd)
    {
        Type[] accepted = new[]
        {
            typeof(ServerBoard),
            typeof(Entity),
            typeof(string),
            typeof(double),
            typeof(bool),
            typeof(int),
            typeof(byte)
        };
        foreach (Type t in cmd.Arguments)
        {
            if (!Array.Exists(accepted, (x) => x == t))
                throw new ArgumentException("Invalid argument type " + nameof(t));
        }
        commands.Add(cmd.Name.ToLower(), cmd);
        nonAliases.Add(cmd.Name.ToLower());
        foreach (string alias in cmd.Aliases)
            commands.Add(alias.ToLower(), cmd);

        registeredCommands++;
    }

    public static void UnregisterCommand(Command cmd)
    {
        commands.Remove(cmd.Name);
        foreach (string alias in cmd.Aliases)
            commands.Remove(alias);
    }

    public static void UnregisterCommand(string name)
    {
        Command? cmd = GetCommand(name);
        if (cmd != null)
            UnregisterCommand(cmd);
    }

    public static void ExecuteCommand(RpgClient? client, string? cmdString)
    {
        string[] splitted = cmdString?.Split(" ") ?? Array.Empty<string>();
        if (splitted.Length == 0)
            return;
        
        string name = splitted[0].ToLower();
        if (name == "")
            return;
        
        string[] argStrings;
        if (splitted.Length == 1)
            argStrings = Array.Empty<string>();
        argStrings = splitted[1..];
        
        Command? cmd = GetCommand(name);
        if (cmd == null)
        {
            Logger.LogError("Unknown command " + name + "(" + name.Length + " chars)");
            return;
        }

        int reqArgs = cmd.Usage.Split("<").Length - 1;
        if (argStrings.Length < reqArgs)
        {
            Logger.LogError("Not enough arguments. Usage: " + cmd.Name + " " + cmd.Usage);
            return;
        }

        object[] args = new object[argStrings.Length];
        bool warned = false;
        for (int i = 0; i < args.Length; i++)
        {
            Type t;
            if (i >= cmd.Arguments.Length)
            {
                if (cmd.Arguments.Length == 0)
                {
                    Logger.LogError("Argument present when no arguments expected.");
                    return;
                }
                t = cmd.Arguments[^1]; // Last argument
                if (!warned)
                {
                    Logger.LogWarning("Argument type not specified, assuming last argument type (" + t.Name + ")");
                    warned = true;
                }
            }
            else
                t = cmd.Arguments[i];
            switch (t.Name)
            {
                case nameof(String):
                    args[i] = argStrings[i];
                    break;
                case nameof(Byte):
                    try
                    {
                        args[i] = byte.Parse(argStrings[i]);
                    }
                    catch (FormatException e)
                    {
                        Logger.LogError("Invalid argument " + argStrings[i] + ": " + e.Message);
                        return;
                    }
                    break;
                case nameof(Double):
                    try
                    {
                        args[i] = double.Parse(argStrings[i], CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        Logger.LogError("Invalid argument " + argStrings[i] + ": " + e.Message);
                        return;
                    }
                    break;
                case nameof(Int32):
                    try
                    {
                        args[i] = int.Parse(argStrings[i]);
                    }
                    catch (FormatException e)
                    {
                        Logger.LogError("Invalid argument " + argStrings[i] + ": " + e.Message);
                        return;
                    }
                    break;
                case nameof(Boolean):
                    args[i] = argStrings[i] == "true" || argStrings[i] == "1" || argStrings[i] == "yes" || argStrings[i] == "y" || argStrings[i] == "t";
                    break;
                case nameof(ServerBoard):
                    ServerBoard? board = null;
                    if (argStrings[i] == "" && Game.GetBoards().Count == 1)
                        board = Game.GetBoards()[0];
                    else
                        board = Game.GetBoard(argStrings[i]);
                    if (board == null)
                    {
                        Logger.LogError("Unknown board " + argStrings[i]);
                        return;
                    }
                    args[i] = board;
                    break;
                case nameof(Entity):
                    // Try to resolve board context from previously parsed arguments
                    ServerBoard? ctxBoard = null;
                    for (int j = 0; j < i; j++)
                    {
                        if (args[j] is ServerBoard sb)
                        {
                            ctxBoard = sb;
                            break;
                        }
                    }
                    if (ctxBoard == null && Game.GetBoards().Count == 1)
                    {
                        ctxBoard = Game.GetBoards()[0];
                    }
                    if (ctxBoard == null)
                    {
                        Logger.LogError("No board context found for entity argument");
                        return;
                    }

                    string token = argStrings[i];
                    string idPart = token;
                    int colonIdx = token.IndexOf(':');
                    if (colonIdx >= 0)
                        idPart = token.Substring(0, colonIdx);
                    if (!int.TryParse(idPart, out int entId))
                    {
                        Logger.LogError("Invalid entity identifier '" + token + "'. Expecting <id> or <id>:<name>");
                        return;
                    }
                    var entity = ctxBoard.GetEntityById(entId);
                    if (entity == null)
                    {
                        Logger.LogError($"Entity {entId} not found in board {ctxBoard.Name}");
                        return;
                    }
                    args[i] = entity;
                    break;
            }
        }
        
        try
        {
            Logger.Log(cmd.Callback(client, args));
        }
        catch (Exception e)
        {
            Logger.LogError("An error occurred executing the command: " + e.Message);
            Logger.LogError(e.ToString());
        }
    }

    public static void Init()
    {
        
        // suggestions: argument 0 -> list of non-alias command names
        RegisterCommand(new Command(
            "help",
            "Displays a list of commands, or help for a specific command.",
            "[command]",
            null,
            new[] { typeof(string) },
            (_, args) =>
            {
                if (args.Length == 0)
                {
                    string ret = "";
                    foreach (string name in nonAliases)
                    {
                        Command? cmd = GetCommand(name);
                        if (cmd != null)
                            ret += name + " " + cmd.Usage + " - " + cmd.Description + "\n";
                    }
                    return ret;
                }
                else
                {
                    string cmdName = (args[0] as string)!;
                    Command? cmd = GetCommand(cmdName);
                    if (cmd == null)
                        return "Unknown command " + cmdName;
                    return cmd.Name + " " + cmd.Usage + " - " + cmd.Description;
                }
            },
            new Dictionary<int, Func<IEnumerable<string>>>
            {
                { 0, () => nonAliases }
            }
        ));
        RegisterCommand(new Command(
            "who",
            "Displays a list of connected clients.",
            "",
            new[]{"online", "players"},
            null,
            (_, _) =>
            {
                string ret = "";
                foreach (RpgClient client in Network.Manager.Clients.Values)
                    ret += client.Username + " - " + client.socket.RemoteEndPoint + "\n";
                return ret;
            }
        ));
        RegisterCommand(new Command(
            "boardlist",
            "Lists all boards loaded",
            "",
            new[]{"showboards", "listboards"},
            null,
            (_, _) =>
            {
                string ret = "";
                foreach (ServerBoard board in Game.GetBoards())
                    ret += board.Name + "\n";
                return ret;
            }
        ));
        RegisterCommand(new Command(
            "boardrename",
            "Renams a board",
            "<board> <name>",
            new[]{"renameboard", "boardname"},
            new[]{typeof(ServerBoard), typeof(string)},
            (_, args) => 
            {
                var board = args[0] as ServerBoard;
                string? newName = args[1] as string;
                string oldName = board!.Name;
                Game.RemoveBoard(oldName);
                if (string.IsNullOrWhiteSpace(newName))
                    return "Invalid name";
                Game.AddBoard(board, newName);
                return "Board " + oldName + "renamed to " + newName;
            }
        ));
        RegisterCommand(new Command(
            "boardsave",
            "Saves a board to a file.",
            "<board> [filename]",
            new[]{"saveboard", "save"},
            new[] { typeof(ServerBoard), typeof(string)},
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                string fileName;
                if (args.Length == 1)
                    fileName = board!.Name.ToLower().FirstCharToUpper() + ".board";
                else
                    fileName = (string)args[1];
                Directory.CreateDirectory("Boards");
                try
                {
                    FileStream fs = File.OpenWrite("Boards/" + fileName);
                    board.ToBytes(fs);
                    fs.Flush();
                    fs.Close();
                }
                catch (IOException e)
                {
                    return "An error ocurred saving the board: " + e.Message;
                }
                
                return "Board " + fileName + " saved.";
            }
        ));
        RegisterCommand(new Command(
            "boardload",
            "Loads a board from a file.",
            "<filename>",
            new[]{"loadboard", "load"},
            new[]{typeof(string)},
            (_, args) =>
            {
                string fileName = (args[0] as string)!;
                fileName = fileName.Replace(".board", "");
                string bName;
                try
                {
                    FileStream fs = File.OpenRead("Boards/" + fileName + ".board");
                    ServerBoard b = new(fs);
                    fs.Close();
                    bName = b.Name;
                    Game.AddBoard(b);
                }
                catch (IOException e)
                {
                    return "An error ocurred loading the board: " + e.Message;
                }
                
                return "Board " + bName + " loaded!";
            }
        ));
        RegisterCommand(new Command(
            "boardunload",
            "Unloads a board from memory.",
            "<board>",
            new[]{"unloadboard", "unload"},
            new[] { typeof(ServerBoard) },
            (_, args) =>
            {
                Board board = (args[0] as Board)!;
                Game.RemoveBoard(board.Name);
                return "Board " + board.Name + " unloaded.";
            }
        ));
        RegisterCommand(new Command(
            "uvttloadmultiple",
            "Loads a board from multiple uvtt floors.",
            "<name> <floor1> [floor2] [floor3]...",
            new[]{"loaduvttmultiple", "uvttmultiple", "uvttloadmany", "loaduvttmany", "uvttmany"},
            new[] { typeof(string), typeof(string) },
            (_, args) =>
            {
                string boardName = (args[0] as string)!;
                ServerBoard board = new(boardName);
                for (int i = 1; i < args.Length; i++)
                {
                    string fileName = (args[i] as string)!;
                    string json;
                    try
                    {
                        json = File.ReadAllText(fileName);
                    }
                    catch (FileNotFoundException)
                    {
                        return "File " + fileName + " not found.";
                    }
                    catch (Exception e)
                    {
                        return "An error occurred reading the file: " + e.Message;
                    }
                    var ents = new List<Entity>();
                    Floor? f = Uvtt.LoadFloorFromUvttJson(json, ents);
                    if (f != null)
                    {
                        board.AddFloor(f);
                        foreach (Entity entity in ents)
                        {
                            entity.Position = new Vector3(entity.Position.X, entity.Position.Y, board.GetFloorCount()-1);
                            board.AddEntity(entity);
                        }
                    }
                    else
                        return "An error ocurred loading the floor " + fileName;
                }

                Game.AddBoard(board);
                return "Loaded board " + boardName + " with " + board.GetFloorCount() + " floors";
            }
        ));
        
        RegisterCommand(new Command(
            "uvttload",
            "Loads a board from a file. If [new|append] is not specified, it defaults to new. If append, the floor will be added to the [board] board.",
            "<file> [new|append] [board]",
            new[]{"loaduvtt", "uvtt"},
            new[] { typeof(string) },
            (_, args) =>
            {
                string fileName = (args[0] as string)!;
                string json;
                try
                {
                    json = File.ReadAllText(fileName);
                }
                catch (FileNotFoundException)
                {
                    return "File " + fileName + " not found.";
                }
                catch (Exception e)
                {
                    return "An error ocurred reading the file: " + e.Message;
                }
                string mode = "new";
                if (args.Length == 2)
                    mode = (args[1] as string)!;
                
                switch (mode)
                {
                    case "new":
                        ServerBoard? b = Uvtt.LoadBoardFromUvttJson(json, fileName.FirstCharToUpper().Replace(".uvtt", ""));
                        if (b == null)
                            return "An error ocurred loading the board.";
                        Game.AddBoard(b);
                        return "Loaded board " + b.Name;
                    case "append":
                        var ents = new List<Entity>();
                        Floor? f = Uvtt.LoadFloorFromUvttJson(json, ents);
                        if (f != null)
                        {
                            if (Game.GetBoards().Count == 0)
                                return "No boards loaded.";
                            if (args.Length < 3)
                                return "No board specified.";
                            ServerBoard? board = Game.GetBoard((args[2] as string)!);
                            if (board == null)
                                return $"Board {args[2]} not found.";

                            board.AddFloor(f);

                            foreach (Entity entity in ents)
                            {
                                entity.Position = new Vector3(entity.Position.X, entity.Position.Y, board.GetFloorCount()-1);
                                board.AddEntity(entity);
                            }
                            return "Loaded floor to index " + (board.GetFloorCount()-1) + " in board " + board.Name;
                        }
                        else
                            return "An error ocurred loading the floor.";
                    default:
                        return "Unknown mode " + mode;
                }
            },
            new Dictionary<int, Func<IEnumerable<string>>>
            {
                { 1, () => new [] { "new", "append" } },
                // if append and board missing, suggestions for arg 2 come from boards - handled in GetStringSuggestionsForCommand
            }
        ));
        RegisterCommand(new Command(
            "entitylist",
            "Lists all entities",
            "<board>",
            new[]{"listentities", "showentities", "entities", "entityshow"},
            new[] { typeof(ServerBoard)},
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                string ret = "";
                foreach (Entity e in board.GetEntities())
                    ret += e.Id + " - " + e.GetType().Name + " at " + e.Position + "\n";
                return ret;
            }
        ));
        RegisterCommand(new Command(
            "entityremove",
            "Removes an entity from a board",
            "<board> <entity>",
            new[]{"removeentity", "deleteentity", "entitydelete", "entitydestroy", "destroyentity"},
            new[] { typeof(ServerBoard), typeof(Entity) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                var ent = (args[1] as Entity)!;
                board.RemoveEntity(ent.Id);

                return "Entity removed";
            }
        ));
        RegisterCommand(new Command(
            "chatclear",
            "Clears the chat",
            "<board>",
            null,
            new[] { typeof(ServerBoard) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                board.GetChatHistory().Clear();
                return "Chat cleared";
            }
        ));
        RegisterCommand(new Command(
            "entityowner",
            "Sets the owner of an entity",
            "<board> <entity> [owner]",
            new[]{"entitysetowner", "creatureowner", "creaturesetowner", "setcreatureowner", "creatureownerset", "entityownerset"},
            new[] { typeof(ServerBoard), typeof(Entity), typeof(string) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = (args[1] as Entity)!;
                if (e is Creature c)
                {
                    if (args.Length == 3)
                        c.Owner = (args[2] as string)!;
                    else
                        if (c.Owner == "")
                            return $"{c.Id} has no owner";
                        else
                            return $"{c.Id}'s owner is: {c.Owner}";
                }
                return "Owner set";
            }
        ));
        RegisterCommand(new Command(
            "entitypos",
            "Sets an entity's position",
            "<board> <entity> [x] [y] [z]",
            new[]{"pos", "entitysetpos", "setentitypos"},
            new[]{typeof(ServerBoard), typeof(Entity), typeof(double), typeof(double), typeof(double)},
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = (args[1] as Entity)!;
                if (args.Length == 2)
                    return e.Position.ToString();
                if (args.Length == 5)
                    e.Position = new Vector3((float)(double)args[2], (float)(double)args[3], (float)(double)args[4]);
                else
                {
                    if (args.Length == 4)
                        e.Position = new Vector3((float)(double)args[2], (float)(double)args[3], e.Position.Z);
                    else if (args.Length == 3)
                        return "Invalid number of arguments";
                }
                return "Position set to " + e.Position;
            }
        ));
        RegisterCommand(new Command(
            "playerkick",
            "Kicks a player from the server",
            "<player>",
            new[]{"kick"},
            new[] { typeof(string) },
            (_, args) =>
            {
                string username = (args[0] as string)!;
                RpgClient? client = Network.Manager.GetClient(username);
                if (client == null)
                    return "Player not found";
                client.Disconnect();
                return $"{client.Username} kicked";
            }
        ));
        RegisterCommand(new Command(
            "entityimage",
            "Sets the image of an entity",
            "<board> <entity> <image>",
            new[]{"entitysetimage", "setentityimage"},
            new[] { typeof(ServerBoard), typeof(Entity), typeof(string) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = (args[1] as Entity)!;
                string? str = args[2] as string;
                if (string.IsNullOrWhiteSpace(str))
                    return "Invalid image";
                e.Display = new Midia(str);
                return "Image set";
            }
        ));
        RegisterCommand(new Command(
            "entityrotation",
            "Sets or reads the rotation of an entity",
            "<board> <entity> [rot]",
            new[]{"entitysetrotation", "setentityrotation", "rotationentity", "rotationsetentity"},
            new[] { typeof(ServerBoard), typeof(Entity), typeof(int) },
            (_, args) => {
                var entity = (args[1] as Entity)!;
                if (args.Length == 2)
                {
                    return "Entity rotation: " + entity.Rotation;
                }
                entity.Rotation = (int)args[2];
                return "Set entity rotation.";
            }
        ));
        RegisterCommand(new Command(
            "boardtick",
            "Ticks a board, or checks in which tick it's in",
            "<board> [ticks]",
            new[]{"tick"},
            new[] { typeof(ServerBoard), typeof(int) },
            (_, args) =>
            {
                var board = (args[0] as ServerBoard)!;
                if (args.Length < 2) return "Board is at tick " + board.CurrentTick;
                
                int ticks = (int)args[1];
                for (int i = 0; i < ticks; i++)
                    board.Tick();
                return "Board ticked " + ticks + " times";

            }
        ));
        RegisterCommand(new Command(
            "boardcombat",
            "Checks or toggles the board's combat mode",
            "<board> [state]",
            new[]{"combat", "combatmode", "turn", "turnmode"},
            [typeof(ServerBoard), typeof(bool)],
            (_, args) =>
            {
                var board = (args[0] as ServerBoard)!;
                if (args.Length < 2) return "Combat mode " + (board.TurnMode ? "enabled" : "disabled");
                
                bool enabled = (bool)args[1];
                if (enabled)
                    board.StartTurnMode();
                else
                    board.EndTurnMode();
                return "Combat mode " + (board.TurnMode ? "enabled" : "disabled");
            }
        ));
        RegisterCommand(new Command(
            "creatureactions",
            "Lists current actions of a creature",
            "<board> <entity>",
            new[]{"actions", "creatureaction", "listcreatureactions", "listactions"},
            new[] { typeof(ServerBoard), typeof(Entity) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = (args[1] as Entity)!;
                if (e is Creature c)
                {
                    string ret = "";
                    foreach (SkillData action in c.ActiveSkills.Values)
                        ret += action.Skill.GetName() + " - " + action.Skill.GetDescription() + "\n";
                    return ret;
                }
                return "Entity is not a creature";
            }
        ));
        RegisterCommand(new Command(
            "creaturestats",
            "Lists creature stats",
            "<board> <entity>",
            new[]{"liststats"},
            new[]{ typeof(ServerBoard), typeof(Entity) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = (args[1] as Entity)!;
                if (e is Creature c)
                {
                    string ret = "";
                    foreach (Stat stat in c.Stats)
                    {
                        ret += stat.Id + " - " + stat.FinalValue + "(" + stat.BaseValue + ") ; " ;
                        ret += stat.GetModifiers().Count() + " mods: ";
                        foreach (StatModifier mod in stat.GetModifiers())
                        {
                            ret += "(" + mod.Id + "," + mod.Type + "," + mod.Value + "), ";
                        }
                        ret += "\n";
                    }
                    return ret;
                }
                return "Entity is not a creature";
            }
        ));
        RegisterCommand(new Command(
            "testcollision",
            "Tests collision between two points",
            "<board> <x1> <y1> <x2> <y2>",
            new[]{"collision"},
            new[] { typeof(ServerBoard), typeof(double), typeof(double), typeof(double), typeof(double) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Vector2 start = new((float)(double)args[1], (float)(double)args[2]);
                Vector2 end = new((float)(double)args[3], (float)(double)args[4]);
                Vector2? intersection = board.GetFloor(0).GetIntersection(start, end);
                if (intersection == null)
                    return "No intersection";
                return "Intersection at " + intersection;
            }
        ));
        RegisterCommand(new Command(
            "floorambientlight",
            "Sets the ambient light of a floor",
            "<board> <floor> <r> <g> <b> [a]",
            new[]{"ambientlight", "setambientlight"},
            new[] { typeof(ServerBoard), typeof(int), typeof(byte), typeof(byte), typeof(byte) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                byte r = (byte)args[2];
                byte g = (byte)args[3];
                byte b = (byte)args[4];
                if (args.Length == 6)
                {
                    byte a = (byte)args[5];
                    board.GetFloor((int)args[1]).AmbientLight = BitConverter.ToUInt32(new[]{r, g, b, a}, 0);
                }
                else
                    board.GetFloor((int)args[1]).AmbientLight = BitConverter.ToUInt32(new byte[]{r, g, b, 255}, 0);

                return "Ambient light set";
            }
        ));
        RegisterCommand(new Command(
            "dumpfloorimage",
            "Dumps an image to a file",
            "<board> <floor> <filename>",
            new[]{"floordumpimage", "floordump", "dumpfloor"},
            new[] { typeof(ServerBoard), typeof(int), typeof(string) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                int floor = (int)args[1];
                string fileName = (args[2] as string)!;
                try
                {
                    File.WriteAllBytes(fileName, board.GetFloor(floor).GetMidia().Bytes);
                }
                catch (IOException e)
                {
                    return "An error ocurred writing the file: " + e.Message;
                }
                return "Image dumped to " + fileName;
            }
        ));
        RegisterCommand(new Command(
            "tilemap",
            "Shows the tileflag map of a floor",
            "<board> <floor>",
            new[]{"floormap"},
            new[] { typeof(ServerBoard), typeof(int) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                int floor = (int)args[1];
                string ret = "";
                Floor floorObj = board.GetFloor(floor);
                for (int y = 0; y < floorObj.Size.Y; y++)
                {
                    for (int x = 0; x < floorObj.Size.X; x++)
                    {
                        ret += floorObj.TileHasFlag(new Vector2(x, y), Floor.TileFlag.FLOOR) ? "X" : ".";
                    }
                    ret += "\n";
                }
                return ret;
            }
        ));
        RegisterCommand(new Command(
            "exectime",
            "Executes a function and returns the time it took to execute",
            "<intersection | tick> <amount> <args...>",
            new[]{"time"},
            new[] { typeof(string), typeof(int), typeof(ServerBoard), typeof(int)},
            (_, args) =>
            {
                string code = (args[0] as string)!;
                var sw = new Stopwatch();
                switch (code)
                {
                    case "intersection":
                    {
                        if (args.Length < 4)
                            return "Not enough arguments. Use exectime intersection <times> <board> <floor>";
                        
                        ServerBoard board = (args[2] as ServerBoard)!;
                        Floor floor = board.GetFloor((int)args[3]);
                        sw.Start();
                        for (int i = 0; i < (int)args[1]; i++)
                        {
                            Vector2 start = new(new Random().Next(0, (int)floor.Size.X), new Random().Next(0, (int)floor.Size.Y));
                            Vector2 end = new(new Random().Next(0, (int)floor.Size.X), new Random().Next(0, (int)floor.Size.Y));
                            floor.GetIntersection(start, end, out var normal);
                        }
                        sw.Stop();
                        int verticeCount = 0;
                        for (int i = 0; i < floor.Walls.Length; i++)
                            verticeCount += floor.Walls[i].points.Length;
                        return "Execution time: " + sw.ElapsedMilliseconds + "ms in a floor with " + verticeCount + " vertices";
                    }
                    case "vIntersection":
                    {
                        if (args.Length < 3)
                            return "Not enough arguments. Use exectime vIntersection <times> <board>";
                        
                        ServerBoard board = (args[2] as ServerBoard)!;
                        sw.Start();
                        for (int i = 0; i < (int)args[1]; i++)
                        {
                            Vector3 pos = new(new Random().Next(0, (int)board.GetFloor(0).Size.X), new Random().Next(0, (int)board.GetFloor(0).Size.Y), new Random().Next(0, (int)board.GetFloorCount()));
                            board.GetVerticalIntersection(pos, new Random().Next(0, board.GetFloorCount()));
                        }
                        sw.Stop();
                        return "Execution time: " + sw.ElapsedMilliseconds + "ms" + " in a board with " + board.GetFloorCount() + " floors";
                    }
                    case "obbIntersection":
                    {
                        if (args.Length < 3)
                            return "Not enough arguments. Use exectime obbIntersection <times> <board>";
                        
                        ServerBoard board = (args[2] as ServerBoard)!;
                        Floor floor = board.GetFloor(0);
                        var random = new Random();
                        for (int i = 0; i < (int)args[1]; i++)
                        {
                            OBB obb = new(new Vector2(random.Next(0, (int)floor.Size.X), random.Next(0, (int)floor.Size.Y)), new Vector2(random.Next(0, 4), random.Next(0, 4)), (float)(random.NextDouble() * MathF.PI));
                            sw.Start();
                            foreach (Line wall in floor.BroadPhaseOBB(obb))
                                Geometry.OBBLineIntersection(obb, wall, out Vector2 __);
                            sw.Stop();
                        }

                        int verticeCount = 0;
                        for (int i = 0; i < floor.Walls.Length; i++)
                            verticeCount += floor.Walls[i].points.Length;

                        return "Execution time: " + sw.ElapsedMilliseconds + "ms on a floor with " + verticeCount + " walls";
                    }
                    default:
                        return "Unknown function";
                };
            }
        ));
        RegisterCommand(new Command(
            "spawnitem",
            "Spawns an item",
            "<board> <x> <y> <floor> <name>",
            new[]{"itemspawn", "itemnew", "newitem"},
            new[] { typeof(ServerBoard), typeof(double), typeof(double), typeof(double), typeof(string) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                double x = (double)args[1];
                double y = (double)args[2];
                double floor = (double)args[3];
                string name = (args[4] as string)!;
                Item item = new("", name, "Item spawned in through command");
                var e = new ItemEntity(item)
                {
                    Position = new Vector3((float)x, (float)y, (float)floor)
                };
                board.AddEntity(e);
                return "Item spawned";
            }
        ));
        
        RegisterCommand(new Command(
            "dooredit",
            "Edits the properties of a door",
            "<board> <entity> <vision|flip|open|close>",
            new[]{"door", "editdoor"},
            new[]{typeof(ServerBoard), typeof(Entity), typeof(string)},
            (_, args) =>
            {
                var board = (args[0] as ServerBoard)!;
                string? operation = args[2] as string;
                var ent = (args[1] as Entity)!;
                var door = ent as DoorEntity;
                if (door == null)
                    return "Entity is not a door";

                switch (operation)
                {
                    case "vision":
                    {
                        door.BlocksVision = !door.BlocksVision;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return "Door " + door.Id + " is now " + door.BlocksVision;
                    }
                    case "flip":
                    {
                        Vector2 old1 = door.Bounds[1];
                        door.Bounds[1] = door.Bounds[0];
                        door.Bounds[0] = old1;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return "Door " + door.Id + " is now flipped";
                    }
                    case "open":
                    {
                        door.Closed = false;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return "Door " + door.Id + " opened";
                    }
                    case "close":
                    {
                        door.Closed = true;
                        Manager.SendToBoard(new DoorUpdatePacket(door), board.Name);
                        return "Door " + door.Id + " closed";
                    }
                    default:
                    {
                        return "Invalid operation.";
                    }
                }
            },
            new Dictionary<int, Func<IEnumerable<string>>>
            {
                { 2, () => new [] { "vision", "flip", "open", "close" } }
            }
        ));
        RegisterCommand(new Command(
            "dumpbody",
            "Returns the body of a creature as JSON",
            "<board> <entity>",
            new []{"jsonbody", "body", "bodydump"},
            new[]{typeof(ServerBoard), typeof(Entity)},
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                var ent = (args[1] as Entity)!;
                Creature? c = ent as Creature;
                if (c == null)
                    return "Creature not found";

                return c.Body.Root.ToJson().ToString();
            }
        ));
        RegisterCommand(new Command(
            "loadjson",
            "Loads a JSON file into the compendium",
            "<compendiumFolder> <path>",
            new[]{"json", "compendiumadd", "compendium", "addjson", "jsonload", "jsonadd", "compendiumload"},
            new[]{typeof(string), typeof(string)},
            (_, args) =>
            {
                string folder = (args[0] as string)!;
                string fPath = (args[1] as string)!;
                fPath = fPath.Replace('/', '\\');
                if (!File.Exists(fPath))
                    return $"File {fPath} not found";
                if (!Compendium.Folders.Contains(folder))
                    return $"Folder {folder} not found";
                
                string fName = fPath.Substring(fPath.LastIndexOf('\\'), fPath.LastIndexOf('.') - fPath.LastIndexOf('\\'));
                if (Compendium.GetEntryJsonOrNull(folder, fName) != null)
                    return $"Entry {fName} already exists in folder {folder}";

                var json = JsonNode.Parse(File.ReadAllText(fPath));
                if (json == null || json.GetValueKind() != JsonValueKind.Object)
                {
                    return "Invalid JSON Data";
                }
                var obj = Compendium.RegisterEntry(folder, fName, json.AsObject());
                if (obj == null)
                    return "Invalid JSON Data";
                return "Registered entry " + fName;
            }
        ,
            new Dictionary<int, Func<IEnumerable<string>>>
            {
                { 0, () => Compendium.Folders }
            }
        ));
        RegisterCommand(new Command(
            "openbrowser",
            "Opens the web interface in the default browser",
            "",
            ["openweb", "webopen", "browseropen", "browser", "url", "web"],
            Array.Empty<Type>(),
            (_, _) =>
            {
                string url = "http://localhost:5000";
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    return "Opened web interface at " + url;
                }
                catch (Exception e)
                {
                    return "Failed to open browser: " + e.Message;
                }
            }
        ));

        nonAliases.Sort();
        
        Logger.Log("Registered " + registeredCommands + " commands and " + (commands.Count - registeredCommands) + " aliases");
    }

}

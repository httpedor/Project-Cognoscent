using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg;
using Rpg.Inventory;
using Server.Game.Import;
using Server.Network;
using Server.AI;

namespace Server.Game;

public class Command
{
    private static int registeredCommands = 0;
    private static Dictionary<string, Command?> commands = new Dictionary<string, Command?>();
    private static List<string> nonAliases = new List<string>();
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
    
    public Func<RpgClient?, object[], string> Callback
    {
        get;
        private set;
    }
    
    public Command(string name, string description, string? usage, string[]? aliases, Type[]? arguments, Func<RpgClient?, object[], string> callback)
    {
        Name = name;
        Description = description;
        usage ??= "";
        Usage = usage;
        aliases ??= Array.Empty<string>();
        Aliases = aliases;
        arguments ??= Type.EmptyTypes;
        Arguments = arguments;
        Callback = callback;
    }

    private static Command? GetCommand(string name)
    {
        return commands.TryGetValue(name, out Command? command) ? command : null;
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
    
    private static void ListenForCommands()
    {
        while (true)
        {
            string? cmd = Console.ReadLine();
            ExecuteCommand(null, cmd);
        }
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
            Console.WriteLine("Unknown command " + name + "(" + name.Length + " chars)");
            return;
        }

        int reqArgs = cmd.Usage.Split("<").Length - 1;
        if (argStrings.Length < reqArgs)
        {
            Console.WriteLine("Not enough arguments. Usage: " + cmd.Name + " " + cmd.Usage);
            return;
        }

        object[] args = new object[argStrings.Length];
        bool warned = false;
        for (int i = 0; i < args.Length; i++)
        {
            Type t;
            if (i >= cmd.Arguments.Length)
            {
                t = cmd.Arguments[^1]; // Last argument
                if (!warned)
                {
                    Console.WriteLine("[WARNING] Argument type not specified, assuming last argument type (" + t.Name + ")");
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
                        Console.WriteLine("Invalid argument " + argStrings[i] + ": " + e.Message);
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
                        Console.WriteLine("Invalid argument " + argStrings[i] + ": " + e.Message);
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
                        Console.WriteLine("Invalid argument " + argStrings[i] + ": " + e.Message);
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
                        Console.WriteLine("Unknown board " + argStrings[i]);
                        return;
                    }
                    args[i] = board;
                    break;
            }
        }
        
        try
        {
            Console.WriteLine(cmd.Callback(client, args));
        }
        catch (Exception e)
        {
            Console.WriteLine("An error occurred executing the command: " + e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }

    public static void Init()
    {
        Task.Run(ListenForCommands);
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
            "<board> <id>",
            new[]{"removeentity", "deleteentity", "entitydelete", "entitydestroy", "destroyentity"},
            new[] { typeof(ServerBoard), typeof(int) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                board.RemoveEntity((int)args[1]);

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
            "<board> <id> [owner]",
            new[]{"entitysetowner", "creatureowner", "creaturesetowner", "setcreatureowner", "creatureownerset", "entityownerset"},
            new[] { typeof(ServerBoard), typeof(int), typeof(string) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = board.GetEntityById((int)args[1])!;
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
            "<board> <id> [x] [y] [z]",
            new[]{"pos", "entitysetpos", "setentitypos"},
            new[]{typeof(ServerBoard), typeof(int), typeof(double), typeof(double), typeof(double)},
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = board.GetEntityById((int)args[1])!;
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
            "<board> <id> <image>",
            new[]{"entitysetimage", "setentityimage"},
            new[] { typeof(ServerBoard), typeof(int), typeof(string) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = board.GetEntityById((int)args[1])!;
                string? str = args[2] as string;
                Span<byte> buffer = new Span<byte>(new byte[str.Length]);
                e.Display = new Midia(str);
                return "Image set";
            }
        ));
        RegisterCommand(new Command(
            "entityrotation",
            "Sets or reads the rotation of an entity",
            "<board> <id> [rot]",
            new[]{"entitysetrotation", "setentityrotation", "rotationentity", "rotationsetentity"},
            new[] { typeof(ServerBoard), typeof(int), typeof(string) },
            (_, args) => {
                ServerBoard board = args[0] as ServerBoard;
                int id = (int)args[1];
                if (args.Length == 2)
                {
                    return "Entity rotation: " + board.GetEntityById(id).Rotation;
                }

                board.GetEntityById(id).Rotation = (int)args[2];
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
            new[]{"combat"},
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
            "<board> <id>",
            new[]{"actions", "creatureaction", "listcreatureactions", "listactions"},
            new[] { typeof(ServerBoard), typeof(int) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = board.GetEntityById((int)args[1])!;
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
            "<board> <id>",
            new[]{"liststats"},
            new[]{ typeof(ServerBoard), typeof(int) },
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                Entity e = board.GetEntityById((int)args[1])!;
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
                    File.WriteAllBytes(fileName, board.GetFloor(floor).GetImage());
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
                Item item = new(null, name, "Item spawned in through command");
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
            "<board> <id> <vision|flip|open|close>",
            new[]{"door", "editdoor"},
            new[]{typeof(ServerBoard), typeof(int), typeof(string)},
            (_, args) => 
            {
                var board = args[0] as ServerBoard;
                int id = ((int)args[1]);
                string? operation = args[2] as string;

                var door = board.GetEntityById(id) as DoorEntity;

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
            }
        ));
        RegisterCommand(new Command(
            "aichat",
            "Sends a message to the AI chat",
            "<message>",
            new[]{"ai", "aicommand"},
            new[]{typeof(string)},
            (_, args) =>
            {
                var msg = new StringBuilder();
                foreach (object arg in args)
                {
                    msg.Append(arg);
                    msg.Append(' ');
                }

                Task.Run(async () =>
                {
                    string response = await AI.AI.ServerChat.Prompt(msg.ToString());
                    
                    Console.WriteLine("AI response: " + response);
                });
                return "Message sent successfully.";
            }
        ));
        RegisterCommand(new Command(
            "dumpbody",
            "Returns the body of a creature as JSON",
            "<board> <id>",
            new []{"jsonbody", "body", "bodydump"},
            new[]{typeof(ServerBoard), typeof(int)},
            (_, args) =>
            {
                ServerBoard board = (args[0] as ServerBoard)!;
                int id = (int)args[1];

                Creature? c = board.GetEntityById<Creature>(id);
                if (c == null)
                    return "Creature not found";

                return c.Body.Root.ToJson().ToString();
            }
        ));
        RegisterCommand(new Command(
            "loadjson",
            "Loads a JSON file into the compendium",
            "<compendiumFolder> <path>",
            ["json", "compendiumadd", "compendium", "addjson", "jsonload", "jsonadd", "compendiumload"],
            new[]{typeof(ServerBoard)},
            (_, args) =>
            {
                string folder = args[0] as string;
                string fPath = args[1] as string;
                fPath = fPath.Replace('/', '\\');
                if (!File.Exists(fPath))
                    return $"File {fPath} not found";
                if (!Compendium.Folders.Contains(folder))
                    return $"Folder {folder} not found";
                
                string fName = fPath.Substring(fPath.LastIndexOf('\\'), fPath.LastIndexOf('.') - fPath.LastIndexOf('\\'));
                if (Compendium.GetEntryOrNull(folder, fName) != null)
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
        ));

        nonAliases.Sort();
        
        Console.WriteLine("Registered " + registeredCommands + " commands and " + (commands.Count - registeredCommands) + " aliases");
    }

}

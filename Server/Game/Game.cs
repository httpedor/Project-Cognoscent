using System.Diagnostics;
using Rpg;

namespace Server.Game;

public static class Game
{
    private static readonly Dictionary<string, ServerBoard> _boards = new();
    public static void Init()
    {
        Compendium.OnEntryRegistered += (folder, name, obj) =>
        {
            Network.Manager.SendToAll(CompendiumUpdatePacket.AddEntry(folder, name, obj));
        };
        Compendium.OnEntryRemoved += (folder, name) =>
        {
            Network.Manager.SendToAll(CompendiumUpdatePacket.RemoveEntry(folder, name));
        };
        Compendium.RegisterDefaults();
        foreach (Feature feat in Features.All)
        {
            Compendium.RegisterEntry<Feature>(feat.GetId(), null!);
        }

        foreach (var skillInfo in Skills.All)
        {
            Compendium.RegisterEntry<Skill>(skillInfo.id, null!);
        }
        foreach (string folder in Compendium.Folders)
        {
            foreach (var pair in Compendium.GetFiles(folder))
            {
                Compendium.RegisterEntry(folder, pair.fName, pair.obj);
            }
            
            Logger.Log("Registered " + Compendium.GetEntryCount(folder) + " " + folder + " entries.");
        }
    }

    public static ServerBoard? GetBoard(string name)
    {
        return _boards.GetValueOrDefault(name);
    }

    public static List<ServerBoard> GetBoards()
    {
        return _boards.Values.ToList();
    }

    public static void AddBoard(ServerBoard board)
    {
        bool deleteOld = _boards.ContainsKey(board.Name);
        _boards[board.Name] = board;

        foreach (var client in Network.Manager.Clients.Values)
        {
            if (deleteOld)
                client.Send(new BoardRemovePacket(board));
            if (client.IsGm || client.LoadedBoards.Contains(board.Name) || _boards.Count == 1)
                client.SendBoard(board);
        }
        
        const int timePerTick = (int)(1f/50 * 1000); // 20 ms, this means 50tps
        Task.Run(async () => {
            var sw = new Stopwatch();
            while (_boards.ContainsKey(board.Name))
            {
                if (board.TurnMode)
                    continue;

                sw.Start();
                board.Tick();
                sw.Stop();
                long elapsed = sw.ElapsedMilliseconds;
                sw.Reset();

                if (elapsed > timePerTick)
                    Logger.LogWarning("Tick took " + elapsed + "ms");

                if (elapsed < timePerTick)
                    await Task.Delay(timePerTick - (int)elapsed);
            }
        });
    }

    public static void AddBoard(ServerBoard b, string name)
    {
        b.Name = name;
        AddBoard(b);
    }
    
    public static Board? RemoveBoard(string name)
    {
        if (_boards.TryGetValue(name, out var board))
        {
            _boards.Remove(name);
            foreach (var client in Network.Manager.Clients.Values)
            {
                if (client.LoadedBoards.Contains(name))
                {
                    client.Send(new BoardRemovePacket(board));
                    client.LoadedBoards.Remove(name);
                }
            }
            return board;
        }

        return null;
    }

}

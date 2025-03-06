using System.Diagnostics;
using MoonSharp.Interpreter;
using Rpg;
using Rpg.Entities;

namespace Server.Game;

public static class Game
{
    private static Dictionary<String, ServerBoard> _boards = new Dictionary<String, ServerBoard>();
    public static void Init()
    {
        UserData.RegisterType<Entity>();
        UserData.RegisterType<Creature>();
        UserData.RegisterType<ServerBoard>();
        UserData.RegisterType<ServerFloor>();
    }

    public static ServerBoard? GetBoard(string name)
    {
        if (_boards.TryGetValue(name, out var board))
            return board;
        return null;
    }

    public static List<ServerBoard> GetBoards()
    {
        return _boards.Values.ToList();
    }

    public static void AddBoard(ServerBoard board)
    {
        _boards[board.Name] = board;

        foreach (var client in Network.Manager.Clients.Values)
        {
            if (client.IsGm || client.LoadedBoards.Contains(board.Name) || _boards.Count == 1)
                client.SendBoard(board);
        }
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

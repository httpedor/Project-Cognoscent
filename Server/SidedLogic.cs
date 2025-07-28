using System.Numerics;
using System.Reflection;
using Rpg;
using Server.Game;

namespace Server;

public class ServerSidedLogic : SidedLogic
{
    public static void Init(){
        Instance = new ServerSidedLogic();
    }

    public override Board NewBoard()
    {
        return new ServerBoard("");
    }

    public override Floor NewFloor(Vector2 size, Vector2 tileSize, uint ambientLight)
    {
        return new ServerFloor(size, tileSize, ambientLight);
    }

    public override Boolean IsClient()
    {
        return false;
    }

    public override Board? GetBoard(String name)
    {
        return Game.Game.GetBoard(name);
    }

    public override string GetRpgAssemblyPath()
    {
        return typeof(Entity).Assembly.Location;
    }
}

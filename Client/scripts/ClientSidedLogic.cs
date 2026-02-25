using System.IO;
using Godot;
using Rpg;
using TTRpgClient.scripts.RpgImpl;
using Vector2 = System.Numerics.Vector2;

namespace TTRpgClient.scripts;

public class ClientSidedLogic : SidedLogic
{
    public static void Init(){
        Instance = new ClientSidedLogic();
    }

    public override Board NewBoard()
    {
        return new ClientBoard();
    }

    public override Floor NewFloor(Vector2 size, Vector2 tileSize, uint ambientLight)
    {
        return new ClientFloor(size.ToGodot(), tileSize.ToGodot(), ambientLight);
    }

    public override bool IsClient()
    {
        return true;
    }

    public override Board? GetBoard(string name)
    {
        return GameManager.Instance.GetBoard(name);
    }

}

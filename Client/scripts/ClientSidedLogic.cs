using System.IO;
using System.Numerics;
using Rpg;
using TTRpgClient.scripts.RpgImpl;

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

    public override System.Boolean IsClient()
    {
        return true;
    }

    public override Board? GetBoard(System.String name)
    {
        return GameManager.Instance.GetBoard(name);
    }

}

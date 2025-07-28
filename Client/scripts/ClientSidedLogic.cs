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

    public override System.Boolean IsClient()
    {
        return true;
    }

    public override string GetRpgAssemblyPath()
    {
        #if DEBUG
        return ProjectSettings.GlobalizePath("res://") + "\\.godot\\mono\\temp\\bin\\Debug\\Rpg.dll";
        #else
        return OS.GetExecutablePath() + "\\data_TTRpgClient_windows_x86_64\\Rpg.dll"
        #endif
    }

    public override Board? GetBoard(System.String name)
    {
        return GameManager.Instance.GetBoard(name);
    }

}

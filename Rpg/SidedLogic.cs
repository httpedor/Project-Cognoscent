using System.Numerics;
using Rpg.Entities;

namespace Rpg;

public abstract class SidedLogic
{
    public static SidedLogic Instance {get; protected set;}

    public abstract Board NewBoard();
    public abstract Floor NewFloor(Vector2 size, Vector2 tileSize, UInt32 ambientLight);
    public abstract Board? GetBoard(string name);
    public abstract bool IsClient();
}

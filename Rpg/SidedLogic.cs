using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg;

namespace Rpg;

public abstract class SidedLogic
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public static SidedLogic Instance {get; protected set;}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public abstract Board NewBoard();
    public abstract Floor NewFloor(Vector2 size, Vector2 tileSize, UInt32 ambientLight);
    public abstract Board? GetBoard(string name);
    public abstract bool IsClient();
    public abstract string GetRpgAssemblyPath();
}

using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Rpg;

namespace Rpg;

public abstract class SidedLogic
{
    public static SidedLogic Instance {get; protected set;}

    public abstract Board NewBoard();
    public abstract Floor NewFloor(Vector2 size, Vector2 tileSize, UInt32 ambientLight);
    public abstract Board? GetBoard(string name);
    public abstract bool IsClient();
    public abstract string GetRpgAssemblyPath();

    public Func<TC, T> Compile<TC, T>(string code, params string[] imports)
    {
        var test = CSharpScript.Create<object>(code,
            ScriptOptions.Default.WithReferences(MetadataReference.CreateFromFile(GetRpgAssemblyPath()))
                .WithImports(imports), typeof(TC)).CreateDelegate();
        var script = CSharpScript.Create<T>(code,
            ScriptOptions.Default.WithReferences(MetadataReference.CreateFromFile(GetRpgAssemblyPath()))
                .WithImports(imports), typeof(TC)).CreateDelegate();
        return ctx => script(ctx).Result;
    }
}

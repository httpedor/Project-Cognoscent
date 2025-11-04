using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Rpg;

public static class Scripting
{
    public static Func<C, T> Compile<C, T>(string code)
    {
        if (SidedLogic.Instance.IsClient())
        {
            Logger.LogWarning("Attempted to compile script on client side. Returning default function.");
            return ctx => default!;
        }
        try
        {
            var script = CSharpScript.Create<T>(
                code,
                ScriptOptions.Default
                    .WithReferences(typeof(Creature).Assembly)
                    .WithReferences(typeof(C).Assembly)
                    .WithReferences(typeof(T).Assembly)
                    .WithReferences(typeof(Math).Assembly)
                    .WithImports("Rpg")
                    .WithImports("System")
                    .WithImports("System.Math")
                    .WithImports(typeof(T).Namespace!)
                    .WithImports(typeof(C).Namespace!),
                typeof(C)
            ).CreateDelegate();

            return ctx => script(ctx).Result;
        } catch (Exception e)
        {
            throw new Exception("Could not compile script: " + code, e);
        }
    }
    public static Action<C> Compile<C>(string code)
    {
        var script = CSharpScript.Create<C>(
            code,
            ScriptOptions.Default
                .WithReferences(typeof(Creature).Assembly)
                .WithReferences(typeof(C).Assembly)
                .WithReferences(typeof(Math).Assembly)
                .WithImports("Rpg")
                .WithImports("System")
                .WithImports("System.Math")
                .WithImports(typeof(C).Namespace!)
                ,
            typeof(C)
        ).CreateDelegate();

        return ctx => script(ctx);
    }
}

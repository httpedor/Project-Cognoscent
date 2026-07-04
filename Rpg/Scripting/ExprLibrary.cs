using System.Text.Json;

namespace Rpg.Scripting;

public class ExprLibrary : ISerializable
{
    public readonly string Id;
    private readonly Dictionary<string, BaseExpr> _exprs = new();
    public ExprLibrary(string id)
    {
        Id = id;
    }
    public void RegisterExpr(string name, BaseExpr expr)
    {
        _exprs[name] = expr;
    }

    public BaseExpr? GetExpr(string name)
    {
        _exprs.TryGetValue(name, out var expr);
        return expr;
    }

    public IEnumerable<BaseExpr> GetAllExprs()
    {
        return _exprs.Values;
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Id);
        stream.WriteInt32(_exprs.Count);
        foreach (var kvp in _exprs)
        {
            stream.WriteString(kvp.Key);
            kvp.Value.ToBytes(stream);
        }
    }

    public static ExprLibrary FromBytes(Stream stream)
    {
        var id = stream.ReadString();
        var library = new ExprLibrary(id);
        var count = stream.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var name = stream.ReadString();
            var expr = BaseExpr.Deserialize(stream);
            library.RegisterExpr(name, expr);
        }
        return library;
    }

    public static ExprLibrary FromJson(string id, JsonElement obj)
    {
        var library = new ExprLibrary(id);
        if (obj.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Expected JSON object for expression library");
        }
        foreach (var prop in obj.EnumerateObject())
        {
            var name = prop.Name;
            var expr = ExpressionCompiler.CompileBaseExpr(prop.Value);
            library.RegisterExpr(name, expr);
        }
        return library;
    }

    public static object? ExecuteFromLibrary(string libraryId, string exprName, EvalContext ctx = null!)
    {
        if (!Compendium.TryGetEntry<ExprLibrary>(libraryId, out var library))
        {
            Logger.LogWarning($"Expression library {libraryId} not found");
            return null;
        }
        var expr = library.GetExpr(exprName);
        if (expr == null)
        {
            Logger.LogWarning($"Expression {exprName} not found in library {libraryId}");
            return null;
        }
        

        if (ctx == null)
            ctx = new EvalContext();

        return expr.BaseEval(ctx);
    }
}
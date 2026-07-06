using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// Thrown when a DSL expression cannot be lexed or parsed. Carries a 1-based line/column
/// so authoring tools (and load-time errors) can point at the exact spot.
/// </summary>
public sealed class DslException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public DslException(string message, int line, int column, string source)
        : base(Format(message, line, column, source))
    {
        Line = line;
        Column = column;
    }

    private static string Format(string message, int line, int column, string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var snippet = (line >= 1 && line <= lines.Length) ? lines[line - 1] : "";
        var caret = new string(' ', Math.Max(0, column - 1)) + "^";
        return $"DSL error (line {line}, col {column}): {message}\n    {snippet}\n    {caret}";
    }
}

/// <summary>
/// The DSL front-end. Parses a small typed expression language and <b>desugars it to the existing
/// JSON expression tree</b>, which <see cref="ExpressionCompiler"/> then compiles into <c>Expr&lt;T&gt;</c>.
/// This is a new authoring surface over the same runtime — no new evaluation engine.
/// <para>
/// A field opts into the DSL by using a string that begins with <c>=</c> (spreadsheet-style), e.g.
/// <c>"= (80 + target.constitution) * footFactor(target)"</c>.
/// </para>
/// </summary>
public static class DslCompiler
{
    /// <summary>The marker character that flags a string field as DSL source.</summary>
    public const char Marker = '=';

    /// <summary>True if <paramref name="element"/> is a DSL string (a string beginning with '=').</summary>
    public static bool IsDslSource(JsonElement element, out string source)
    {
        source = string.Empty;
        if (element.ValueKind != JsonValueKind.String) return false;
        var s = element.GetString();
        if (s == null) return false;
        var trimmed = s.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != Marker) return false;
        source = trimmed.Substring(1);
        return true;
    }

    /// <summary>
    /// Convert a JSON expression tree back into DSL source text (the inverse of <see cref="Desugar"/>).
    /// Best-effort: throws <see cref="NotSupportedException"/> for ops with no faithful DSL form.
    /// Callers persisting the result should round-trip-verify it against <see cref="Desugar"/>.
    /// </summary>
    public static string ToDsl(JsonElement expression) => DslWriter.Write(expression);

    /// <summary>Parse DSL source and desugar it into an equivalent JSON expression element.</summary>
    public static JsonElement Desugar(string source)
    {
        var tokens = Lexer.Tokenize(source);
        var node = new Parser(tokens, source).ParseProgram();
        return JsonSerializer.SerializeToElement(node);
    }

    // Positional-argument resolution
    // The existing ops read named JSON properties (obj.GetProperty("part")). To allow positional
    // DSL calls like bp_health(target), we map op name -> ordered [ExprParam] names by reflecting
    // over the same attributes the source generator reads. Built once, cached.
    private static IReadOnlyDictionary<string, string[]>? _paramTable;

    internal static IReadOnlyDictionary<string, string[]> ParamTable =>
        _paramTable ??= BuildParamTable();

    private static Dictionary<string, string[]> BuildParamTable()
    {
        var table = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(ExpressionCompiler).Assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var ops = method.GetCustomAttributes<ExprOpAttribute>().ToArray();
                if (ops.Length == 0) continue;
                var paramNames = method.GetCustomAttributes<ExprParamAttribute>().Select(p => p.Name).ToArray();
                foreach (var op in ops)
                    foreach (var name in op.OpNames)
                        if (!table.ContainsKey(name))
                            table[name] = paramNames;
            }
        }
        return table;
    }
}

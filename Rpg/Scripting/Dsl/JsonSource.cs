using System.Collections.Immutable;
using System.Text.Json;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// Turns the JSON a data file carries into <see cref="AstNode"/>s.
/// <para>
/// JSON is a <em>carrier</em>, not a program representation: a string beginning with <c>=</c> is DSL
/// source and is lexed and parsed here; anything else is a literal and becomes the corresponding
/// literal node. There is no longer a second, object-shaped encoding of the language — an
/// <c>{"op": …}</c> tree is now a diagnostic rather than a program.
/// </para>
/// <para>
/// The point is that every expression in the system reaches <see cref="AstCompiler"/> the same way.
/// Literal handling used to be written twice (once per front end) and drift between them was the
/// source of most of the compiler's special cases.
/// </para>
/// </summary>
internal static class JsonSource
{
    /// <summary>The marker character that flags a string field as DSL source.</summary>
    public const char Marker = '=';

    /// <summary>True if <paramref name="element"/> is a DSL string (a string beginning with '=').</summary>
    public static bool IsDslSource(JsonElement element, out string source)
    {
        source = string.Empty;
        if (element.ValueKind != JsonValueKind.String) return false;
        if (element.GetString() is not { } text) return false;

        var trimmed = text.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != Marker) return false;

        source = trimmed[1..];
        return true;
    }

    /// <summary>
    /// Converts <paramref name="element"/> into the tree to lower. DSL strings are parsed, keeping
    /// their own text attached so failures inside them quote the right field.
    /// </summary>
    public static AstNode Parse(JsonElement element)
    {
        if (IsDslSource(element, out var source))
            return new SourceRoot(new Parser(Lexer.Tokenize(source), source).ParseProgram(), source);

        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new NumberLit(element.GetDouble(), Span.None);

            case JsonValueKind.True:
                return new BoolLit(true, Span.None);

            case JsonValueKind.False:
                return new BoolLit(false, Span.None);

            case JsonValueKind.Null:
                return new NullLit(Span.None);

            case JsonValueKind.String:
                return LiteralString(element.GetString()!);

            case JsonValueKind.Array:
                return new ArrayLit(
                    element.EnumerateArray().Select(Parse).ToImmutableArray(),
                    Span.None);

            default:
                throw new Exception(
                    $"An expression must be a literal or a DSL string beginning with '=', but this is a JSON object. "
                    + $"Write it as an expression, e.g. \"= {Suggest(element)}\". (got: {Abbreviate(element)})");
        }
    }

    /// <summary>
    /// A bare JSON string is a literal. The one exception is refused rather than reinterpreted: a
    /// string that is only a <c>$N</c> used to mean a variable, and silently turning it into the
    /// text "$0" would change what an unmigrated field computes without saying so.
    /// </summary>
    private static AstNode LiteralString(string text)
    {
        if (text.Length > 1 && text[0] == '$' && text.AsSpan(1).ToString().All(char.IsDigit))
            throw new Exception(
                $"\"{text}\" is a plain string here, not a variable. Write it as an expression: \"= {text}\".");

        return new StringLit(text, Span.None);
    }

    /// <summary>Turns a legacy <c>{"op": "x", …}</c> object into the call it would now be written as.</summary>
    private static string Suggest(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("op", out var op)
            || op.GetString() is not { } name)
            return "…";

        var arguments = element.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "op", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name);

        return $"{name}({string.Join(", ", arguments)})";
    }

    private static string Abbreviate(JsonElement element)
    {
        var text = element.GetRawText().ReplaceLineEndings(" ");
        return text.Length <= 120 ? text : text[..117] + "...";
    }
}

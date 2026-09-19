namespace Rpg.Scripting.Dsl;

/// <summary>
/// Thrown when an expression cannot be lexed, parsed or compiled. Carries a 1-based line/column and
/// renders the offending line with a caret under it.
/// </summary>
public sealed class DslException : Exception
{
    public int Line { get; }
    public int Column { get; }

    /// <summary>The message without the source snippet, for nesting inside another diagnostic.</summary>
    public string RawMessage { get; }

    public DslException(string message, int line, int column, string source)
        : base(Format(message, line, column, source))
    {
        Line = line;
        Column = column;
        RawMessage = message;
    }

    private static string Format(string message, int line, int column, string source)
    {
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        var snippet = (line >= 1 && line <= lines.Length) ? lines[line - 1] : "";
        var caret = new string(' ', Math.Max(0, column - 1)) + "^";
        return $"DSL error (line {line}, col {column}): {message}\n    {snippet}\n    {caret}";
    }
}

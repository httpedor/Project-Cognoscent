using System.Text;

namespace Rpg.Scripting.Dsl;

internal enum TokKind { Number, String, Dice, Ident, Var, Op, Eof }

internal readonly struct Token
{
    public readonly TokKind Kind;
    public readonly string Text;
    public readonly double Number;
    public readonly int Line;
    public readonly int Column;

    public Token(TokKind kind, string text, int line, int column, double number = 0)
    {
        Kind = kind;
        Text = text;
        Line = line;
        Column = column;
        Number = number;
    }

    public override string ToString() => $"{Kind}:'{Text}'";
}

/// <summary>Turns DSL source into a flat token list. Errors carry line/column.</summary>
internal static class Lexer
{
    // Multi-character operators, checked longest-first.
    private static readonly string[] MultiOps = { "<=", ">=", "==", "!=", "&&", "||", "=>", "::", ".." };
    private const string SingleOps = "+-*/<>()[]{},.?:=!%";

    public static List<Token> Tokenize(string src)
    {
        var tokens = new List<Token>();
        int i = 0, line = 1, col = 1;

        void Advance(int n = 1)
        {
            for (int k = 0; k < n; k++)
            {
                if (i < src.Length && src[i] == '\n') { line++; col = 1; }
                else col++;
                i++;
            }
        }

        while (i < src.Length)
        {
            char c = src[i];

            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { Advance(); continue; }

            int startLine = line, startCol = col;

            // Numbers (and dice literals like 1d6 / 2D8)
            if (char.IsDigit(c))
            {
                int start = i;
                while (i < src.Length && char.IsDigit(src[i])) Advance();
                // dice: <digits> d|D <digits>
                if (i < src.Length && (src[i] == 'd' || src[i] == 'D')
                    && i + 1 < src.Length && char.IsDigit(src[i + 1]))
                {
                    Advance(); // consume d
                    while (i < src.Length && char.IsDigit(src[i])) Advance();
                    tokens.Add(new Token(TokKind.Dice, src.Substring(start, i - start), startLine, startCol));
                    continue;
                }
                if (i < src.Length && src[i] == '.' && i + 1 < src.Length && char.IsDigit(src[i + 1]))
                {
                    Advance(); // consume '.'
                    while (i < src.Length && char.IsDigit(src[i])) Advance();
                }
                var numText = src.Substring(start, i - start);
                tokens.Add(new Token(TokKind.Number, numText, startLine, startCol, double.Parse(numText, System.Globalization.CultureInfo.InvariantCulture)));
                continue;
            }

            // Variables: $0, $1, ...
            if (c == '$')
            {
                Advance();
                int start = i;
                while (i < src.Length && char.IsDigit(src[i])) Advance();
                if (i == start)
                    throw new DslException("expected a variable index after '$' (e.g. $0)", startLine, startCol, src);
                tokens.Add(new Token(TokKind.Var, src.Substring(start, i - start), startLine, startCol));
                continue;
            }

            // Identifiers / keywords
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) Advance();
                tokens.Add(new Token(TokKind.Ident, src.Substring(start, i - start), startLine, startCol));
                continue;
            }

            // String literals
            if (c == '"' || c == '\'')
            {
                char quote = c;
                Advance();
                var sb = new StringBuilder();
                while (i < src.Length && src[i] != quote)
                {
                    if (src[i] == '\\' && i + 1 < src.Length)
                    {
                        Advance();
                        sb.Append(src[i] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', var e => e });
                    }
                    else sb.Append(src[i]);
                    Advance();
                }
                if (i >= src.Length)
                    throw new DslException("unterminated string literal", startLine, startCol, src);
                Advance(); // closing quote
                tokens.Add(new Token(TokKind.String, sb.ToString(), startLine, startCol));
                continue;
            }

            // Multi-char operators
            bool matched = false;
            foreach (var op in MultiOps)
            {
                if (i + op.Length <= src.Length && src.Substring(i, op.Length) == op)
                {
                    tokens.Add(new Token(TokKind.Op, op, startLine, startCol));
                    Advance(op.Length);
                    matched = true;
                    break;
                }
            }
            if (matched) continue;

            // Single-char operators
            if (SingleOps.IndexOf(c) >= 0)
            {
                tokens.Add(new Token(TokKind.Op, c.ToString(), startLine, startCol));
                Advance();
                continue;
            }

            throw new DslException($"unexpected character '{c}'", startLine, startCol, src);
        }

        tokens.Add(new Token(TokKind.Eof, "", line, col));
        return tokens;
    }
}

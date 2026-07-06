using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// Converts a JSON expression tree back into DSL source text — the inverse of
/// <see cref="DslCompiler.Desugar"/>. Best-effort: throws <see cref="NotSupportedException"/> for
/// ops it can't faithfully represent (callers should keep the original JSON in that case, and
/// should round-trip-verify the result for anything they intend to persist).
/// </summary>
internal static class DslWriter
{
    // Precedence (higher binds tighter). Used to decide when a sub-expression needs parentheses.
    private const int PTernary = 1, POr = 2, PAnd = 3, PEq = 4, PCmp = 5, PAdd = 6, PMul = 7, PUnary = 8, PAtom = 9;

    private static readonly HashSet<string> EntityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "target", "caller", "self", "target_component", "target_part"
    };

    public static string Write(JsonElement e) => Write(e, 0);

    private static string Write(JsonElement e, int parentPrec)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                return e.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.String:
                return WriteString(e.GetString()!);
            case JsonValueKind.Array:
                // A bare JSON array becomes a DSL array literal.
                return "[" + string.Join(", ", e.EnumerateArray().Select(x => Write(x, 0))) + "]";
            case JsonValueKind.Object:
                return WriteObject(e, parentPrec);
            default:
                throw new NotSupportedException($"Cannot express {e.ValueKind} in the DSL");
        }
    }

    private static string WriteString(string s)
    {
        if (s.StartsWith("$") && s.Length > 1 && s.Skip(1).All(char.IsDigit))
            return s; // variable reference
        if (EntityKeywords.Contains(s))
            return s.ToLowerInvariant(); // context keyword
        // String literal — single-quoted so it needs no escaping when embedded in a JSON string.
        return "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }

    private static string WriteObject(JsonElement e, int parentPrec)
    {
        if (!e.TryGetProperty("op", out var opEl) || opEl.ValueKind != JsonValueKind.String)
            throw new NotSupportedException("Object is not an expression (no string 'op')");
        var op = opEl.GetString()!.ToLowerInvariant();

        switch (op)
        {
            // ── arithmetic (n-ary, over "numbers") ──
            case "sum": case "plus": case "add": case "addition": case "+":
                return Nary(e, " + ", PAdd, parentPrec, "sum");
            case "sub": case "subtract": case "minus": case "subtraction": case "-":
                return Nary(e, " - ", PAdd, parentPrec, "sub");
            case "mul": case "multiply": case "times": case "multiplication": case "*":
                return Nary(e, " * ", PMul, parentPrec, "mul");
            case "div": case "divide": case "division": case "/":
                return Nary(e, " / ", PMul, parentPrec, "div");

            // ── comparisons / equality (binary, left/right) ──
            case ">": case "<": case ">=": case "<=":
                return Binary(e, " " + op + " ", PCmp, parentPrec);
            case "==": case "=":
                return Binary(e, " == ", PEq, parentPrec);
            case "!=":
                return Binary(e, " != ", PEq, parentPrec);

            // ── boolean ──
            case "and":
                return NaryConditions(e, " and ", PAnd, parentPrec);
            case "or":
                return NaryConditions(e, " or ", POr, parentPrec);
            case "not":
                return Paren(PUnary, parentPrec, "not " + Write(Req(e, "condition"), PUnary));

            // ── control ──
            case "if":
                return Paren(PTernary, parentPrec,
                    $"{Write(Req(e, "condition"), PTernary)} ? {Write(Req(e, "true"), PTernary)} : {Write(Req(e, "false"), PTernary)}");
            case "switch_number":
                return WriteSwitch(e);

            // ── stat access sugar ──
            case "stat": case "creature_stat": case "entity_stat": case "entitystat": case "creaturestat":
                return WriteStat(e);

            // ── lambda-taking array ops (body already uses $0, so no lambda needed) ──
            case "filter":
                return $"filter({Write(Req(e, "values"), 0)}, {Write(Req(e, "condition"), 0)})";
            case "map":
                return $"map({Write(Req(e, "values"), 0)}, {Write(Req(e, "expression"), 0)})";
            case "all":
                return $"all({Write(Req(e, "variables"), 0)}, {Write(Req(e, "condition"), 0)})";
            case "any":
                return $"any({Write(Req(e, "variables"), 0)}, {Write(Req(e, "condition"), 0)})";

            // ── library call ──
            case "function": case "run_function": case "call_function": case "run_expr": case "call_expr":
                return WriteLibraryCall(e);

            // ── everything else: positional call via the [ExprParam] order ──
            default:
                return WriteGenericCall(op, e);
        }
    }

    private static string WriteSwitch(JsonElement e)
    {
        var sb = new StringBuilder();
        sb.Append("switch ").Append(Write(Req(e, "value"), PAtom)).Append(" { ");
        var parts = new List<string>();
        if (e.TryGetProperty("cases", out var cases) && cases.ValueKind == JsonValueKind.Object)
            foreach (var c in cases.EnumerateObject())
            {
                if (!float.TryParse(c.Name, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    throw new NotSupportedException($"switch case key '{c.Name}' is not numeric");
                parts.Add($"{c.Name} => {Write(c.Value, 0)}");
            }
        if (e.TryGetProperty("default", out var def))
            parts.Add($"_ => {Write(def, 0)}");
        sb.Append(string.Join(", ", parts)).Append(" }");
        return sb.ToString();
    }

    private static string WriteStat(JsonElement e)
    {
        var name = Req(e, "stat");
        if (name.ValueKind != JsonValueKind.String)
            throw new NotSupportedException("stat name must be a literal string for member sugar");
        var statName = name.GetString()!;
        bool isIdent = statName.Length > 0 && (char.IsLetter(statName[0]) || statName[0] == '_')
                       && statName.All(c => char.IsLetterOrDigit(c) || c == '_');
        // Member sugar only when it's a plain `entity.stat` with no default override.
        if (isIdent && e.TryGetProperty("entity", out var entity) && !e.TryGetProperty("default", out _))
            return $"{Write(entity, PAtom)}.{statName}";
        return WriteGenericCall("stat", e);
    }

    private static string WriteLibraryCall(JsonElement e)
    {
        if (!e.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            throw new NotSupportedException("library call missing string 'id'");
        var id = idEl.GetString()!;
        if (!id.Contains(':'))
            throw new NotSupportedException("library call id has no namespace");
        var parts = id.Split(':', 2);
        var args = new List<string>();
        if (e.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array)
            foreach (var a in argsEl.EnumerateArray())
                args.Add(Write(a, 0));
        return $"{parts[0]}::{parts[1]}({string.Join(", ", args)})";
    }

    private static string WriteGenericCall(string op, JsonElement e)
    {
        if (!DslCompiler.ParamTable.TryGetValue(op, out var paramNames))
            throw new NotSupportedException($"op '{op}' has no positional form");

        // Positional args must be contiguous from the first parameter.
        int last = -1;
        for (int i = 0; i < paramNames.Length; i++)
            if (e.TryGetProperty(paramNames[i], out _)) last = i;
        if (last < 0)
            return $"{op}()";
        var args = new List<string>();
        for (int i = 0; i <= last; i++)
        {
            if (!e.TryGetProperty(paramNames[i], out var v))
                throw new NotSupportedException($"op '{op}' omits '{paramNames[i]}' before a later argument — no positional form");
            args.Add(Write(v, 0));
        }
        return $"{op}({string.Join(", ", args)})";
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    private static JsonElement Req(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v))
            throw new NotSupportedException($"expression missing required '{prop}'");
        return v;
    }

    private static string Nary(JsonElement e, string sep, int prec, int parentPrec, string callName)
    {
        var arr = Req(e, "numbers");
        // "numbers" may be a single array-producing expression rather than a literal array;
        // in that case fall back to the call form, e.g. sum(map(...)).
        if (arr.ValueKind != JsonValueKind.Array)
            return $"{callName}({Write(arr, 0)})";
        var items = arr.EnumerateArray().ToList();
        if (items.Count == 0) throw new NotSupportedException("empty n-ary expression");
        var pieces = new List<string>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            // Left-associative: the first operand binds normally, but any later operand at the same
            // precedence (e.g. a `/` inside a `*`, or a `-` inside a `+`) must be parenthesized to
            // preserve grouping — `a * (b / c)` is not `a * b / c`.
            int childParent = i == 0 ? prec : prec + 1;
            pieces.Add(Write(items[i], childParent));
        }
        return Paren(prec, parentPrec, string.Join(sep, pieces));
    }

    private static string NaryConditions(JsonElement e, string sep, int prec, int parentPrec)
    {
        var arr = Req(e, "conditions");
        if (arr.ValueKind != JsonValueKind.Array)
            throw new NotSupportedException("'conditions' must be an array");
        var pieces = arr.EnumerateArray().Select(x => Write(x, prec)).ToList();
        if (pieces.Count == 0) throw new NotSupportedException("empty boolean expression");
        return Paren(prec, parentPrec, string.Join(sep, pieces));
    }

    private static string Binary(JsonElement e, string sep, int prec, int parentPrec)
    {
        var left = Write(Req(e, "left"), prec + 1);
        var right = Write(Req(e, "right"), prec + 1);
        return Paren(prec, parentPrec, left + sep + right);
    }

    private static string Paren(int prec, int parentPrec, string s) => prec < parentPrec ? "(" + s + ")" : s;
}

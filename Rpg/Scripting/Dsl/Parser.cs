using System.Text.Json.Nodes;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// Recursive-descent / precedence-climbing parser that emits the existing JSON expression tree
/// directly (as <see cref="JsonNode"/>). Each grammar rule returns the JSON it desugars to.
/// </summary>
internal sealed class Parser
{
    private readonly List<Token> _tokens;
    private readonly string _source;
    private int _pos;

    // Lambda parameter names, innermost last. Resolving a bound name yields the matching $index,
    // matching the runtime's convention: the current element is variable 0 and existing vars shift up.
    private readonly List<string> _scope = new();

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "target", "caller", "self", "target_component", "target_part"
    };

    // Functions that take a collection plus a lambda/predicate, with their JSON property names.
    private static readonly Dictionary<string, (string CollKey, string BodyKey)> LambdaFns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["map"] = ("values", "expression"),
            ["filter"] = ("values", "condition"),
            ["all"] = ("variables", "condition"),
            ["any"] = ("variables", "condition"),
        };

    public Parser(List<Token> tokens, string source)
    {
        _tokens = tokens;
        _source = source;
    }

    public JsonNode ParseProgram()
    {
        var node = ParseExpr();
        if (Current.Kind != TokKind.Eof)
            throw Error($"unexpected '{Current.Text}' after expression");
        return node;
    }

    // ── token helpers ───────────────────────────────────────────────────────
    private Token Current => _tokens[_pos];
    private Token Peek(int n = 1) => _tokens[Math.Min(_pos + n, _tokens.Count - 1)];
    private Token Advance() => _tokens[_pos++];

    private bool IsOp(string op) => Current.Kind == TokKind.Op && Current.Text == op;
    private bool IsIdent(string kw) => Current.Kind == TokKind.Ident && string.Equals(Current.Text, kw, StringComparison.OrdinalIgnoreCase);

    private void Expect(string op)
    {
        if (!IsOp(op)) throw Error($"expected '{op}' but found '{Current.Text}'");
        Advance();
    }

    private DslException Error(string message) => new(message, Current.Line, Current.Column, _source);

    // ── grammar (lowest to highest precedence) ──────────────────────────────
    private JsonNode ParseExpr() => ParseTernary();

    private JsonNode ParseTernary()
    {
        var cond = ParseOr();
        if (IsOp("?"))
        {
            Advance();
            var whenTrue = ParseExpr();
            Expect(":");
            var whenFalse = ParseExpr();
            return new JsonObject { ["op"] = "if", ["condition"] = cond, ["true"] = whenTrue, ["false"] = whenFalse };
        }
        return cond;
    }

    private JsonNode ParseOr()
    {
        var left = ParseAnd();
        while (IsIdent("or") || IsOp("||"))
        {
            Advance();
            left = Combine("or", "conditions", left, ParseAnd());
        }
        return left;
    }

    private JsonNode ParseAnd()
    {
        var left = ParseEquality();
        while (IsIdent("and") || IsOp("&&"))
        {
            Advance();
            left = Combine("and", "conditions", left, ParseEquality());
        }
        return left;
    }

    private JsonNode ParseEquality()
    {
        var left = ParseComparison();
        while (IsOp("==") || IsOp("!="))
        {
            var op = Advance().Text;
            left = new JsonObject { ["op"] = op, ["left"] = left, ["right"] = ParseComparison() };
        }
        return left;
    }

    private JsonNode ParseComparison()
    {
        var left = ParseAdditive();
        while (IsOp("<") || IsOp("<=") || IsOp(">") || IsOp(">="))
        {
            var op = Advance().Text;
            left = new JsonObject { ["op"] = op, ["left"] = left, ["right"] = ParseAdditive() };
        }
        return left;
    }

    private JsonNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (IsOp("+") || IsOp("-"))
        {
            var op = Advance().Text;
            var right = ParseMultiplicative();
            left = op == "+" ? Combine("sum", "numbers", left, right)
                             : Combine("sub", "numbers", left, right);
        }
        return left;
    }

    private JsonNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (IsOp("*") || IsOp("/"))
        {
            var op = Advance().Text;
            var right = ParseUnary();
            left = op == "*" ? Combine("mul", "numbers", left, right)
                             : Combine("div", "numbers", left, right);
        }
        return left;
    }

    private JsonNode ParseUnary()
    {
        if (IsOp("-"))
        {
            Advance();
            var operand = ParseUnary();
            // Fold a literal negation; otherwise multiply by -1.
            if (operand is JsonValue v && v.TryGetValue<double>(out var d))
                return JsonValue.Create(-d);
            return new JsonObject { ["op"] = "mul", ["numbers"] = new JsonArray(JsonValue.Create(-1.0), operand) };
        }
        if (IsOp("!") || IsIdent("not"))
        {
            Advance();
            return new JsonObject { ["op"] = "not", ["condition"] = ParseUnary() };
        }
        return ParsePostfix();
    }

    private JsonNode ParsePostfix()
    {
        var node = ParsePrimary();
        while (IsOp("."))
        {
            Advance();
            if (Current.Kind != TokKind.Ident)
                throw Error("expected a stat name after '.'");
            var stat = Advance().Text;
            // x.foo  =>  read stat "foo" from entity x
            node = new JsonObject { ["op"] = "stat", ["entity"] = node, ["stat"] = stat };
        }
        return node;
    }

    private JsonNode ParsePrimary()
    {
        var tok = Current;
        switch (tok.Kind)
        {
            case TokKind.Number:
                Advance();
                return JsonValue.Create(tok.Number);
            case TokKind.String:
                Advance();
                return JsonValue.Create(tok.Text);
            case TokKind.Dice:
                Advance();
                return JsonValue.Create(tok.Text); // e.g. "1d6" — RandomExpr handles it
            case TokKind.Var:
                Advance();
                return JsonValue.Create("$" + tok.Text);
            case TokKind.Ident:
                return ParseIdent();
            case TokKind.Op when tok.Text == "(":
                Advance();
                var inner = ParseExpr();
                Expect(")");
                return inner;
            case TokKind.Op when tok.Text == "[":
                return ParseArrayLiteral();
            default:
                throw Error($"unexpected '{tok.Text}'");
        }
    }

    private JsonNode ParseIdent()
    {
        var name = Advance().Text;

        // boolean literals
        if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true);
        if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false);

        // switch <value> { k => e, _ => default }
        if (string.Equals(name, "switch", StringComparison.OrdinalIgnoreCase))
            return ParseSwitch();

        // qualified library call: namespace::function(args)
        if (IsOp("::"))
        {
            Advance();
            if (Current.Kind != TokKind.Ident)
                throw Error("expected a function name after '::'");
            var fn = Advance().Text;
            return ParseLibraryCall($"{name}:{fn}");
        }

        // function call
        if (IsOp("("))
            return ParseCall(name);

        // lambda-bound variable?
        var scopeIndex = ResolveScope(name);
        if (scopeIndex >= 0)
            return JsonValue.Create("$" + scopeIndex);

        // context keyword (target / caller / ...)
        if (Keywords.Contains(name))
            return JsonValue.Create(name.ToLowerInvariant());

        throw Error($"unknown identifier '{name}' (use $N for variables, or a known keyword)");
    }

    private JsonNode ParseArrayLiteral()
    {
        Expect("[");
        var arr = new JsonArray();
        while (!IsOp("]"))
        {
            arr.Add(ParseExpr());
            if (IsOp(",")) Advance();
            else break;
        }
        Expect("]");
        return arr;
    }

    private JsonNode ParseSwitch()
    {
        var value = ParseExpr();
        Expect("{");
        var cases = new JsonObject();
        JsonNode? defaultCase = null;
        while (!IsOp("}"))
        {
            string? key = null;
            if (Current.Kind == TokKind.Ident && Current.Text == "_")
                Advance();
            else if (Current.Kind == TokKind.Number)
                key = Advance().Text;
            else
                throw Error("switch case key must be a number or '_'");
            Expect("=>");
            var body = ParseExpr();
            if (key == null) defaultCase = body;
            else cases[key] = body;
            if (IsOp(",")) Advance();
            else break;
        }
        Expect("}");
        var node = new JsonObject { ["op"] = "switch_number", ["value"] = value, ["cases"] = cases };
        if (defaultCase != null) node["default"] = defaultCase;
        return node;
    }

    private JsonNode ParseLibraryCall(string id)
    {
        var args = ParseArgList();
        var node = new JsonObject { ["op"] = "call_expr", ["id"] = id };
        var argArr = new JsonArray();
        foreach (var a in args) argArr.Add(a);
        node["args"] = argArr;
        return node;
    }

    private JsonNode ParseCall(string name)
    {
        // if(cond, a, b)
        if (string.Equals(name, "if", StringComparison.OrdinalIgnoreCase))
        {
            var a = ParseArgList();
            if (a.Count != 3)
                throw Error("if(...) takes exactly 3 arguments: condition, true, false");
            return new JsonObject { ["op"] = "if", ["condition"] = a[0], ["true"] = a[1], ["false"] = a[2] };
        }

        // map/filter/all/any: (collection, lambda-or-expr)
        if (LambdaFns.TryGetValue(name, out var keys))
            return ParseLambdaCall(name, keys.CollKey, keys.BodyKey);

        // generic op: positional args mapped to declared parameter names
        var args = ParseArgList();
        var node = new JsonObject { ["op"] = name.ToLowerInvariant() };
        if (args.Count == 0) return node;

        if (!DslCompiler.ParamTable.TryGetValue(name, out var paramNames) || paramNames.Length == 0)
            throw Error($"unknown op '{name}' (no positional parameters known); check the op name");
        if (args.Count > paramNames.Length)
            throw Error($"'{name}' accepts at most {paramNames.Length} argument(s) but got {args.Count}");
        for (int k = 0; k < args.Count; k++)
            node[paramNames[k]] = args[k];
        return node;
    }

    private JsonNode ParseLambdaCall(string name, string collKey, string bodyKey)
    {
        Expect("(");
        var collection = ParseExpr();
        Expect(",");
        JsonNode body;
        // optional `param =>` lambda; otherwise the body may reference $0 directly
        if (Current.Kind == TokKind.Ident && Peek().Kind == TokKind.Op && Peek().Text == "=>")
        {
            var param = Advance().Text;
            Advance(); // =>
            _scope.Add(param);
            body = ParseExpr();
            _scope.RemoveAt(_scope.Count - 1);
        }
        else
        {
            body = ParseExpr();
        }
        Expect(")");
        return new JsonObject { ["op"] = name.ToLowerInvariant(), [collKey] = collection, [bodyKey] = body };
    }

    private List<JsonNode> ParseArgList()
    {
        Expect("(");
        var args = new List<JsonNode>();
        while (!IsOp(")"))
        {
            args.Add(ParseExpr());
            if (IsOp(",")) Advance();
            else break;
        }
        Expect(")");
        return args;
    }

    /// <summary>Resolve a lambda-bound name to its variable index (innermost = 0), or -1 if not bound.</summary>
    private int ResolveScope(string name)
    {
        for (int k = _scope.Count - 1; k >= 0; k--)
            if (_scope[k] == name)
                return _scope.Count - 1 - k;
        return -1;
    }

    /// <summary>
    /// Append <paramref name="right"/> to an existing n-ary node with the same op (flattening
    /// chains like a+b+c into one array); otherwise create a fresh 2-element node.
    /// </summary>
    private static JsonNode Combine(string op, string arrayKey, JsonNode left, JsonNode right)
    {
        if (left is JsonObject obj && obj["op"] is JsonValue v && v.TryGetValue<string>(out var existing)
            && existing == op && obj[arrayKey] is JsonArray arr)
        {
            arr.Add(right);
            return obj;
        }
        return new JsonObject { ["op"] = op, [arrayKey] = new JsonArray(left, right) };
    }
}

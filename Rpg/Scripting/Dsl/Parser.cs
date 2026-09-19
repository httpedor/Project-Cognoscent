using System.Collections.Immutable;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// Recursive-descent / precedence-climbing parser producing a typed <see cref="AstNode"/> tree.
/// <para>
/// Every node records where it came from, so a failure later in compilation can be reported against
/// the source text rather than against an anonymous subtree.
/// </para>
/// </summary>
internal sealed class Parser
{
    private readonly List<Token> _tokens;
    private readonly string _source;
    private int _pos;

    /// <summary>
    /// Lambda parameter names, innermost last. A bound name resolves to its slot index, matching the
    /// runtime convention that the innermost binding is <c>$0</c> and outer ones shift up.
    /// </summary>
    private readonly List<string> _scope = new();

    private static readonly HashSet<string> ContextKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "target", "caller", "self", "target_component", "target_part"
    };

    public Parser(List<Token> tokens, string source)
    {
        _tokens = tokens;
        _source = source;
    }

    public AstNode ParseProgram()
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
    private Span Here => new(Current.Line, Current.Column);

    private bool IsOp(string op) => Current.Kind == TokKind.Op && Current.Text == op;
    private bool IsIdent(string kw) => Current.Kind == TokKind.Ident && string.Equals(Current.Text, kw, StringComparison.OrdinalIgnoreCase);

    private void Expect(string op)
    {
        if (!IsOp(op)) throw Error($"expected '{op}' but found '{Current.Text}'");
        Advance();
    }

    private DslException Error(string message) => new(message, Current.Line, Current.Column, _source);

    // ── grammar (lowest to highest precedence) ──────────────────────────────
    private AstNode ParseExpr() => ParseTernary();

    private AstNode ParseTernary()
    {
        var condition = ParseOr();
        if (!IsOp("?")) return condition;

        var span = Here;
        Advance();
        var whenTrue = ParseExpr();
        Expect(":");
        var whenFalse = ParseExpr();
        return new Call("if", ImmutableArray.Create(condition, whenTrue, whenFalse), span);
    }

    private AstNode ParseOr()
    {
        var left = ParseAnd();
        while (IsIdent("or") || IsOp("||"))
        {
            var span = Here;
            Advance();
            left = Flatten("or", left, ParseAnd(), span);
        }
        return left;
    }

    private AstNode ParseAnd()
    {
        var left = ParseEquality();
        while (IsIdent("and") || IsOp("&&"))
        {
            var span = Here;
            Advance();
            left = Flatten("and", left, ParseEquality(), span);
        }
        return left;
    }

    private AstNode ParseEquality()
    {
        var left = ParseComparison();
        while (IsOp("==") || IsOp("!="))
        {
            var span = Here;
            var op = Advance().Text;
            left = new Call(op, ImmutableArray.Create(left, ParseComparison()), span);
        }
        return left;
    }

    private AstNode ParseComparison()
    {
        var left = ParseAdditive();
        while (IsOp("<") || IsOp("<=") || IsOp(">") || IsOp(">="))
        {
            var span = Here;
            var op = Advance().Text;
            left = new Call(op, ImmutableArray.Create(left, ParseAdditive()), span);
        }
        return left;
    }

    private AstNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (IsOp("+") || IsOp("-"))
        {
            var span = Here;
            var op = Advance().Text;
            left = Flatten(op == "+" ? "sum" : "sub", left, ParseMultiplicative(), span);
        }
        return left;
    }

    private AstNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (IsOp("*") || IsOp("/"))
        {
            var span = Here;
            var op = Advance().Text;
            left = Flatten(op == "*" ? "mul" : "div", left, ParseUnary(), span);
        }
        return left;
    }

    private AstNode ParseUnary()
    {
        if (IsOp("-"))
        {
            var span = Here;
            Advance();
            var operand = ParseUnary();
            // Fold a literal negation; otherwise multiply by -1.
            if (operand is NumberLit literal)
                return new NumberLit(-literal.Value, span);
            return new Call("mul",
                ImmutableArray.Create<AstNode>(
                    new ArrayLit(ImmutableArray.Create<AstNode>(new NumberLit(-1, span), operand), span)),
                span);
        }

        if (IsOp("!") || IsIdent("not"))
        {
            var span = Here;
            Advance();
            return new Call("not", ImmutableArray.Create(ParseUnary()), span);
        }

        return ParsePostfix();
    }

    private AstNode ParsePostfix()
    {
        var node = ParsePrimary();
        while (IsOp("."))
        {
            var span = Here;
            Advance();
            if (Current.Kind != TokKind.Ident)
                throw Error("expected a member name after '.'");
            var name = Advance().Text;

            // Method-call sugar: the receiver becomes the first positional argument, so
            // `x.fn(a, b)` is exactly `fn(x, a, b)` and `x.ns::fn(a)` is exactly `ns::fn(x, a)`.
            if (IsOp("::"))
            {
                Advance();
                if (Current.Kind != TokKind.Ident)
                    throw Error("expected a function name after '::'");
                var function = Advance().Text;
                node = ParseLibraryCall($"{name}:{function}", span, node);
            }
            else if (IsOp("("))
            {
                node = ParseCall(name, span, node);
            }
            else
            {
                // `x.foo` reads stat "foo" from entity x. `stat` declares (name, entity), so the
                // receiver is the second argument.
                node = new Call("stat",
                    ImmutableArray.Create<AstNode>(new StringLit(name, span), node), span);
            }
        }
        return node;
    }

    private AstNode ParsePrimary()
    {
        var token = Current;
        var span = new Span(token.Line, token.Column);

        switch (token.Kind)
        {
            case TokKind.Number:
                Advance();
                return new NumberLit(token.Number, span);
            case TokKind.String:
                Advance();
                return new StringLit(token.Text, span);
            case TokKind.Dice:
                Advance();
                return new DiceLit(token.Text, span);
            case TokKind.Var:
                Advance();
                return new VarRef(int.Parse(token.Text, System.Globalization.CultureInfo.InvariantCulture), span);
            case TokKind.Ident:
                return ParseIdent();
            case TokKind.Op when token.Text == "(":
                Advance();
                var inner = ParseExpr();
                Expect(")");
                return inner;
            case TokKind.Op when token.Text == "[":
                return ParseArrayLiteral();
            default:
                throw Error($"unexpected '{token.Text}'");
        }
    }

    private AstNode ParseIdent()
    {
        var span = Here;
        var name = Advance().Text;

        if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase)) return new BoolLit(true, span);
        if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase)) return new BoolLit(false, span);

        if (string.Equals(name, "switch", StringComparison.OrdinalIgnoreCase))
            return ParseSwitch(span);

        // Qualified library call: namespace::function(args)
        if (IsOp("::"))
        {
            Advance();
            if (Current.Kind != TokKind.Ident)
                throw Error("expected a function name after '::'");
            var function = Advance().Text;
            return ParseLibraryCall($"{name}:{function}", span);
        }

        if (IsOp("("))
            return ParseCall(name, span);

        var bound = ResolveScope(name);
        if (bound >= 0)
            return new VarRef(bound, span);

        if (ContextKeywords.Contains(name))
            return new ContextRef(name.ToLowerInvariant(), span);

        throw new DslException(
            $"unknown identifier '{name}' (use $N for variables, or a known keyword)",
            span.Line, span.Column, _source);
    }

    private AstNode ParseArrayLiteral()
    {
        var span = Here;
        Expect("[");
        var items = ImmutableArray.CreateBuilder<AstNode>();
        while (!IsOp("]"))
        {
            items.Add(ParseExpr());
            if (IsOp(",")) Advance();
            else break;
        }
        Expect("]");
        return new ArrayLit(items.ToImmutable(), span);
    }

    /// <summary>
    /// <c>switch v { 0 =&gt; a, 1..5 =&gt; b, _ =&gt; c }</c>. A case key is a number, an inclusive
    /// <c>min..max</c> range, or <c>_</c> for the fallback.
    /// </summary>
    private AstNode ParseSwitch(Span span)
    {
        var value = ParseExpr();
        Expect("{");

        var cases = ImmutableArray.CreateBuilder<SwitchCase>();
        AstNode? fallback = null;

        while (!IsOp("}"))
        {
            var isFallback = Current.Kind == TokKind.Ident && Current.Text == "_";
            var (min, max) = isFallback ? (0f, 0f) : ParseCaseKey();
            if (isFallback) Advance();

            Expect("=>");
            var body = ParseExpr();

            if (isFallback) fallback = body;
            else cases.Add(new SwitchCase(min, max, body));

            if (IsOp(",")) Advance();
            else break;
        }

        Expect("}");
        return new Switch(value, cases.ToImmutable(), fallback, span);
    }

    /// <summary>Reads a case key: either <c>n</c> or <c>min..max</c>, both possibly negated.</summary>
    private (float Min, float Max) ParseCaseKey()
    {
        var min = ParseCaseBound();
        if (!IsOp("..")) return (min, min);

        Advance();
        var max = ParseCaseBound();
        if (max < min)
            throw Error($"switch range {min}..{max} is empty; the upper bound must not be below the lower one");
        return (min, max);
    }

    private float ParseCaseBound()
    {
        var negative = IsOp("-");
        if (negative) Advance();
        if (Current.Kind != TokKind.Number)
            throw Error("switch case key must be a number, a min..max range, or '_'");
        var value = (float)Advance().Number;
        return negative ? -value : value;
    }

    private AstNode ParseLibraryCall(string id, Span span, AstNode? receiver = null)
    {
        var arguments = ParseArgList(receiver);
        // call_expr declares (id, args, library); the whole argument list is one array.
        return new Call("call_expr",
            ImmutableArray.Create<AstNode>(
                new StringLit(id, span),
                new ArrayLit(arguments, span)),
            span);
    }

    /// <summary>
    /// Parses <c>name(...)</c>. <paramref name="receiver"/>, when present, comes from method-call
    /// sugar and is spliced in as the first argument.
    /// </summary>
    private AstNode ParseCall(string name, Span span, AstNode? receiver = null)
    {
        // array_concat(a, b, …) is variadic sugar for concat([a, b, …]) — the op itself takes one
        // argument, an array of arrays.
        if (string.Equals(name, "array_concat", StringComparison.OrdinalIgnoreCase))
        {
            var parts = ParseArgList(receiver);
            return new Call("concat",
                ImmutableArray.Create<AstNode>(new ArrayLit(parts, span)), span);
        }

        return new Call(name.ToLowerInvariant(), ParseArgList(receiver), span);
    }

    /// <summary>Reads a parenthesised argument list, splicing in a method-call receiver.</summary>
    private ImmutableArray<AstNode> ParseArgList(AstNode? receiver = null)
    {
        Expect("(");
        var arguments = ImmutableArray.CreateBuilder<AstNode>();
        if (receiver is not null) arguments.Add(receiver);

        while (!IsOp(")"))
        {
            arguments.Add(ParseArgument());
            if (IsOp(",")) Advance();
            else break;
        }

        Expect(")");
        return arguments.ToImmutable();
    }

    /// <summary>
    /// One argument. <c>p =&gt; body</c> names the function's parameter; any other expression is an
    /// ordinary argument, and becomes a function body only if the op it lands on wants one there —
    /// which is decided during overload resolution, not here.
    /// </summary>
    private AstNode ParseArgument()
    {
        if (Current.Kind == TokKind.Ident && Peek().Kind == TokKind.Op && Peek().Text == "=>")
        {
            var span = Here;
            var parameter = Advance().Text;
            Advance(); // =>

            _scope.Add(parameter);
            var body = ParseExpr();
            _scope.RemoveAt(_scope.Count - 1);

            return new Lambda(body, span);
        }

        return ParseExpr();
    }

    /// <summary>Resolves a lambda-bound name to its slot index (innermost = 0), or -1.</summary>
    private int ResolveScope(string name)
    {
        for (var i = _scope.Count - 1; i >= 0; i--)
            if (_scope[i] == name)
                return _scope.Count - 1 - i;
        return -1;
    }

    /// <summary>
    /// Builds an n-ary call, folding chains so that <c>a + b + c</c> becomes one <c>sum</c> over
    /// three operands rather than nested two-operand sums.
    /// </summary>
    private static AstNode Flatten(string op, AstNode left, AstNode right, Span span)
    {
        if (left is Call existing
            && existing.Name == op
            && existing.Arguments.Length == 1
            && existing.Arguments[0] is ArrayLit operands)
        {
            return new Call(op,
                ImmutableArray.Create<AstNode>(new ArrayLit(operands.Items.Add(right), operands.Span)),
                existing.Span);
        }

        return new Call(op,
            ImmutableArray.Create<AstNode>(new ArrayLit(ImmutableArray.Create(left, right), span)),
            span);
    }
}

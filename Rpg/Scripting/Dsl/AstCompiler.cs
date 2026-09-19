using Rpg.Entities;
using Rpg.Scripting.Types;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// Carries a compile-time failure together with where in the source it happened, so the position is
/// still available once the whole expression has been walked. Formatted into a
/// <see cref="DslException"/> at the nearest enclosing <see cref="SourceRoot"/>; nested occurrences
/// keep their own message unformatted so they read cleanly when the resolver quotes them as the
/// reason an overload was rejected.
/// </summary>
internal class DslCompileException : Exception
{
    public Span Span { get; }
    public DslCompileException(string message, Span span) : base(message) => Span = span;
}

/// <summary>
/// A positioned failure that no other overload could resolve, so overload resolution should stop
/// rather than record it as one candidate's rejection reason.
/// </summary>
internal sealed class FatalDslCompileException : DslCompileException, IFatalCompileError
{
    public FatalDslCompileException(string message, Span span) : base(message, span) { }
}

/// <summary>
/// Lowers an <see cref="AstNode"/> tree to executable expressions, directed by the type each
/// position expects.
/// <para>
/// This is the whole compiler. There used to be a second one — a parallel set of JSON-shaped
/// entry points, each re-deciding what a literal meant in its own category, with the meta-ops
/// duplicated across them. Both front ends now produce the same nodes (see <see cref="JsonSource"/>)
/// and meet here, so a rule about what a bare number means in a condition is written once.
/// </para>
/// </summary>
internal static class AstCompiler
{
    /// <summary>Parses and compiles DSL source at a position expecting <paramref name="expected"/>.</summary>
    public static BaseExpr CompileSource(string source, ExprType? expected)
    {
        var ast = new Parser(Lexer.Tokenize(source), source).ParseProgram();
        return Format(() => Compile(ast, expected, source), source);
    }

    /// <summary>
    /// Compiles an already-parsed tree — the JSON literal bridge's entry point. Failures inside a
    /// <see cref="SourceRoot"/> are formatted against that node's text; failures in the literal
    /// structure around it have no text to quote and are reported plainly.
    /// </summary>
    public static BaseExpr CompileNode(AstNode node, ExprType? expected)
        => Format(() => Compile(node, expected, source: null), source: null);

    /// <summary>
    /// Runs <paramref name="body"/>, turning a positioned failure into the caret-and-snippet form
    /// when there is source text it belongs to.
    /// </summary>
    private static BaseExpr Format(Func<BaseExpr> body, string? source)
    {
        try
        {
            return body();
        }
        catch (DslCompileException e) when (source != null && e.Span.IsKnown)
        {
            throw new DslException(e.Message, e.Span.Line, e.Span.Column, source);
        }
        catch (DslCompileException e)
        {
            throw new Exception(e.Message);
        }
    }

    /// <summary>
    /// Compiles one node, tagging any failure with its position. A failure that already carries a
    /// position keeps it, so the innermost node that actually went wrong is the one reported.
    /// </summary>
    private static BaseExpr Compile(AstNode node, ExprType? expected, string? source)
    {
        try
        {
            return Lower(node, expected, source);
        }
        catch (DslCompileException)
        {
            throw;
        }
        catch (DslException)
        {
            throw;
        }
        catch (Exception e)
        {
            // Keep an unrecoverable error unrecoverable as it gains its position, so an enclosing
            // call does not absorb it while trying its other overloads.
            throw e is IFatalCompileError
                ? new FatalDslCompileException(e.Message, node.Span)
                : new DslCompileException(e.Message, node.Span);
        }
    }

    private static BaseExpr Lower(AstNode node, ExprType? expected, string? source)
    {
        // A position that wants a function takes any expression as its body: `filter(xs, $0 > 1)`
        // is the same as `filter(xs, e => e > 1)`. Which parameter is a function comes from the
        // resolved signature, so the wrapping happens here rather than being guessed while parsing.
        if (expected is ExprType.Fn && node is not Lambda)
            return LowerLambda(new Lambda(node, node.Span), expected, source);

        return node switch
        {
            SourceRoot root => Format(() => Compile(root.Body, expected, root.Text), root.Text),
            NumberLit number => LowerNumber(number, expected),
            StringLit text => LowerString(text, expected),
            BoolLit boolean => LowerBool(boolean, expected),
            NullLit empty => LowerNull(empty, expected),
            DiceLit dice => new RandomExpr(dice.Notation),
            VarRef variable => LowerVariable(variable, expected),
            ContextRef context => LowerContext(context, expected),
            ArrayLit array => LowerArray(array, expected, source),
            Lambda lambda => LowerLambda(lambda, expected, source),
            Switch choice => LowerSwitch(choice, expected, source),
            Call call => LowerCall(call, expected, source),
            _ => throw new DslCompileException($"cannot compile {node.GetType().Name}", node.Span)
        };
    }

    // ── literals ────────────────────────────────────────────────────────────

    private static BaseExpr LowerNumber(NumberLit node, ExprType? expected)
    {
        // A bare number in a condition position is a probability.
        if (expected is ExprType.Prim { Kind: PrimKind.Bool })
        {
            if (node.Value is < 0 or > 1)
                throw new DslCompileException(
                    $"a number used as a condition is a probability and must be between 0 and 1, but this is {node.Value}",
                    node.Span);
            return new RandomConditionExpr(new ConstNumberExpr((float)node.Value));
        }

        return new ConstNumberExpr((float)node.Value);
    }

    private static BaseExpr LowerString(StringLit node, ExprType? expected)
    {
        switch (expected)
        {
            case ExprType.Prim { Kind: PrimKind.Number }:
                return Number(node);

            case ExprType.Prim { Kind: PrimKind.Bool }:
                if (bool.TryParse(node.Value, out var flag)) return new ConstConditionExpr(flag);
                if (node.Value.EndsWith('%') && TryParse(node.Value[..^1], out var percent))
                    return new RandomConditionExpr(new ConstNumberExpr(percent / 100f));
                throw new DslCompileException($"'{node.Value}' is not a condition", node.Span);
        }

        // A string naming an enum member or a compendium entry stays a string here; turning it into
        // the thing it names is an implicit conversion (see ExprTypes.Adapt), so a literal id and a
        // computed one go through the same rule.
        return new StringLiteralExpr(node.Value);
    }

    /// <summary>
    /// A string in number position. Besides a plain numeral this accepts the range shorthands
    /// (<c>1d6</c>, <c>2:3</c>, <c>3-10</c>) that data files use for rolled values.
    /// </summary>
    private static BaseExpr Number(StringLit node)
    {
        if (TryParse(node.Value, out var parsed))
            return new ConstNumberExpr(parsed);

        try
        {
            return new RandomExpr(node.Value);
        }
        catch (ArgumentException)
        {
            throw new DslCompileException(
                $"'{node.Value}' is not a number or a range (expected e.g. 3, 1d6, 2:5)", node.Span);
        }
    }

    private static bool TryParse(string text, out float value) =>
        float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// <c>false</c> in effect position means "do nothing" — the spelling data files use for an
    /// effect field that is switched off.
    /// </summary>
    private static BaseExpr LowerBool(BoolLit node, ExprType? expected)
        => expected is ExprType.Prim { Kind: PrimKind.Void } && !node.Value
            ? new NoEffectExpr()
            : new ConstConditionExpr(node.Value);

    /// <summary>
    /// JSON <c>null</c>: the absent value of whatever the position wants. It has no DSL spelling,
    /// so this only ever fires for a literal field.
    /// </summary>
    private static BaseExpr LowerNull(NullLit node, ExprType? expected) => expected switch
    {
        ExprType.Prim { Kind: PrimKind.Void } => new NoEffectExpr(),
        ExprType.Prim { Kind: PrimKind.Bool } => new ConstConditionExpr(false),
        ExprType.Ref reference when typeof(Component).IsAssignableFrom(reference.Clr) => new NoComponentExpr(),
        ExprType.Ref reference when typeof(Entity).IsAssignableFrom(reference.Clr) => new NoEntityExpr(),
        _ => throw new DslCompileException(
            $"null is not a valid {expected?.ToString() ?? "expression"}", node.Span)
    };

    // ── references ──────────────────────────────────────────────────────────

    private static BaseExpr LowerVariable(VarRef node, ExprType? expected)
    {
        // A variable in array position iterates the slot; elsewhere it is cast to what is wanted.
        if (expected is ExprType.Arr array)
            return ExprFactory.New(typeof(VarArrayExpr<>), ExprTypes.ElementClr(array.Element), node.Index);

        return ExprFactory.New(typeof(VarExpr<>), ExprTypes.ToClr(expected ?? ExprType.Unknown), node.Index);
    }

    private static BaseExpr LowerContext(ContextRef node, ExprType? expected)
    {
        var wantsComponent = expected is ExprType.Ref reference
                             && typeof(Component).IsAssignableFrom(reference.Clr);

        switch (node.Name)
        {
            case "caller":
            case "self":
                return new CallerEntityExpr();

            case "target":
                // In a component position `target` is the targeted component, not the entity.
                return wantsComponent ? new TargetComponentExpr() : new TargetEntityExpr();

            case "target_component":
            case "target_part":
                return wantsComponent ? new TargetComponentExpr() : new TargetComponentEntityExpr();

            default:
                throw new FatalDslCompileException($"unknown context symbol '{node.Name}'", node.Span);
        }
    }

    // ── composites ──────────────────────────────────────────────────────────

    private static BaseExpr LowerArray(ArrayLit node, ExprType? expected, string? source)
    {
        var element = expected is ExprType.Arr array ? array.Element : ExprType.Unknown;

        // Each item is adapted to the element type: an array literal is a single typed array, so a
        // BodyPart[] sitting in an any[] has to be bridged rather than stored as-is.
        var items = node.Items
            .Select(item => ExprTypes.Adapt(Compile(item, element, source), element))
            .ToArray();

        // An array of effects is not an array of values: enumerating a ConstArrayExpr evaluates its
        // items, which for effects would *run* them and yield nulls to whoever asked for the list.
        // EffectArray hands the effects over unevaluated, which is what `composite([…])` needs.
        if (element is ExprType.Prim { Kind: PrimKind.Void })
            return new EffectArray(items.Cast<EffectExpr>().ToArray());

        var elementClr = ExprTypes.ElementClr(element);
        return ExprFactory.NewFromArray(typeof(ConstArrayExpr<>), elementClr,
            ExprFactory.TypedArray(ExprFactory.Close(typeof(Expr<>), elementClr), items));
    }

    private static BaseExpr LowerLambda(Lambda node, ExprType? expected, string? source)
    {
        if (expected is not ExprType.Fn function)
        {
            // The parser wraps the trailing argument of map/filter/... as a lambda; reaching here
            // means the resolved overload wants a plain value there instead.
            return Compile(node.Body, expected, source);
        }

        var resultHint = function.Result is ExprType.Any or ExprType.Var ? null : function.Result;
        var body = Compile(node.Body, resultHint, source);
        var bodyClr = ExprTypes.ToClr(ExprTypes.TypeOf(body));

        return ExprFactory.New(typeof(LambdaExpr<>), bodyClr, body, function.Params.Length);
    }

    /// <summary>
    /// Lowers a case table. Exact keys become a dictionary lookup; the presence of any
    /// <c>min..max</c> arm makes it an ordered scan of inclusive bounds instead, since ranges have
    /// to be tested in the order they were written.
    /// </summary>
    private static BaseExpr LowerSwitch(Switch node, ExprType? expected, string? source)
    {
        var result = expected ?? ExprType.Unknown;
        var resultClr = ExprTypes.ToClr(result);
        var resultExprType = ExprFactory.Close(typeof(Expr<>), resultClr);

        var value = ExprTypes.Adapt(Compile(node.Value, ExprType.Number, source), ExprType.Number);
        var fallback = node.Default is null
            ? null
            : ExprTypes.Adapt(Compile(node.Default, result, source), result);

        BaseExpr Arm(SwitchCase arm) => ExprTypes.Adapt(Compile(arm.Body, result, source), result);

        if (node.Cases.Any(c => c.IsRange))
        {
            var tuple = typeof(ValueTuple<,,>).MakeGenericType(typeof(float), typeof(float), resultExprType);
            var cases = (System.Collections.IList)Activator.CreateInstance(
                ExprFactory.Close(typeof(List<>), tuple))!;

            foreach (var arm in node.Cases)
                cases.Add(Activator.CreateInstance(tuple, arm.Min, arm.Max, Arm(arm)));

            return ExprFactory.New(typeof(SwitchRangeExpr<>), resultClr, value, cases, fallback);
        }

        var exact = (System.Collections.IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(float), resultExprType))!;

        foreach (var arm in node.Cases)
            exact[arm.Min] = Arm(arm);

        return ExprFactory.New(typeof(SwitchNumberExpr<>), resultClr, value, exact, fallback);
    }

    private static BaseExpr LowerCall(Call node, ExprType? expected, string? source)
        => OpResolver.Resolve(
            node.Name, node.Arguments, expected,
            (argument, hint) => Compile(argument, hint, source));
}

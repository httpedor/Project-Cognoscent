using System.Collections.Immutable;

namespace Rpg.Scripting.Types;

/// <summary>
/// Registers the polymorphic operations — <c>if</c>, <c>map</c>, <c>filter</c> and friends.
/// <para>
/// These are the ops that could not be registered before, because the old registry keyed on a
/// closed <c>ExprCategory</c> enum and had no way to say "returns whatever its branches return" or
/// "takes an array of a and produces an array of b". They therefore lived in two hand-maintained
/// switch statements in the compiler (<c>CheckForMetaExpr</c> and <c>CheckForMetaArrays</c>) that
/// had to be kept in agreement, with a documented ordering hazard between them.
/// </para>
/// <para>
/// With type variables in the signature language they are ordinary registrations. They are written
/// by hand here rather than generated because they construct open generic expression types
/// (<c>ConditionalExpr&lt;T&gt;</c>) whose type argument is only known after inference.
/// </para>
/// </summary>
public static class MetaOps
{
    private static bool _registered;

    // Type variables used across the signatures below. Each op's variables are independent; the
    // resolver builds a fresh substitution per call site.
    private static readonly ExprType A = new ExprType.Var(0);
    private static readonly ExprType B = new ExprType.Var(1);

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        // if(condition, true, false) : (bool, a, a) -> a
        Register(
            names: new[] { "if" },
            description: "Chooses between two expressions of the same type.",
            result: A,
            parameters: new[]
            {
                Param("condition", ExprType.Bool, "Condition to test"),
                Param("true", A, "Value when the condition holds"),
                Param("false", A, "Value otherwise"),
            },
            build: (args, result) => Construct(
                typeof(ConditionalExpr<>), ExprTypes.ToClr(result), args[0]!, args[1]!, args[2]!));

        // map(values, expression) : (a[], b) -> b[]
        // `expression` is evaluated once per element with the element bound as variable 0.
        Register(
            names: new[] { "map", "select" },
            description: "Transforms every element of an array.",
            result: new ExprType.Arr(B),
            parameters: new[]
            {
                Param("values", new ExprType.Arr(A), "Array to transform"),
                Param("expression", ExprType.Func(B, A), "Result computed from each element"),
            },
            build: (args, result) => Construct(
                typeof(MapArrayExpr<>), ElementOf(result), args[0]!, args[1]!));

        // filter(values, condition) : (a[], bool) -> a[]
        Register(
            names: new[] { "filter", "where" },
            description: "Keeps the elements for which a condition holds.",
            result: new ExprType.Arr(A),
            parameters: new[]
            {
                Param("values", new ExprType.Arr(A), "Array to filter"),
                Param("condition", ExprType.Func(ExprType.Bool, A), "Test applied to each element"),
            },
            build: (args, result) => Construct(
                typeof(FilterArrayExpr<>), ElementOf(result), args[0]!, args[1]!));

        // concat(arrays) : (a[][]) -> a[]
        // Shares its names with string concatenation; the two are told apart by the argument name
        // ("arrays" vs "strings") and by the type the surrounding context expects.
        Register(
            names: new[] { "concat", "append", "join", "array_concat", "concat_arrays" },
            description: "Concatenates several arrays into one.",
            result: new ExprType.Arr(A),
            parameters: new[]
            {
                Param("arrays", new ExprType.Arr(new ExprType.Arr(A)), "Arrays to join end to end"),
            },
            build: (args, result) => ConcatArrays(ElementOf(result), args[0]!));

        // subarray(array, start?, length?) : (a[], number, number) -> a[]
        Register(
            names: new[] { "subarray", "slice" },
            description: "A contiguous run of elements.",
            result: new ExprType.Arr(A),
            parameters: new[]
            {
                Param("array", new ExprType.Arr(A), "Array to slice"),
                Param("start", ExprType.Number, "First index; defaults to 0", required: false),
                Param("length", ExprType.Number, "Number of elements; defaults to the rest", required: false),
            },
            build: (args, result) => Construct(
                typeof(SubArrayExpr<>), ElementOf(result),
                args[0]!,
                args[1] ?? new ConstNumberExpr(0),
                args[2] ?? new ConstNumberExpr(float.MaxValue)));

        // order(values, comparison) : (a[], bool) -> a[]
        Register(
            names: new[] { "order", "sort", "order_by" },
            description: "Sorts an array by a pairwise comparison.",
            result: new ExprType.Arr(A),
            parameters: new[]
            {
                Param("values", new ExprType.Arr(A), "Array to sort"),
                Param("comparison", ExprType.Func(ExprType.Bool, A, A), "True when the first element sorts before the second"),
            },
            build: (args, result) => Construct(
                typeof(OrderArrayExpr<>), ElementOf(result), args[0]!, args[1]!));

        RegisterRanked("max_elements", "max", "top", descending: true,
            "The highest-scoring elements of an array.");
        RegisterRanked("min_elements", "min", "bottom", descending: false,
            "The lowest-scoring elements of an array.");

        // with_vars([values], expression) : (any[], a) -> a
        Register(
            names: new[] { "with_vars", "with_var", "with_variables" },
            description: "Binds variable slots for an inner expression.",
            result: A,
            parameters: new[]
            {
                Param("variables", new ExprType.Arr(ExprType.Unknown), "Values bound as variables 0, 1, …"),
                Param("expression", A, "Expression evaluated with those bindings"),
            },
            build: (args, result) => Construct(
                typeof(WithVariablesExpr<>), ExprTypes.ToClr(result), args[0]!, args[1]!));

        // call_expr(id, args?) : (string, any[]) -> a
        // The library expression's own result type is unknown here, so the call is wrapped in a
        // cast to whatever the surrounding context expects.
        Register(
            names: new[] { "function", "run_function", "call_function", "run_expr", "call_expr" },
            description: "Calls a named expression from a library.",
            result: A,
            parameters: new[]
            {
                Param("id", ExprType.String, "Expression name, optionally 'library:name'"),
                Param("args", new ExprType.Arr(ExprType.Unknown), "Arguments bound as variables 0, 1, …", required: false),
                Param("library", ExprType.String, "Library id, when not part of 'id'", required: false),
            },
            build: (args, result) => LibraryCall(args, result));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static OpParam Param(string name, ExprType type, string description, bool required = true)
        => new(name, type, required, description);

    private static void Register(
        string[] names,
        string description,
        ExprType result,
        OpParam[] parameters,
        Func<BaseExpr?[], ExprType, BaseExpr> build)
    {
        OpRegistry.Register(new OpDef
        {
            Name = names[0],
            Aliases = names.ToImmutableArray(),
            Params = parameters.ToImmutableArray(),
            Result = result,
            Description = description,
            DeclaringMethod = "Rpg.Scripting.Types.MetaOps",
            Factory = build
        });
    }

    private static void RegisterRanked(string primary, string alias1, string alias2, bool descending, string description)
    {
        Register(
            names: new[] { primary, alias1, alias2 },
            description: description,
            result: new ExprType.Arr(A),
            parameters: new[]
            {
                Param("values", new ExprType.Arr(A), "Array to rank"),
                Param("value", ExprType.Func(ExprType.Number, A), "Score computed from each element"),
                Param("count", ExprType.Number, "How many to keep; defaults to all", required: false),
            },
            build: (args, result) => Construct(
                typeof(RankedElementsArrayExpr<>), ElementOf(result),
                args[0]!, args[1]!, args[2] ?? new ConstNumberExpr(float.MaxValue), descending));
    }

    /// <summary>Instantiates an open generic expression type over <paramref name="typeArgument"/>.</summary>
    private static BaseExpr Construct(Type openGeneric, Type typeArgument, params object?[] constructorArguments)
        => ExprFactory.New(openGeneric, typeArgument, constructorArguments);

    /// <summary>The CLR element type of a resolved array type.</summary>
    private static Type ElementOf(ExprType result)
        => result is ExprType.Arr array
            ? ExprTypes.ElementClr(array.Element)
            : throw new OpResolutionException($"expected an array result but inference produced {result}");

    /// <summary>
    /// <c>ArrayAppendExpr&lt;T&gt;</c> takes its parts as a params array of <c>ArrayExpr&lt;T&gt;</c>,
    /// so the outer array literal is unpacked here rather than evaluated as a nested sequence.
    /// </summary>
    private static BaseExpr ConcatArrays(Type element, BaseExpr arrays)
    {
        var parts = Unwrap(arrays);

        var pieces = parts is IConstArrayExpr literal
            ? literal.Items.Select(part => Coerce(part, element)).ToArray()
            : new[] { Coerce(parts, element) };

        return ExprFactory.NewFromArray(typeof(ArrayAppendExpr<>), element,
            ExprFactory.TypedArray(ExprFactory.Close(typeof(ArrayExpr<>), element), pieces));
    }

    /// <summary>Adapts a sequence expression to <c>ArrayExpr&lt;element&gt;</c>, which is invariant.</summary>
    private static object Coerce(BaseExpr expr, Type element)
    {
        var arrayExprType = ExprFactory.Close(typeof(ArrayExpr<>), element);
        if (arrayExprType.IsInstanceOfType(expr)) return expr;

        var inner = Unwrap(expr);
        if (arrayExprType.IsInstanceOfType(inner)) return inner;

        return ExprFactory.New(typeof(CastArrayExpr<>), element, expr);
    }

    /// <summary>Looks through the cast an <c>any</c>-typed position introduces.</summary>
    private static BaseExpr Unwrap(BaseExpr expr) => expr is ICastExpr cast ? cast.CastSource : expr;

    /// <summary>
    /// Builds a call into an expression library. The name may be written <c>"library:name"</c> or
    /// split across the <c>id</c> and <c>library</c> arguments.
    /// </summary>
    private static BaseExpr LibraryCall(BaseExpr?[] args, ExprType result)
    {
        var id = Literals.String(args[0]!);
        string libraryId;
        string exprName;

        if (id.Contains(':'))
        {
            var parts = id.Split(':', 2);
            libraryId = parts[0];
            exprName = parts[1];
        }
        else
        {
            libraryId = args[2] is { } library
                ? Literals.String(library)
                : throw new OpResolutionException(
                    $"'{id}' does not name a library; write 'library:{id}' or pass a 'library' argument");
            exprName = id;
        }

        var call = new RunExprFromLibraryExpr(libraryId, exprName, (ArrayExpr<object>?)args[1]);
        return Construct(typeof(CastExpr<>), ExprTypes.ToClr(result), call);
    }
}

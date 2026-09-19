using System.Collections.Immutable;

namespace Rpg.Scripting.Types;

/// <summary>One declared parameter of an op.</summary>
/// <param name="Name">What this parameter is called in signatures, diagnostics and schemas.</param>
/// <param name="Type">Expected type; may contain type variables for polymorphic ops.</param>
/// <param name="Required">False when the op declares a default for this parameter.</param>
public sealed record OpParam(
    string Name,
    ExprType Type,
    bool Required,
    string? Description = null);

/// <summary>
/// A single registered operation: its name(s), full signature, and the factory that builds the
/// expression once arguments have been compiled and type-checked.
/// <para>
/// The factory receives arguments already compiled to the declared parameter types, in declaration
/// order, with <c>null</c> for omitted optional parameters. Ops therefore never touch
/// <c>JsonElement</c> and never repeat their own parameter names — the single declaration here is
/// what argument binding, the schema and the type checker all read.
/// </para>
/// </summary>
public sealed class OpDef
{
    public required string Name { get; init; }
    public required ImmutableArray<string> Aliases { get; init; }
    public required ImmutableArray<OpParam> Params { get; init; }
    public required ExprType Result { get; init; }

    /// <summary>
    /// Builds the expression from arguments already compiled to the declared parameter types, in
    /// declaration order, with <c>null</c> for omitted optional parameters.
    /// <para>
    /// The second argument is the op's result type <em>after</em> inference. Monomorphic ops ignore
    /// it; polymorphic ones need it, since <c>if : (bool, a, a) -&gt; a</c> can only construct a
    /// <c>ConditionalExpr&lt;T&gt;</c> once <c>a</c> is known.
    /// </para>
    /// </summary>
    public required Func<BaseExpr?[], ExprType, BaseExpr> Factory { get; init; }
    public string? Description { get; init; }

    /// <summary>Where this op was declared, for diagnostics.</summary>
    public string? DeclaringMethod { get; init; }

    public int RequiredCount => Params.Count(p => p.Required);

    public string Signature =>
        $"{Name}({string.Join(", ", Params.Select(p => $"{p.Name}: {p.Type}{(p.Required ? "" : "?")}"))}) -> {Result}";

    public override string ToString() => Signature;
}

/// <summary>
/// The one table of every operation the language knows.
/// <para>
/// This replaces six generated dictionaries (<c>_NumberOps</c>, <c>_NumberArrayOps</c>,
/// <c>_ConditionOps</c>, …) plus their twelve accessor methods. Array-ness is part of an op's
/// signature now, not a parallel table, and lookup is by name only — which category an op belongs
/// to is a consequence of its result type rather than a registration-time decision.
/// </para>
/// </summary>
public static class OpRegistry
{
    private static readonly Dictionary<string, List<OpDef>> _byName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<OpDef> _all = new();
    private static readonly object _gate = new();

    public static IReadOnlyList<OpDef> All
    {
        get { lock (_gate) return _all.ToArray(); }
    }

    public static void Register(OpDef def)
    {
        lock (_gate)
        {
            _all.Add(def);
            foreach (var alias in def.Aliases)
            {
                if (!_byName.TryGetValue(alias, out var list))
                    _byName[alias] = list = new List<OpDef>();
                list.Add(def);
            }
        }
    }

    /// <summary>All ops registered under <paramref name="name"/>, in registration order.</summary>
    public static IReadOnlyList<OpDef> Lookup(string name)
    {
        lock (_gate)
            return _byName.TryGetValue(name, out var list) ? list.ToArray() : Array.Empty<OpDef>();
    }

    /// <summary>
    /// Names close enough to <paramref name="name"/> to be worth suggesting when lookup fails.
    /// </summary>
    public static IEnumerable<string> Similar(string name, int max = 3)
    {
        lock (_gate)
        {
            return _byName.Keys
                .Select(k => (Key: k, Distance: Levenshtein(k.ToLowerInvariant(), name.ToLowerInvariant())))
                .Where(x => x.Distance <= Math.Max(2, name.Length / 3))
                .OrderBy(x => x.Distance)
                .Take(max)
                .Select(x => x.Key)
                .ToArray();
        }
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}

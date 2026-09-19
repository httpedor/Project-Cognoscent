using System.Collections.Immutable;

namespace Rpg.Scripting.Types;

/// <summary>
/// Resolves the type variables in a polymorphic op signature against the actual argument types at
/// a call site.
/// <para>
/// This is deliberately the smallest thing that works: there is no let-polymorphism, no recursive
/// types and no inference beyond matching arguments against declared parameters. That is enough to
/// give <c>if : (bool, a, a) -&gt; a</c>, <c>map : (a[], a -&gt; b) -&gt; b[]</c> and friends real
/// signatures, which is what lets them be registered as ordinary ops instead of living in a
/// hand-maintained switch in the compiler.
/// </para>
/// </summary>
public sealed class Substitution
{
    private readonly Dictionary<int, ExprType> _bindings = new();

    public IReadOnlyDictionary<int, ExprType> Bindings => _bindings;

    /// <summary>Replaces every bound type variable in <paramref name="type"/> with its binding.</summary>
    public ExprType Apply(ExprType type) => type switch
    {
        ExprType.Var v => _bindings.TryGetValue(v.Id, out var bound)
            // Chase through variables bound to other variables.
            ? (bound is ExprType.Var ? Apply(bound) : bound)
            : type,
        ExprType.Arr a => new ExprType.Arr(Apply(a.Element)),
        ExprType.Fn f => new ExprType.Fn(f.Params.Select(Apply).ToImmutableArray(), Apply(f.Result)),
        _ => type
    };

    /// <summary>
    /// Attempts to make <paramref name="pattern"/> (which may contain type variables) match
    /// <paramref name="actual"/> (which may not), recording any variable bindings. Returns false on
    /// a genuine mismatch, leaving the substitution usable only if the caller discards it.
    /// </summary>
    public bool Unify(ExprType pattern, ExprType actual)
    {
        pattern = Apply(pattern);
        actual = Apply(actual);

        if (pattern is ExprType.Var v)
        {
            if (actual is ExprType.Var w && w.Id == v.Id) return true;

            // `any` is the absence of a constraint, not a type to commit to. Binding a variable to
            // it would pin the whole signature prematurely: `map : (a[], b) -> b[]` used where an
            // `any[]` is acceptable would fix b = any before the body is compiled, and then build a
            // MapArrayExpr<object> around a body that actually produces a float.
            if (actual is ExprType.Any) return true;

            if (OccursIn(v.Id, actual)) return false;
            _bindings[v.Id] = actual;
            return true;
        }

        if (actual is ExprType.Var)
            return Unify(actual, pattern);

        switch (pattern, actual)
        {
            case (ExprType.Arr a, ExprType.Arr b):
                return Unify(a.Element, b.Element);

            case (ExprType.Fn f, ExprType.Fn g):
                if (f.Params.Length != g.Params.Length) return false;
                for (var i = 0; i < f.Params.Length; i++)
                    if (!Unify(f.Params[i], g.Params[i])) return false;
                return Unify(f.Result, g.Result);

            default:
                // Concrete against concrete: fall back to the coercion relation so a BodyPart
                // argument satisfies a Component parameter without needing an explicit cast.
                return ExprTypes.CanCoerce(actual, pattern);
        }
    }

    /// <summary>Guards against building an infinite type such as <c>a = a[]</c>.</summary>
    private static bool OccursIn(int id, ExprType type) => type switch
    {
        ExprType.Var v => v.Id == id,
        ExprType.Arr a => OccursIn(id, a.Element),
        ExprType.Fn f => f.Params.Any(p => OccursIn(id, p)) || OccursIn(id, f.Result),
        _ => false
    };
}

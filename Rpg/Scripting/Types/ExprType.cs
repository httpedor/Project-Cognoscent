using System.Collections.Immutable;
using Rpg.Entities;

namespace Rpg.Scripting.Types;

/// <summary>
/// The static type of an expression.
/// <para>
/// This replaces the old triple of <c>ExprCategory</c> (a closed enum of six "kinds"), an
/// <c>IsArray</c> boolean flag, and a <c>SubType</c> string. Those three could not describe an
/// array of arrays, a function, or a polymorphic op, which is why arrays and meta-ops such as
/// <c>map</c>/<c>if</c> had to be special-cased in the compiler instead of being registered like
/// any other op. Here "array of T" is a type constructor and functions are first class, so
/// <c>map : (Arr a, a -&gt; b) -&gt; Arr b</c> is expressible and needs no special case.
/// </para>
/// </summary>
public abstract record ExprType
{
    /// <summary>Number, Bool, String or Void — the types with no further structure.</summary>
    public sealed record Prim(PrimKind Kind) : ExprType
    {
        public override string ToString() => Kind.ToString().ToLowerInvariant();
    }

    /// <summary>A CLR-backed type: an entity, a component, an enum, a tag, a compendium entry.</summary>
    public sealed record Ref(Type Clr) : ExprType
    {
        public override string ToString() => Clr.Name;
    }

    /// <summary>A sequence of <paramref name="Element"/>. Nests freely, unlike the old bool flag.</summary>
    public sealed record Arr(ExprType Element) : ExprType
    {
        public override string ToString() => $"{Element}[]";
    }

    /// <summary>A function — what a lambda passed to <c>map</c>/<c>filter</c>/<c>order</c> has.</summary>
    public sealed record Fn(ImmutableArray<ExprType> Params, ExprType Result) : ExprType
    {
        public override string ToString() => $"({string.Join(", ", Params)}) -> {Result}";

        public bool Equals(Fn? other) =>
            other is not null && Result == other.Result && Params.SequenceEqual(other.Params);

        public override int GetHashCode()
        {
            var hash = Result.GetHashCode();
            foreach (var p in Params) hash = HashCode.Combine(hash, p);
            return hash;
        }
    }

    /// <summary>
    /// A type variable, used to give polymorphic ops a signature. Resolved by
    /// <see cref="Unifier"/> against the actual argument types at each call site.
    /// </summary>
    public sealed record Var(int Id) : ExprType
    {
        public override string ToString() => $"'{(char)('a' + Id % 26)}{(Id >= 26 ? (Id / 26).ToString() : "")}";
    }

    /// <summary>
    /// The unconstrained type. Corresponds to <c>object</c>: accepted by and convertible to
    /// anything, at the cost of a runtime cast. Used for heterogeneous variable arrays.
    /// </summary>
    public sealed record Any : ExprType
    {
        public override string ToString() => "any";
    }

    // ── shorthands ──────────────────────────────────────────────────────────
    public static readonly ExprType Number = new Prim(PrimKind.Number);
    public static readonly ExprType Bool = new Prim(PrimKind.Bool);
    public static readonly ExprType String = new Prim(PrimKind.String);
    /// <summary>The result type of an effect: it is executed for its side effects and yields nothing.</summary>
    public static readonly ExprType Void = new Prim(PrimKind.Void);
    public static readonly ExprType Unknown = new Any();

    public static ExprType Of(Type clr) => new Ref(clr);
    public static ExprType ArrayOf(ExprType element) => new Arr(element);
    public static ExprType Func(ExprType result, params ExprType[] parameters)
        => new Fn(parameters.ToImmutableArray(), result);
}

public enum PrimKind
{
    Number,
    Bool,
    String,
    Void
}

/// <summary>
/// Maps CLR types (as written in op declarations, e.g. <c>Expr&lt;float&gt;</c>,
/// <c>ArrayExpr&lt;BodyPart&gt;</c>) onto <see cref="ExprType"/>, and decides when a value of one
/// type is acceptable where another is expected.
/// </summary>
public static class ExprTypes
{
    /// <summary>
    /// Translates the CLR type a declared op parameter or return value uses into an
    /// <see cref="ExprType"/>. Understands <c>Expr&lt;T&gt;</c>, <c>ArrayExpr&lt;T&gt;</c>,
    /// <c>EffectExpr</c>, and bare value types.
    /// </summary>
    public static ExprType FromClr(Type type)
    {
        // Unwrap nullability: `Expr<Entity?>` and `Expr<Entity>` are the same expression type.
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        if (type == typeof(EffectExpr)) return ExprType.Void;

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arg = type.GetGenericArguments()[0];

            if (definition == typeof(ArrayExpr<>))
                return ExprType.ArrayOf(FromClr(arg));
            if (definition == typeof(Expr<>))
                return FromClr(arg);
            if (definition == typeof(IEnumerable<>) || definition == typeof(List<>) || definition == typeof(IReadOnlyList<>))
                return ExprType.ArrayOf(FromClr(arg));
        }

        if (type.IsArray && type.GetElementType() is { } element)
            return ExprType.ArrayOf(FromClr(element));

        if (type == typeof(object)) return ExprType.Unknown;
        if (type == typeof(string)) return ExprType.String;
        if (type == typeof(bool)) return ExprType.Bool;
        if (type == typeof(void) || type == typeof(NoReturn)) return ExprType.Void;
        if (IsNumeric(type)) return ExprType.Number;

        return ExprType.Of(type);
    }

    /// <summary>
    /// The CLR type that carries values of <paramref name="type"/> — the inverse of
    /// <see cref="FromClr"/>, used to close the compiler's generic entry points over a resolved type.
    /// </summary>
    public static Type ToClr(ExprType type) => type switch
    {
        ExprType.Prim { Kind: PrimKind.Number } => typeof(float),
        ExprType.Prim { Kind: PrimKind.Bool } => typeof(bool),
        ExprType.Prim { Kind: PrimKind.String } => typeof(string),
        ExprType.Prim { Kind: PrimKind.Void } => typeof(NoReturn),
        ExprType.Ref r => r.Clr,
        ExprType.Arr a => ExprFactory.Close(typeof(IEnumerable<>), ElementClr(a.Element)),
        _ => typeof(object)
    };

    /// <summary>
    /// The CLR type carried by the <em>elements</em> of an array of <paramref name="element"/>.
    /// <para>
    /// This differs from <see cref="ToClr"/> in exactly one case, and that case is why it exists:
    /// a scalar of type <c>void</c> is an effect that has already run, carried as
    /// <see cref="NoReturn"/> — but an <em>array</em> of void is a list of effects still to be run,
    /// carried as <see cref="EffectExpr"/>. Collapsing the two made <c>composite([…])</c> build a
    /// list that evaluated its effects while producing them.
    /// </para>
    /// </summary>
    public static Type ElementClr(ExprType element)
        => element is ExprType.Prim { Kind: PrimKind.Void } ? typeof(EffectExpr) : ToClr(element);

    /// <summary>
    /// The static type an already-compiled expression produces, read off its <c>Expr&lt;T&gt;</c> /
    /// <c>ArrayExpr&lt;T&gt;</c> base type.
    /// </summary>
    public static ExprType TypeOf(BaseExpr expr)
    {
        if (expr is EffectExpr) return ExprType.Void;

        // A lambda's type is a function type. Its parameter types are not carried on the node, so
        // they are reported as `any` and get pinned by unification against the op's signature.
        //
        // The result is read by asking the body what it produces, rather than reaching for the
        // first type argument of the body's immediate base class: that base is only Expr<T> for
        // expressions that derive from it directly, so a lambda over an effect (whose body derives
        // from EffectExpr) indexed an empty array.
        if (expr is ILambdaExpr lambda)
            return ExprType.Func(
                TypeOf(lambda.LambdaBody),
                Enumerable.Repeat(ExprType.Unknown, lambda.Arity).ToArray());

        for (var type = expr.GetType(); type != null; type = type.BaseType)
        {
            if (!type.IsGenericType) continue;
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ArrayExpr<>))
                return ExprType.ArrayOf(FromClr(type.GetGenericArguments()[0]));
            if (definition == typeof(Expr<>))
                return FromClr(type.GetGenericArguments()[0]);
        }
        return ExprType.Unknown;
    }

    private static bool IsNumeric(Type type) =>
        type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
        type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) ||
        type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong);

    /// <summary>
    /// True if a value of type <paramref name="from"/> may be used where <paramref name="to"/> is
    /// expected. This is the one place the language's implicit conversions are defined; previously
    /// they were scattered across <c>CastExpr.CastOp</c>, <c>CompileComponentAs</c>'s try/catch and
    /// the builder-retry loops.
    /// </summary>
    public static bool CanCoerce(ExprType from, ExprType to)
    {
        if (from == to) return true;
        // `any` is the opt-out: it costs a checked cast at runtime but is always allowed.
        if (from is ExprType.Any || to is ExprType.Any) return true;

        return (from, to) switch
        {
            // Arrays are covariant: an array of BodyPart is usable as an array of Component.
            (ExprType.Arr a, ExprType.Arr b) => CanCoerce(a.Element, b.Element),

            // Functions: contravariant in parameters, covariant in the result.
            (ExprType.Fn f, ExprType.Fn g) =>
                f.Params.Length == g.Params.Length
                && CanCoerce(f.Result, g.Result)
                && f.Params.Zip(g.Params).All(p => CanCoerce(p.Second, p.First)),

            (ExprType.Ref a, ExprType.Ref b) => CanCoerceRef(a.Clr, b.Clr),

            // A string names an enum member or a compendium entry. This is the one direction that
            // is meaningful — a component used where a string is wanted is a real type error — and
            // it is what lets a dynamically built id ("= concat([...])") land in a typed position.
            (ExprType.Prim { Kind: PrimKind.String }, ExprType.Ref r) => IsNamed(r.Clr),

            _ => false
        };
    }

    /// <summary>True for types an expression may name with a string: enums and compendium folders.</summary>
    public static bool IsNamed(Type clr) => clr.IsEnum || Compendium.IsFolder(clr);

    private static bool CanCoerceRef(Type from, Type to)
    {
        if (to.IsAssignableFrom(from)) return true;

        // An entity carries components, and a component knows its entity. Both directions are
        // resolved at runtime by CastExpr; statically we allow them so `bp_health(target)` works
        // where target is an Entity and the op wants a BodyPart.
        if (typeof(Entity).IsAssignableFrom(from) && typeof(Component).IsAssignableFrom(to)) return true;
        if (typeof(Component).IsAssignableFrom(from) && typeof(Entity).IsAssignableFrom(to)) return true;

        // A general component used where a specific one is wanted, e.g. an op returning
        // Expr<Component?> passed to one taking Expr<Body?>. Which component an expression yields
        // is frequently only known at evaluation time, so this is a checked downcast rather than a
        // static guarantee — CastExpr throws if the value turns out to be the wrong component.
        if (typeof(Component).IsAssignableFrom(from) && typeof(Component).IsAssignableFrom(to)) return true;

        return false;
    }

    /// <summary>
    /// Adapts a compiled expression to the CLR shape a parameter of type <paramref name="target"/>
    /// requires, inserting a checked cast when the types are compatible but not identical.
    /// <para>
    /// The coercion relation says an <c>Expr&lt;Component?&gt;</c> may be used where an
    /// <c>Expr&lt;Body?&gt;</c> is wanted; this is what makes that true at runtime. Without it the
    /// op's factory would attempt a plain C# cast between two unrelated closed generics and fail.
    /// </para>
    /// </summary>
    public static BaseExpr Adapt(BaseExpr expr, ExprType target)
    {
        // A function value is built to shape by whoever compiled it.
        if (target is ExprType.Fn) return expr;

        // A string standing for an enum member or a compendium entry is resolved by name at
        // evaluation time, so the conversion is a real node rather than a cast.
        if (target is ExprType.Ref named && IsNamed(named.Clr) && expr is Expr<string> text)
            return ExprFactory.New(
                named.Clr.IsEnum ? typeof(EnumExpr<>) : typeof(CompendiumEntryExpr<>), named.Clr, text);

        var isArray = target is ExprType.Arr;
        var element = target is ExprType.Arr array ? ElementClr(array.Element) : ToClr(target);
        var wanted = ExprFactory.Close(isArray ? typeof(ArrayExpr<>) : typeof(Expr<>), element);

        if (wanted.IsInstanceOfType(expr)) return expr;

        // Re-adapting an expression that was already cast for some other position would stack a
        // second conversion on top of the first, and every layer costs a checked cast per
        // evaluation. Retarget the original instead.
        if (expr is ICastExpr cast && wanted.IsInstanceOfType(cast.CastSource))
            return cast.CastSource;

        return ExprFactory.New(isArray ? typeof(CastArrayExpr<>) : typeof(CastExpr<>), element, expr);
    }

    /// <summary>
    /// How good a match <paramref name="from"/> is for <paramref name="to"/>, used to pick between
    /// overloads. Exact beats subtype beats <c>any</c>, so an op declared for BodyPart wins over one
    /// declared for Component when the argument really is a BodyPart.
    /// </summary>
    public static int MatchScore(ExprType from, ExprType to)
    {
        if (from == to) return 3;
        if (from is ExprType.Any || to is ExprType.Any) return 1;
        if (from is ExprType.Ref a && to is ExprType.Ref b && b.Clr.IsAssignableFrom(a.Clr)) return 2;
        if (from is ExprType.Arr x && to is ExprType.Arr y) return MatchScore(x.Element, y.Element);
        return CanCoerce(from, to) ? 1 : 0;
    }
}

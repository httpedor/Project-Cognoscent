using System.Collections.Immutable;

namespace Rpg.Scripting.Dsl;

/// <summary>
/// A position in the source an expression came from. Carried by every AST node so that an error
/// found during <em>compilation</em> — an unknown op, an argument of the wrong type, a failed
/// overload — can point at the text that produced it, the same way a syntax error does.
/// <para>
/// <see cref="None"/> is used for nodes built from a JSON literal, which has no position within a
/// DSL source string; those nodes report against the enclosing field instead.
/// </para>
/// </summary>
internal readonly record struct Span(int Line, int Column)
{
    public static readonly Span None = new(0, 0);
    public bool IsKnown => Line > 0;
}

/// <summary>
/// The parsed shape of an expression, and the only program representation the compiler lowers.
/// <para>
/// Both front ends produce these nodes: DSL source is lexed and parsed into them, and a JSON literal
/// is converted directly into the corresponding literal node. There is no second interpretation
/// path — <see cref="AstCompiler"/> is the single place a node becomes an expression.
/// </para>
/// </summary>
internal abstract record AstNode(Span Span);

/// <summary>
/// A subtree that was parsed from its own DSL source string, together with that text.
/// <para>
/// Spans are relative to the string they were lexed from, so a JSON structure holding several
/// independent DSL fields has no single text to quote. This node re-roots error formatting: a
/// failure anywhere beneath it is rendered against <see cref="Text"/>, whichever field it came from.
/// </para>
/// </summary>
internal sealed record SourceRoot(AstNode Body, string Text) : AstNode(Body.Span);

/// <summary>A numeric literal.</summary>
internal sealed record NumberLit(double Value, Span Span) : AstNode(Span);

/// <summary>A quoted string. May also name an enum value or compendium entry, depending on context.</summary>
internal sealed record StringLit(string Value, Span Span) : AstNode(Span);

/// <summary><c>true</c> / <c>false</c>.</summary>
internal sealed record BoolLit(bool Value, Span Span) : AstNode(Span);

/// <summary>
/// JSON <c>null</c>. Has no DSL spelling; what it means depends on the position it appears in —
/// no effect, no entity, false.
/// </summary>
internal sealed record NullLit(Span Span) : AstNode(Span);

/// <summary>Dice notation such as <c>1d6</c>, distinct from a string that merely looks like it.</summary>
internal sealed record DiceLit(string Notation, Span Span) : AstNode(Span);

/// <summary>A variable slot: <c>$0</c>, or a name bound by an enclosing lambda.</summary>
internal sealed record VarRef(int Index, Span Span) : AstNode(Span);

/// <summary>A context symbol: <c>target</c>, <c>caller</c>, <c>target_part</c>.</summary>
internal sealed record ContextRef(string Name, Span Span) : AstNode(Span);

/// <summary>An array literal.</summary>
internal sealed record ArrayLit(ImmutableArray<AstNode> Items, Span Span) : AstNode(Span);

/// <summary>
/// A call to an op, with positional arguments. Which parameter each argument binds to is decided by
/// overload resolution, not here.
/// </summary>
internal sealed record Call(string Name, ImmutableArray<AstNode> Arguments, Span Span) : AstNode(Span);

/// <summary>
/// A function body passed to <c>map</c>, <c>filter</c>, <c>order</c> and friends. Its parameters are
/// bound by the combinator that invokes it; inside the body they are the innermost variable slots.
/// </summary>
internal sealed record Lambda(AstNode Body, Span Span) : AstNode(Span);

/// <summary>
/// One arm of a <see cref="Switch"/>. <see cref="Max"/> equal to <see cref="Min"/> is the exact-match
/// form (<c>1 =&gt; …</c>); a wider span is the range form (<c>1..5 =&gt; …</c>), matched inclusively.
/// </summary>
internal readonly record struct SwitchCase(float Min, float Max, AstNode Body)
{
    public bool IsRange => Max > Min;
}

/// <summary>
/// <c>switch value { 1 =&gt; a, 2..5 =&gt; b, _ =&gt; c }</c>. A syntactic form rather than a call,
/// because a case table is not an argument list.
/// <para>
/// All-exact cases lower to a dictionary lookup (<c>SwitchNumberExpr</c>); any range case present
/// makes the whole switch a scan of inclusive bounds (<c>SwitchRangeExpr</c>).
/// </para>
/// </summary>
internal sealed record Switch(
    AstNode Value,
    ImmutableArray<SwitchCase> Cases,
    AstNode? Default,
    Span Span) : AstNode(Span);

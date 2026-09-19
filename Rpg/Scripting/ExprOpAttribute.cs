using System;

namespace Rpg.Scripting;

/// <summary>
/// Marks a static method as the factory for one or more op names.
/// <para>
/// The method's parameters <em>are</em> the op's declaration. Everything the rest of the system
/// needs — parameter names, their expression types, which are optional, the result type, the
/// argument binding and the schema — is derived from that one signature by <c>OpTableGenerator</c>.
/// Nothing is restated in attributes.
/// </para>
/// <example>
/// <code>
/// [ExprOp("stat", "creature_stat", "entity_stat", Description = "Reads a named stat.")]
/// public static Expr&lt;float&gt; Op(
///     [Doc("Name of the stat to read")] string stat,
///     [Doc("Entity to read from; defaults to the caller")] Expr&lt;Entity?&gt;? entity = null,
///     [Doc("Value when the stat is absent")] Expr&lt;float&gt;? @default = null)
///     =&gt; new StatExpr(stat, entity ?? new CallerEntityExpr(), @default ?? new ConstNumberExpr(0));
/// </code>
/// </example>
/// <para>
/// Parameter kinds: <c>Expr&lt;T&gt;</c>/<c>ArrayExpr&lt;T&gt;</c>/<c>EffectExpr</c> receive a
/// compiled expression; a bare <c>string</c>/number/<c>bool</c>/enum is a compile-time literal;
/// <c>VariableRef</c> is a variable slot. A default value makes a parameter optional and it arrives
/// as <c>null</c> when omitted.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ExprOpAttribute : Attribute
{
    /// <summary>One or more op name aliases, matched case-insensitively.</summary>
    public string[] OpNames { get; }

    /// <summary>Human-readable summary, surfaced in schemas and editor tooling.</summary>
    public string? Description { get; set; }

    public ExprOpAttribute(params string[] opNames) => OpNames = opNames;
}

/// <summary>
/// Documents an op parameter. Optional — it supplies only the description used by schemas and
/// tooling; the name, type and optionality all come from the parameter itself.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class DocAttribute : Attribute
{
    public string Text { get; }
    public DocAttribute(string text) => Text = text;
}

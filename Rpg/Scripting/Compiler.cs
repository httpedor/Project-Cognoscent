using System.Text.Json;
using Rpg.Scripting.Dsl;
using Rpg.Scripting.Types;

namespace Rpg.Scripting;

/// <summary>
/// Compiles the expression fields of a data file.
/// <para>
/// This is a façade over one pipeline, not a compiler in its own right. A field's JSON is turned
/// into an <see cref="AstNode"/> by <see cref="JsonSource"/> — a <c>"= …"</c> string is parsed as
/// DSL, anything else is a literal — and lowered by <see cref="AstCompiler"/> against the type the
/// field expects. The methods here differ only in what type that is.
/// </para>
/// <para>
/// There used to be a second front end here: an object-shaped encoding of the language
/// (<c>{"op": "sum", "numbers": […]}</c>) with its own dispatch, its own literal rules per category,
/// and its own copies of the meta-ops. Keeping the two in agreement was most of this file. The DSL
/// is now the only way to write an expression, so what remains is the mapping from a C# type to the
/// <see cref="ExprType"/> the compiler should aim at.
/// </para>
/// </summary>
public static class ExpressionCompiler
{
    static ExpressionCompiler()
    {
        // Populate the signature-declared op table before anything is compiled.
        GeneratedOps.RegisterAll();
        MetaOps.RegisterAll();
    }

    /// <summary>Compiles a field expected to produce <typeparamref name="T"/>.</summary>
    public static Expr<T> Compile<T>(JsonElement element)
        => As<T>(CompileAs(element, ExprTypes.FromClr(typeof(T))));

    /// <summary>Compiles a field expected to produce a sequence of <typeparamref name="T"/>.</summary>
    public static ArrayExpr<T> CompileArray<T>(JsonElement element)
    {
        var compiled = CompileAs(element, ExprType.ArrayOf(ExprTypes.FromClr(typeof(T))));
        return compiled as ArrayExpr<T> ?? new CastArrayExpr<T>(compiled);
    }

    /// <summary>Compiles a field expected to run for its side effects.</summary>
    public static EffectExpr CompileEffect(JsonElement element)
    {
        var compiled = CompileAs(element, ExprType.Void);

        // `call_expr` and a variable slot both produce a value whose type is only known at runtime;
        // in effect position that value is something to run, which is what this wrapper does.
        return compiled as EffectExpr ?? new CallExprEffect(compiled);
    }

    /// <summary>
    /// Compiles a field with no expected type — an expression library entry, whose result type is
    /// whatever the call site that invokes it needs.
    /// </summary>
    public static BaseExpr CompileBaseExpr(JsonElement element) => CompileAs(element, expected: null);

    /// <summary>Compiles <paramref name="element"/> if present, otherwise returns <paramref name="fallback"/>.</summary>
    public static Expr<T> CompileOr<T>(JsonElement? element, Expr<T> fallback)
        => element.HasValue ? Compile<T>(element.Value) : fallback;

    /// <summary>
    /// The one entry point: JSON in, expression out, aimed at <paramref name="expected"/>.
    /// </summary>
    private static BaseExpr CompileAs(JsonElement element, ExprType? expected)
    {
        var compiled = AstCompiler.CompileNode(JsonSource.Parse(element), expected);
        return expected == null ? compiled : ExprTypes.Adapt(compiled, expected);
    }

    /// <summary>
    /// Bridges to the exact <c>Expr&lt;T&gt;</c> a caller asked for. <see cref="ExprTypes.Adapt"/>
    /// works in terms of the language's types, where every number is a <c>float</c>; a caller asking
    /// for <c>Expr&lt;int&gt;</c> or <c>Expr&lt;uint&gt;</c> wants a specific CLR carrier and gets a
    /// converting cast for it.
    /// </summary>
    private static Expr<T> As<T>(BaseExpr expr) => expr as Expr<T> ?? new CastExpr<T>(expr);
}

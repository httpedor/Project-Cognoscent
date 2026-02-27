using System;

namespace Rpg.Scripting;

/// <summary>
/// Specifies the expression category for <see cref="ExprOpAttribute"/>.
/// </summary>
public enum ExprCategory
{
    Number,
    Condition,
    Effect,
    Selector,
    String
}

/// <summary>
/// Marks a static method as a JSON-to-Expr compiler for one or more op names.
/// The source generator collects these to build the dispatch registry and JSON schema.
/// <para>
/// The method must be <c>static</c>, accept a single <see cref="System.Text.Json.JsonElement"/> parameter,
/// and return a <see cref="BaseExpr"/>-derived type.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [ExprOp(ExprCategory.Number, "sum", "plus", "add", "addition", "+")]
/// [ExprParam("numbers", "numberExpr[]", Required = true)]
/// public static Expr&lt;float&gt; CompileOp(JsonElement obj)
///     => new AddExpr(ExpressionCompiler.CompileArgsAs&lt;Expr&lt;float&gt;&gt;(obj.GetProperty("numbers")));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ExprOpAttribute : Attribute
{
    /// <summary>
    /// The expression category this op belongs to (Number, Condition, Effect, Selector, String).
    /// </summary>
    public ExprCategory Category { get; }

    /// <summary>
    /// One or more op name aliases that map to this compile method.
    /// All names are matched case-insensitively at compile time.
    /// </summary>
    public string[] OpNames { get; }

    public ExprOpAttribute(ExprCategory category, params string[] opNames)
    {
        Category = category;
        OpNames = opNames;
    }
}

/// <summary>
/// Describes a JSON parameter accepted by an expression op, used for JSON schema generation.
/// Place on the same method as <see cref="ExprOpAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class ExprParamAttribute : Attribute
{
    /// <summary>
    /// The JSON property name (e.g. "numbers", "stat", "target").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The schema reference type. Use one of:
    /// "numberExpr", "conditionExpr", "effectExpr", "selectorExpr", "stringExpr",
    /// "string" (literal), "number" (literal), "boolean" (literal),
    /// or append "[]" for arrays (e.g. "numberExpr[]", "effectExpr[]").
    /// </summary>
    public string SchemaRef { get; }

    /// <summary>
    /// Whether this parameter is required in the JSON object.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Optional description for the JSON schema.
    /// </summary>
    public string? Description { get; set; }

    public ExprParamAttribute(string name, string schemaRef)
    {
        Name = name;
        SchemaRef = schemaRef;
    }
}

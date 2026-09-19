namespace Rpg.Scripting.Types;

/// <summary>
/// A reference to a variable slot, written either as an index (<c>0</c>, <c>$0</c>) or as a
/// context symbol (<c>target</c>, <c>caller</c>, <c>target_part</c>).
/// <para>
/// Declaring an op parameter as <see cref="VariableRef"/> states that it names a slot rather than
/// producing a value, which is what lets the compiler accept both spellings without the op having
/// to tell them apart itself.
/// </para>
/// </summary>
public readonly record struct VariableRef(int Index)
{
    public static implicit operator int(VariableRef reference) => reference.Index;
}

/// <summary>Implemented by expressions that are a direct reference to a variable slot.</summary>
public interface IVariableReference
{
    int ReferencedVariable { get; }
}

/// <summary>
/// Extracts compile-time constants from compiled argument expressions.
/// <para>
/// Some op parameters are genuinely static — a stat name, a group name, a log level — and the
/// expression that holds them is always a literal. Declaring such a parameter as a bare
/// <c>string</c>/<c>float</c>/enum rather than an <c>Expr&lt;T&gt;</c> says so in the signature, and
/// these helpers unwrap the literal (failing with a clear message when the author passed something
/// dynamic where the op cannot accept it).
/// </para>
/// </summary>
public static class Literals
{
    /// <summary>Resolves a variable reference written as an index, a <c>$N</c> form, or a symbol name.</summary>
    public static VariableRef VariableRef(BaseExpr expr)
    {
        // Unwrap the cast that an `any`-typed position introduces.
        while (expr is ICastExpr cast) expr = cast.CastSource;

        return expr switch
        {
            IVariableReference reference => new VariableRef(reference.ReferencedVariable),
            ConstNumberExpr number => new VariableRef((int)number.Value),
            StringLiteralExpr text => new VariableRef(VariableSlots.Resolve(text.Value)),
            _ => throw new OpResolutionException(
                $"expected a variable reference (an index, $N, or a context name) but got {expr.GetType().Name}")
        };
    }

    public static VariableRef? VariableRefOrNull(BaseExpr? expr) => expr == null ? null : VariableRef(expr);

    public static string String(BaseExpr expr) =>
        expr is StringLiteralExpr literal
            ? literal.Value
            : throw new OpResolutionException(
                $"expected a constant string here, but got {expr.GetType().Name}");

    public static string? StringOrNull(BaseExpr? expr) => expr == null ? null : String(expr);

    public static float Number(BaseExpr expr) =>
        expr is ConstNumberExpr constant
            ? constant.Value
            : throw new OpResolutionException(
                $"expected a constant number here, but got {expr.GetType().Name}");

    public static float? NumberOrNull(BaseExpr? expr) => expr == null ? null : Number(expr);

    public static bool Bool(BaseExpr expr) =>
        expr is ConstConditionExpr constant
            ? constant.Value
            : throw new OpResolutionException(
                $"expected a constant true/false here, but got {expr.GetType().Name}");

    public static bool? BoolOrNull(BaseExpr? expr) => expr == null ? null : Bool(expr);

    public static T Enum<T>(BaseExpr expr) where T : struct, System.Enum
    {
        var text = String(expr);
        if (System.Enum.TryParse<T>(text, ignoreCase: true, out var value))
            return value;
        throw new OpResolutionException(
            $"'{text}' is not a valid {typeof(T).Name}; expected one of {string.Join(", ", System.Enum.GetNames(typeof(T)))}");
    }

    public static T? EnumOrNull<T>(BaseExpr? expr) where T : struct, System.Enum =>
        expr == null ? null : Enum<T>(expr);
}

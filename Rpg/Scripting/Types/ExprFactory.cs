using System.Collections.Concurrent;

namespace Rpg.Scripting.Types;

/// <summary>
/// Constructs expression nodes whose type argument is only known at compile time — a
/// <c>ConstArrayExpr&lt;BodyPart&gt;</c> built while lowering an array of body parts, say.
/// <para>
/// Every such construction used to spell out <c>Activator.CreateInstance(open.MakeGenericType(t), …)</c>
/// at its call site, re-resolving the closed type on every node. Closing a generic is not free and
/// the same handful of types are closed over the same handful of arguments across a whole data load,
/// so the results are cached here and the call sites read as what they are: "make one of these".
/// </para>
/// </summary>
internal static class ExprFactory
{
    private static readonly ConcurrentDictionary<(Type Open, Type Argument), Type> _closed = new();

    /// <summary>Closes <paramref name="openGeneric"/> over one type argument, memoised.</summary>
    public static Type Close(Type openGeneric, Type argument) =>
        _closed.GetOrAdd((openGeneric, argument),
            key => key.Open.MakeGenericType(key.Argument));

    /// <summary>Instantiates <c>Open&lt;argument&gt;</c> with the given constructor arguments.</summary>
    public static BaseExpr New(Type openGeneric, Type argument, params object?[] constructorArguments) =>
        (BaseExpr)Activator.CreateInstance(Close(openGeneric, argument), constructorArguments)!;

    /// <summary>
    /// Instantiates <c>Open&lt;argument&gt;</c> whose constructor takes a single array. Kept apart
    /// from <see cref="New"/> because an array passed to a <c>params object?[]</c> would be spread
    /// into separate arguments rather than handed over whole.
    /// </summary>
    public static BaseExpr NewFromArray(Type openGeneric, Type argument, Array elements) =>
        (BaseExpr)Activator.CreateInstance(Close(openGeneric, argument), new object[] { elements })!;

    /// <summary>Builds a strongly-typed <c>T[]</c> from items already known to be of that type.</summary>
    public static Array TypedArray(Type elementType, IReadOnlyList<object> items)
    {
        var array = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++) array.SetValue(items[i], i);
        return array;
    }
}

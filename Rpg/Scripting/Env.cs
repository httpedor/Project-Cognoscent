namespace Rpg.Scripting;

/// <summary>
/// A chain of variable frames, innermost first.
/// <para>
/// Replaces the flat <c>object[]</c> that every combinator used to reshuffle by hand. Binding a
/// value no longer means allocating a new array, copying the old contents up by one, and writing
/// slot 0 — each of which was reimplemented slightly differently in <c>map</c>, <c>filter</c>,
/// <c>order</c>, <c>with_vars</c>, <c>foreach</c>, <c>all</c> and <c>any</c>. Pushing a frame is
/// now the single operation, and frames are immutable, so a lazily-enumerated combinator can never
/// observe a later iteration's binding.
/// </para>
/// <para>
/// Index 0 is the innermost frame's first slot; indices continue outward through parent frames and
/// finally into the context's host-owned root slots. That is exactly the numbering the
/// <c>$N</c> syntax already had — pushing one binding shifts everything else up by one — so
/// existing data keeps its meaning.
/// </para>
/// </summary>
public sealed class Env
{
    public static readonly Env Empty = new(Array.Empty<object?>(), null);

    private readonly object?[] _slots;
    private readonly Env? _parent;

    private Env(object?[] slots, Env? parent)
    {
        _slots = slots;
        _parent = parent;
    }

    /// <summary>Total number of slots visible through this chain, excluding the host root.</summary>
    public int Depth => _slots.Length + (_parent?.Depth ?? 0);

    /// <summary>Binds <paramref name="values"/> as the innermost frame.</summary>
    public Env Push(params object?[] values) =>
        values.Length == 0 ? this : new Env(values, this);

    /// <summary>
    /// Reads slot <paramref name="index"/>, or reports how many slots were skipped if the chain is
    /// shorter than the index — letting the caller continue into the host root slots.
    /// </summary>
    public bool TryGet(int index, out object? value)
    {
        for (var env = this; env is not null; env = env._parent)
        {
            if (index < env._slots.Length)
            {
                value = env._slots[index];
                return true;
            }
            index -= env._slots.Length;
        }
        value = null;
        return false;
    }

    /// <summary>Slots remaining after this chain is exhausted, for indexing the host root.</summary>
    public int Remaining(int index) => index - Depth;
}

/// <summary>Lets a lambda be inspected without knowing its result type.</summary>
public interface ILambdaExpr
{
    int Arity { get; }
    BaseExpr LambdaBody { get; }
}

/// <summary>
/// A function value: the body of a <c>map</c>, <c>filter</c>, <c>order</c> or similar, together
/// with the parameters it expects.
/// <para>
/// Previously these bodies were plain expressions and the binding of their parameters was a
/// convention — "the current element is variable 0, everything else shifts up" — that each
/// combinator re-implemented and that no signature could describe. Now <c>map</c> genuinely has the
/// type <c>(a[], (a) -&gt; b) -&gt; b[]</c>, and <see cref="Invoke"/> is the only place a parameter
/// is ever bound.
/// </para>
/// </summary>
public sealed class LambdaExpr<T> : Expr<T>, ILambdaExpr
{
    public readonly Expr<T> Body;
    /// <summary>How many arguments <see cref="Invoke"/> binds; parameter i is <c>$i</c>.</summary>
    public readonly int Arity;

    int ILambdaExpr.Arity => Arity;
    BaseExpr ILambdaExpr.LambdaBody => Body;

    public LambdaExpr(Expr<T> body, int arity)
    {
        Body = body;
        Arity = arity;
    }

    public LambdaExpr(Stream stream)
    {
        Body = BaseExpr.Deserialize<Expr<T>>(stream);
        Arity = stream.ReadInt32();
    }

    /// <summary>Evaluates the body with <paramref name="arguments"/> bound as the innermost frame.</summary>
    public T Invoke(EvalContext ctx, params object?[] arguments) => Body.Eval(ctx.Push(arguments));

    /// <summary>Evaluating a lambda without arguments just evaluates its body in the current scope.</summary>
    public override T Eval(EvalContext ctx) => Body.Eval(ctx);

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        Body.ToBytes(stream);
        stream.WriteInt32(Arity);
    }
}

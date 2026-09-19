using Rpg.Entities;
using Rpg.Scripting.Types;

namespace Rpg.Scripting;

/// <summary>
/// Everything an expression can read while it runs: the variable slots in scope, and the entities
/// the script was invoked against.
/// </summary>
public sealed class EvalContext
{
    /// <summary>
    /// The host-owned root variable slots. A host seeds these before evaluating (the body writes
    /// injury severity into slot 0, for instance) and may overwrite them between evaluations.
    /// Frames pushed by combinators layer on top and never alias them.
    /// </summary>
    public object[] Variables = Array.Empty<object>();

    /// <summary>
    /// Frames bound by combinators — the current element inside a <c>map</c>, the values bound by
    /// <c>with_vars</c>, a library call's arguments. Immutable, so a lazily-enumerated combinator
    /// cannot observe a later iteration's binding.
    /// </summary>
    public Env Frames = Env.Empty;

    /// <summary>The board on which the script is being executed, if any.</summary>
    public Board? Board = null;

    /// <summary>The entity executing the script, if any.</summary>
    public Entity? Caller = null;

    /// <summary>The target entity of the script, if any.</summary>
    public Entity? Target = null;

    /// <summary>The exact component of the target entity being affected, if any.</summary>
    public Component? TargetComponent = null;

    public EvalContext()
    {
        Variables = Array.Empty<object>();
    }

    public EvalContext(params object[] args)
    {
        Variables = args;
    }

    public object? GetVariable(int index)
    {
        switch (index)
        {
            case VariableSlots.Target: return Target;
            case VariableSlots.Caller: return Caller;
            case VariableSlots.TargetComponent: return TargetComponent;
        }

        if (index < 0)
            throw new IndexOutOfRangeException($"Variable index {index} is out of range.");

        // Pushed frames shadow the root slots, innermost first.
        if (Frames.TryGet(index, out var bound))
            return bound;

        var rootIndex = Frames.Remaining(index);
        if (rootIndex >= Variables.Length)
            throw new IndexOutOfRangeException($"Variable index {index} is out of range.");
        return Variables[rootIndex];
    }

    public object? GetVariable(string symbolName) => GetVariable(VariableSlots.Resolve(symbolName));

    public T Eval<T>(Expr<T> expr) => expr.Eval(this);

    public EvalContext WithTarget(Entity? entity, Entity? caller = null, Component? targetComponent = null)
        => new()
        {
            Variables = this.Variables,
            Frames = this.Frames,
            Board = this.Board,
            Caller = caller ?? this.Caller,
            Target = entity,
            TargetComponent = targetComponent ?? this.TargetComponent
        };

    /// <summary>
    /// Binds <paramref name="values"/> as a new innermost frame, shifting existing bindings up.
    /// This is the one scoping primitive: every combinator that binds an element, an argument or a
    /// <c>with_vars</c> value goes through it.
    /// </summary>
    public EvalContext Push(params object?[] values) => new()
    {
        Variables = this.Variables,
        Frames = this.Frames.Push(values),
        Board = this.Board,
        Caller = this.Caller,
        Target = this.Target,
        TargetComponent = this.TargetComponent
    };

    /// <summary>
    /// Starts a fresh variable scope, discarding the caller's bindings — what invoking a library
    /// expression does, since its parameters are its own and it cannot see the call site's
    /// <c>$N</c> slots.
    /// </summary>
    public EvalContext WithVariables(params object[] variables) => new()
    {
        Variables = variables,
        Frames = Env.Empty,
        Board = this.Board,
        Caller = this.Caller,
        Target = this.Target,
        TargetComponent = this.TargetComponent
    };
}

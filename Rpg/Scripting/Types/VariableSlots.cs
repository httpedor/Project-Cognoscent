namespace Rpg.Scripting.Types;

/// <summary>
/// The naming of variable slots, shared by everything that has to turn a written slot reference into
/// an index.
/// <para>
/// Non-negative indices are ordinary bindings (a lambda parameter, a <c>with_vars</c> value, a
/// library call's argument). The negative ones are the evaluation context's own fields, which are
/// addressable by name so an op can be handed "the target" the same way it is handed <c>$0</c>.
/// </para>
/// </summary>
public static class VariableSlots
{
    public const int Target = -1;
    public const int Caller = -2;
    public const int TargetComponent = -3;

    /// <summary>
    /// Resolves a slot written as an index (<c>0</c>, <c>$0</c>) or as a context name
    /// (<c>target</c>, <c>caller</c>, <c>target_part</c>).
    /// </summary>
    public static int Resolve(string name)
    {
        var symbol = name.StartsWith('$') ? name[1..] : name;

        if (int.TryParse(symbol, out var index))
            return index;

        return symbol.ToLowerInvariant() switch
        {
            "target" => Target,
            "caller" or "self" => Caller,
            "target_part" or "target_component" => TargetComponent,
            _ => throw new OpResolutionException(
                $"'{name}' does not name a variable slot (expected $N, or one of target, caller, target_part)")
        };
    }
}

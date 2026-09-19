using System.Collections.Immutable;
using System.Text;

namespace Rpg.Scripting.Types;

/// <summary>
/// Thrown when no registered op matches a call site. Unlike the previous behaviour — which tried
/// every builder registered under a name, swallowed each exception, and reported only
/// "Unknown expression operation: X" — this carries why every candidate was rejected.
/// </summary>
public class OpResolutionException : Exception
{
    public OpResolutionException(string message) : base(message) { }
}

/// <summary>
/// Marks an error that trying a different overload cannot fix — an unknown op or identifier, a
/// syntax problem. Resolution lets these through instead of recording them as one candidate's
/// rejection reason, so the report points at the thing that is actually wrong rather than at the
/// enclosing call.
/// </summary>
public interface IFatalCompileError
{
}

/// <summary>No op is registered under this name at all, so no overload could match.</summary>
public sealed class UnknownOperationException : OpResolutionException, IFatalCompileError
{
    public UnknownOperationException(string message) : base(message) { }
}

/// <summary>
/// Picks the right op for a call site and builds it.
/// <para>
/// Replaces the old dispatch, which was exception-driven backtracking over a name-keyed list of
/// builders (<c>try { builder(element) } catch { /* next */ }</c>). Selection is now by type:
/// candidates whose signature cannot accept the arguments are rejected with a reason, and the
/// best-matching remainder wins.
/// </para>
/// </summary>
public static class OpResolver
{
    private sealed record Candidate(OpDef Def, BaseExpr Built, int Score);

    /// <summary>
    /// Resolves <paramref name="opName"/> against <paramref name="args"/>, compiling each argument
    /// with <paramref name="compile"/> once its expected type is known.
    /// <para>
    /// Arguments are positional. They used to be bindable by name as well, because a JSON op-object
    /// supplied its arguments as properties; with the DSL as the only front end every call site is
    /// a positional argument list.
    /// </para>
    /// </summary>
    /// <param name="expected">
    /// The type demanded by the surrounding context, or null at a position that accepts anything.
    /// </param>
    public static BaseExpr Resolve<TNode>(
        string opName,
        IReadOnlyList<TNode> args,
        ExprType? expected,
        Func<TNode, ExprType?, BaseExpr> compile)
    {
        var candidates = OpRegistry.Lookup(opName);
        if (candidates.Count == 0)
        {
            var similar = OpRegistry.Similar(opName).ToList();
            var hint = similar.Count > 0 ? $" Did you mean {string.Join(", ", similar.Select(s => $"'{s}'"))}?" : "";
            throw new UnknownOperationException($"Unknown operation '{opName}'.{hint}");
        }

        var matches = new List<Candidate>();
        var rejections = new List<(OpDef Def, string Reason)>();

        foreach (var def in candidates)
        {
            if (TryBuild(def, args, expected, compile, out var built, out var score, out var reason))
                matches.Add(new Candidate(def, built!, score));
            else
                rejections.Add((def, reason!));
        }

        if (matches.Count > 0)
            return matches.MaxBy(m => m.Score)!.Built;

        var message = new StringBuilder();
        message.Append($"No overload of '{opName}' matches this call");
        if (expected != null) message.Append($" in a position expecting {expected}");
        message.AppendLine(".");
        foreach (var (def, reason) in rejections)
            message.AppendLine($"    {def.Signature}\n        {reason}");
        throw new OpResolutionException(message.ToString().TrimEnd());
    }

    private static bool TryBuild<TNode>(
        OpDef def,
        IReadOnlyList<TNode> args,
        ExprType? expected,
        Func<TNode, ExprType?, BaseExpr> compile,
        out BaseExpr? built,
        out int score,
        out string? reason)
    {
        built = null;
        score = 0;
        reason = null;

        var subst = new Substitution();

        if (expected != null && !subst.Unify(def.Result, expected))
        {
            reason = $"returns {def.Result}, which cannot be used as {expected}";
            return false;
        }

        if (args.Count > def.Params.Length)
        {
            reason = $"takes at most {def.Params.Length} argument(s) but {args.Count} were given";
            return false;
        }

        var arguments = new BaseExpr?[def.Params.Length];

        for (var i = 0; i < def.Params.Length; i++)
        {
            var param = def.Params[i];

            if (i >= args.Count)
            {
                if (param.Required)
                {
                    reason = $"is missing required parameter '{param.Name}'";
                    return false;
                }
                arguments[i] = null;
                continue;
            }

            // Apply what has been inferred so far, so `map`'s body is compiled knowing the element
            // type the collection argument just fixed. Type variables that are still open are erased
            // to `any` rather than discarding the whole expectation — for `concat : (a[][]) -> a[]`
            // the argument is known to be an array of arrays long before `a` is, and that structure
            // is what tells the compiler to build an array rather than a scalar.
            var hint = Erase(subst.Apply(param.Type));

            BaseExpr compiled;
            try
            {
                compiled = compile(args[i], hint);
            }
            catch (Exception e) when (e is not IFatalCompileError)
            {
                reason = $"could not compile argument '{param.Name}': {e.Message}";
                return false;
            }

            var actual = ExprTypes.TypeOf(compiled);
            if (!subst.Unify(param.Type, actual))
            {
                reason = $"argument '{param.Name}' is {actual}, expected {subst.Apply(param.Type)}";
                return false;
            }

            var resolved = subst.Apply(param.Type);
            score += ExprTypes.MatchScore(actual, resolved);

            // The argument's type is acceptable, but the factory casts to the parameter's exact CLR
            // type; bridge the two where they differ (a Component used as a Body, say).
            arguments[i] = ExprTypes.Adapt(compiled, resolved);
        }

        try
        {
            built = def.Factory(arguments, subst.Apply(def.Result));
        }
        catch (Exception e)
        {
            reason = $"failed to build: {e.Message}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Replaces any still-unresolved type variable with <c>any</c>, keeping the surrounding
    /// structure. The result is what a call site can usefully be told about an argument whose
    /// element type inference has not pinned down yet.
    /// </summary>
    private static ExprType Erase(ExprType type) => type switch
    {
        ExprType.Var => ExprType.Unknown,
        ExprType.Arr a => new ExprType.Arr(Erase(a.Element)),
        ExprType.Fn f => new ExprType.Fn(
            f.Params.Select(Erase).ToImmutableArray(), Erase(f.Result)),
        _ => type
    };
}

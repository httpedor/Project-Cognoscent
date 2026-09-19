using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>
/// Picks unique member names for generated code.
/// <para>
/// This replaces three copies of the same four-deep nested fallback ladder — one for entity
/// accessor properties, one for event-handler arrays, one for category arrays — each written out
/// longhand with slightly different suffixes.
/// </para>
/// </summary>
public static class Naming
{
    /// <summary>
    /// The first of <paramref name="preferred"/> that is neither reserved nor already taken,
    /// falling back to a numeric suffix. Claims the chosen name in <paramref name="taken"/>.
    /// </summary>
    public static string Unique(IEnumerable<string> preferred, ISet<string> taken, ISet<string>? reserved = null)
    {
        var candidates = preferred.ToList();
        foreach (var candidate in candidates)
        {
            if (reserved is not null && reserved.Contains(candidate)) continue;
            if (taken.Add(candidate)) return candidate;
        }

        var basis = candidates.Count > 0 ? candidates[candidates.Count - 1] : "Member";
        for (var suffix = 2; ; suffix++)
        {
            var candidate = basis + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if ((reserved is null || !reserved.Contains(candidate)) && taken.Add(candidate))
                return candidate;
        }
    }

    /// <summary>Drops a trailing word, e.g. <c>FeaturesContainerComponent</c> -> <c>FeaturesContainer</c>.</summary>
    public static string TrimSuffix(string name, string suffix) =>
        name.EndsWith(suffix, System.StringComparison.Ordinal) && name.Length > suffix.Length
            ? name.Substring(0, name.Length - suffix.Length)
            : name;

    /// <summary>Drops the interface <c>I</c> prefix, e.g. <c>ITickableComponent</c> -> <c>TickableComponent</c>.</summary>
    public static string TrimInterfacePrefix(string name) =>
        name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? name.Substring(1) : name;

    /// <summary>
    /// Names already declared on <paramref name="type"/>, so generated members never collide with
    /// hand-written ones. Previously this was a hard-coded list of sixteen <c>Entity</c> members
    /// that had to be maintained by hand and silently rotted when Entity gained a member.
    /// </summary>
    public static HashSet<string> DeclaredMembers(INamedTypeSymbol? type)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers())
                names.Add(member.Name);
        return names;
    }
}

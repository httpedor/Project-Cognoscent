using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>
/// Emits a lookup array of component ids for each abstract component class and component
/// interface — <c>Component.TickableIDs</c>, <c>Component.SkillProviderIDs</c> and so on — so
/// "every component that ticks" is an array walk rather than a type test per component.
/// </summary>
[Generator]
public sealed class ComponentCategoryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(ComponentModel.Build);

        context.RegisterSourceOutput(model, static (spc, model) =>
        {
            if (model.IsEmpty || model.Categories.IsDefaultOrEmpty) return;
            var source = Emit(model);
            if (source is not null)
                spc.AddSource("ComponentCategoryIds.g.cs", source);
        });
    }

    private static string? Emit(ComponentModel model)
    {
        var taken = new HashSet<string>(System.StringComparer.Ordinal);
        var arrays = new StringBuilder();
        var emitted = false;

        // Abstract classes first, then interfaces, each group ordered by name — the order the
        // arrays appear in has no meaning beyond keeping the generated file stable between builds.
        foreach (var category in model.Categories.OrderBy(c => c.TypeKind == TypeKind.Interface)
                                                  .ThenBy(c => c.Name, System.StringComparer.Ordinal))
        {
            var members = model.Components
                .Where(c => category.TypeKind == TypeKind.Interface
                    ? c.Symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, category))
                    : ComponentModel.InheritsFrom(c.Symbol, category))
                .ToList();

            if (members.Count == 0) continue;

            var stem = category.TypeKind == TypeKind.Interface
                ? Naming.TrimInterfacePrefix(category.Name)
                : category.Name;
            stem = Naming.TrimSuffix(stem, "Component");

            var arrayName = Naming.Unique(new[] { stem + "IDs" }, taken);

            arrays.AppendLine($"    public static readonly uint[] {arrayName} =");
            arrays.AppendLine("        new uint[]");
            arrays.AppendLine("        {");
            foreach (var member in members)
                arrays.AppendLine($"            {member.FullyQualifiedName}.ID,");
            arrays.AppendLine("        };");
            arrays.AppendLine();
            emitted = true;
        }

        if (!emitted) return null;

        return ComponentIdGenerator.Header() + $$"""
            namespace Rpg.Entities;

            public abstract partial class Component
            {
            {{arrays.ToString().TrimEnd()}}
            }

            """;
    }
}

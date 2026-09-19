using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>
/// Emits a typed property on <c>Entity</c> for each component, so <c>entity.Body</c> reads the
/// component array slot directly instead of going through a lookup.
/// </summary>
[Generator]
public sealed class EntityAccessorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(ComponentModel.Build);

        context.RegisterSourceOutput(model, static (spc, model) =>
        {
            if (model.IsEmpty) return;
            spc.AddSource("Entity.ComponentAccessors.g.cs", Emit(model));
        });
    }

    private static string Emit(ComponentModel model)
    {
        // Ask Entity what it already declares rather than maintaining a hand-written list of its
        // members that silently rots whenever Entity gains one.
        var reserved = Naming.DeclaredMembers(model.EntityType);
        var taken = new HashSet<string>(System.StringComparer.Ordinal);

        var properties = new StringBuilder();
        foreach (var component in model.Components)
        {
            var name = Naming.Unique(
                new[] { Naming.TrimSuffix(component.Symbol.Name, "Component"), component.Symbol.Name },
                taken,
                reserved);

            properties.AppendLine(
                $"    public {component.FullyQualifiedName}? {name} => ({component.FullyQualifiedName}?)componentArray[{component.FullyQualifiedName}.ID];");
        }

        return ComponentIdGenerator.Header() + $$"""
            namespace Rpg.Entities;

            public partial class Entity
            {
            {{properties.ToString().TrimEnd()}}
            }

            """;
    }
}

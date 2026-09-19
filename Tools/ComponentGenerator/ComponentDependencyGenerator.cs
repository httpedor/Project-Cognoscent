using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>
/// Emits the required/optional dependency tables for components that declare
/// <c>[RequiredComponent]</c> or <c>[OptionalComponent]</c> members.
/// <para>
/// Components with no dependencies get nothing: the base class already returns an empty array from
/// a virtual property, so emitting a partial with two empty arrays and two overrides for every
/// component was pure weight. The original code even said as much in a comment and then emitted
/// them anyway.
/// </para>
/// </summary>
[Generator]
public sealed class ComponentDependencyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(ComponentModel.Build);

        context.RegisterSourceOutput(model, static (spc, model) =>
        {
            if (model.IsEmpty || model.RequiredAttribute is null || model.OptionalAttribute is null) return;

            // Built once, not per component.
            var idByType = new Dictionary<INamedTypeSymbol, uint>(
                (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default);
            foreach (var component in model.Components)
                idByType[component.Symbol] = component.Id;

            var declarations = new List<(ComponentInfo Component, string Required, string Optional)>();
            foreach (var component in model.Components)
            {
                var (required, optional, diagnostics) = Collect(component, model, idByType);
                foreach (var diagnostic in diagnostics)
                    spc.ReportDiagnostic(diagnostic);

                if (required.Count == 0 && optional.Count == 0) continue;
                declarations.Add((component, Render(required), Render(optional)));
            }

            if (declarations.Count == 0) return;
            spc.AddSource("ComponentDependencies.g.cs", Emit(declarations));
        });
    }

    private static string Emit(List<(ComponentInfo Component, string Required, string Optional)> declarations)
    {
        var body = new StringBuilder();

        foreach (var group in declarations.GroupBy(d => d.Component.Namespace, System.StringComparer.Ordinal))
        {
            var scoped = !string.IsNullOrEmpty(group.Key);
            if (scoped) body.Append("namespace ").Append(group.Key).AppendLine().AppendLine("{");

            foreach (var (component, required, optional) in group)
                body.Append($$"""
                        public partial class {{component.Symbol.Name}}
                        {
                            private static readonly global::Rpg.Entities.Component.ComponentDependency[] __requiredComponentDependencies =
                    {{required}};

                            private static readonly global::Rpg.Entities.Component.ComponentDependency[] __optionalComponentDependencies =
                    {{optional}};

                            public override global::Rpg.Entities.Component.ComponentDependency[] RequiredComponentDependencies => __requiredComponentDependencies;
                            public override global::Rpg.Entities.Component.ComponentDependency[] OptionalComponentDependencies => __optionalComponentDependencies;
                        }

                    """);

            if (scoped) body.AppendLine("}").AppendLine();
        }

        return ComponentIdGenerator.Header() + body;
    }

    private static string Render(List<string> entries) =>
        entries.Count == 0
            ? "            global::System.Array.Empty<global::Rpg.Entities.Component.ComponentDependency>()"
            : "            new global::Rpg.Entities.Component.ComponentDependency[]\n            {\n"
              + string.Join("\n", entries.Select(e => e + ",")) + "\n            }";

    /// <summary>
    /// Reads a component's dependency members. Diagnostics come back as data rather than being
    /// reported from here, so this stays a pure function of the symbol.
    /// </summary>
    private static (List<string> Required, List<string> Optional, List<Diagnostic> Diagnostics) Collect(
        ComponentInfo component, ComponentModel model, Dictionary<INamedTypeSymbol, uint> idByType)
    {
        var required = new List<string>();
        var optional = new List<string>();
        var diagnostics = new List<Diagnostic>();

        foreach (var member in component.Symbol.GetMembers())
        {
            if (member.IsStatic) continue;

            var memberType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null
            };
            if (memberType is null) continue;

            foreach (var attribute in member.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null) continue;

                var isRequired = SymbolEqualityComparer.Default.Equals(attributeClass, model.RequiredAttribute);
                var isOptional = SymbolEqualityComparer.Default.Equals(attributeClass, model.OptionalAttribute);
                if (!isRequired && !isOptional) continue;

                if (attribute.ConstructorArguments.Length != 1 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol dependency)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(memberType, dependency) || !IsWritable(member))
                {
                    diagnostics.Add(Diagnostic.Create(
                        ComponentModel.InvalidDependencyMember,
                        member.Locations.FirstOrDefault(),
                        component.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        member.Name,
                        attributeClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                    continue;
                }

                // A component outside the registry has no constant id, so fall back to the runtime
                // lookup for it.
                var dependencyId = idByType.TryGetValue(dependency, out var id)
                    ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "u"
                    : $"global::Rpg.Entities.Component.GetComponentTypeId(typeof({dependency.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))";

                var componentName = component.FullyQualifiedName;
                var dependencyName = dependency.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                (isRequired ? required : optional).Add(
                    $"                new global::Rpg.Entities.Component.ComponentDependency({dependencyId}, "
                    + $"static (global::Rpg.Entities.Component self, global::Rpg.Entities.Component value) => "
                    + $"(({componentName})self).{member.Name} = ({dependencyName})value)");
            }
        }

        return (required, optional, diagnostics);
    }

    private static bool IsWritable(ISymbol member) => member switch
    {
        IFieldSymbol field => !field.IsReadOnly,
        IPropertySymbol property => property.SetMethod is not null && !property.SetMethod.IsInitOnly,
        _ => false
    };
}

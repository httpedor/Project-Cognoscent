using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>
/// Emits each component's <c>ID</c> constant, its <c>GetId()</c> override, and the two id/type
/// lookup dictionaries.
/// <para>
/// Ids are positions in <see cref="ComponentModel.Components"/>, which is also what indexes an
/// entity's component array.
/// </para>
/// </summary>
[Generator]
public sealed class ComponentIdGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(ComponentModel.Build);

        context.RegisterSourceOutput(model, static (spc, model) =>
        {
            foreach (var diagnostic in model.Diagnostics)
                spc.ReportDiagnostic(diagnostic);

            if (model.IsEmpty) return;

            spc.AddSource("ComponentIds.g.cs", EmitIdConstants(model));
            spc.AddSource("ComponentIdMap.g.cs", EmitIdMaps(model));

            var overrides = EmitGetIdOverrides(model);
            if (overrides is not null)
                spc.AddSource("Component.GetId.g.cs", overrides);
        });
    }

    private static string EmitIdConstants(ComponentModel model)
    {
        var body = new StringBuilder();

        // The file spans several namespaces, so it cannot use a file-scoped namespace.
        foreach (var group in model.Components.GroupBy(c => c.Namespace, System.StringComparer.Ordinal))
        {
            var scoped = !string.IsNullOrEmpty(group.Key);
            if (scoped) body.Append("namespace ").Append(group.Key).AppendLine().AppendLine("{");

            foreach (var component in group)
                body.Append($$"""
                        public partial class {{component.Symbol.Name}}
                        {
                            public const uint ID = {{component.Id}}u;
                        }

                    """);

            if (scoped) body.AppendLine("}").AppendLine();
        }

        return Header() + body;
    }

    private static string EmitIdMaps(ComponentModel model)
    {
        var byId = string.Join("\n", model.Components.Select(c =>
            $"            {{ {c.Id}u, typeof({c.FullyQualifiedName}) }},"));
        var byType = string.Join("\n", model.Components.Select(c =>
            $"            {{ typeof({c.FullyQualifiedName}), {c.Id}u }},"));

        return Header() + $$"""
            namespace Rpg.Entities;

            public abstract partial class Component
            {
                private static readonly global::System.Collections.Generic.Dictionary<uint, global::System.Type> _componentTypesById =
                    new global::System.Collections.Generic.Dictionary<uint, global::System.Type>
                    {
            {{byId}}
                    };

                private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, uint> _componentTypeIds =
                    new global::System.Collections.Generic.Dictionary<global::System.Type, uint>
                    {
            {{byType}}
                    };

                public static global::System.Collections.Generic.IReadOnlyDictionary<uint, global::System.Type> ComponentTypesById => _componentTypesById;
                public static global::System.Collections.Generic.IReadOnlyDictionary<global::System.Type, uint> ComponentTypeIds => _componentTypeIds;
            }

            """;
    }

    private static string? EmitGetIdOverrides(ComponentModel model)
    {
        var candidates = model.Components.Where(c => NeedsGetId(c.Symbol)).ToList();
        if (candidates.Count == 0) return null;

        var body = new StringBuilder();
        foreach (var group in candidates.GroupBy(c => c.Namespace, System.StringComparer.Ordinal))
        {
            var scoped = !string.IsNullOrEmpty(group.Key);
            if (scoped) body.Append("namespace ").Append(group.Key).AppendLine().AppendLine("{");

            foreach (var component in group)
                body.Append($$"""
                        public partial class {{component.Symbol.Name}}
                        {
                            public override uint GetId() => ID;
                        }

                    """);

            if (scoped) body.AppendLine("}").AppendLine();
        }

        return Header() + body;
    }

    /// <summary>
    /// False when the component, or any base, already provides a concrete <c>GetId()</c>.
    /// </summary>
    private static bool NeedsGetId(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var declared = current.GetMembers("GetId")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => !m.IsStatic
                                     && m.Parameters.Length == 0
                                     && m.ReturnType.SpecialType == SpecialType.System_UInt32);

            // The type itself declaring GetId means generating another would be a duplicate; a base
            // declaring a concrete one means the override is unnecessary.
            if (declared is not null)
                return SymbolEqualityComparer.Default.Equals(current, type) ? false : declared.IsAbstract;
        }
        return true;
    }

    internal static string Header() => """
        // <auto-generated />
        #nullable enable


        """;
}

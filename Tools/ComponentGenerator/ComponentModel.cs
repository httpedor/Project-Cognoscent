using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>One concrete component and the id it was assigned.</summary>
public sealed class ComponentInfo
{
    public ComponentInfo(INamedTypeSymbol symbol, uint id)
    {
        Symbol = symbol;
        Id = id;
    }

    public INamedTypeSymbol Symbol { get; }
    public uint Id { get; }

    public string FullyQualifiedName => Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    public string? Namespace => Symbol.ContainingNamespace.IsGlobalNamespace
        ? null
        : Symbol.ContainingNamespace.ToDisplayString();
}

/// <summary>
/// Everything the component generators need to know, computed once.
/// <para>
/// Id assignment in particular used to be derived independently in three places — once for the
/// <c>ID</c> constants, once for the abstract/interface lookup arrays, and once for the dependency
/// tables — three computations that had to agree and nothing checking that they did. There is now
/// one ordered list and every emitter reads its ids from it.
/// </para>
/// </summary>
public sealed class ComponentModel
{
    public ImmutableArray<ComponentInfo> Components { get; private init; } = ImmutableArray<ComponentInfo>.Empty;

    /// <summary>Abstract component classes and component interfaces that get a lookup array.</summary>
    public ImmutableArray<INamedTypeSymbol> Categories { get; private init; } = ImmutableArray<INamedTypeSymbol>.Empty;

    /// <summary>Concrete event classes, so each can be given a direct dispatch entry.</summary>
    public ImmutableArray<INamedTypeSymbol> AllEventTypes { get; private init; } = ImmutableArray<INamedTypeSymbol>.Empty;

    public INamedTypeSymbol? ComponentBase { get; private init; }
    public INamedTypeSymbol? EntityType { get; private init; }
    public INamedTypeSymbol? ComponentEvent { get; private init; }
    public INamedTypeSymbol? ComponentEventHandler { get; private init; }
    public INamedTypeSymbol? RequiredAttribute { get; private init; }
    public INamedTypeSymbol? OptionalAttribute { get; private init; }

    public ImmutableArray<Diagnostic> Diagnostics { get; private init; } = ImmutableArray<Diagnostic>.Empty;

    public bool IsEmpty => Components.IsDefaultOrEmpty;

    private const string ComponentMetadataName = "Rpg.Entities.Component";
    private const string EntityMetadataName = "Rpg.Entities.Entity";
    private const string ComponentEventMetadataName = "Rpg.Entities.ComponentEvent";
    private const string ComponentEventHandlerMetadataName = "Rpg.Entities.ComponentEventHandler`1";
    private const string RegisterComponentMetadataName = "Rpg.Entities.RegisterComponentAttribute";
    private const string RequiredComponentMetadataName = "Rpg.Entities.RequiredComponentAttribute";
    private const string OptionalComponentMetadataName = "Rpg.Entities.OptionalComponentAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        id: "COMP001",
        title: "Component must be partial",
        messageFormat: "Component '{0}' must be declared partial so its id and dependency tables can be generated",
        category: "ComponentGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidDependencyMember = new(
        id: "COMP002",
        title: "Invalid component dependency",
        messageFormat: "In '{0}', member '{1}' marked '{2}' must be writable and have the same type as the attribute",
        category: "ComponentGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingRegistration = new(
        id: "COMP003",
        title: "Component is missing [RegisterComponent]",
        messageFormat: "'{0}' derives from Component but is not marked [RegisterComponent], so it gets no id and cannot be stored on an entity",
        category: "ComponentGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Walks the compilation once, finds the registered component types, and assigns ids.
    /// </summary>
    public static ComponentModel Build(Compilation compilation, System.Threading.CancellationToken cancellationToken)
    {
        var componentBase = compilation.GetTypeByMetadataName(ComponentMetadataName);
        var registerAttribute = compilation.GetTypeByMetadataName(RegisterComponentMetadataName);
        if (componentBase is null || registerAttribute is null)
            return new ComponentModel();

        var componentEvent = compilation.GetTypeByMetadataName(ComponentEventMetadataName);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var concrete = new List<INamedTypeSymbol>();
        var categories = new List<INamedTypeSymbol>();
        var events = new List<INamedTypeSymbol>();

        foreach (var type in EnumerateTypes(compilation.Assembly.GlobalNamespace, cancellationToken))
        {
            if (componentEvent is not null
                && type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsGenericType
                && InheritsFrom(type, componentEvent))
                events.Add(type);

            var registered = type.GetAttributes().Any(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, registerAttribute));

            if (type.TypeKind == TypeKind.Interface)
            {
                if (registered) categories.Add(type);
                continue;
            }

            if (type.TypeKind != TypeKind.Class || !InheritsFrom(type, componentBase))
                continue;

            if (!registered)
            {
                // Only concrete components must register; an abstract helper base that never appears
                // on an entity is allowed to opt out silently.
                if (!type.IsAbstract && !type.IsGenericType)
                    diagnostics.Add(Diagnostic.Create(
                        MissingRegistration, type.Locations.FirstOrDefault(),
                        type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                continue;
            }

            if (type.IsAbstract || type.IsGenericType)
            {
                categories.Add(type);
                continue;
            }

            if (!IsPartial(type, cancellationToken))
            {
                diagnostics.Add(Diagnostic.Create(
                    MustBePartial, type.Locations.FirstOrDefault(),
                    type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                continue;
            }

            concrete.Add(type);
        }

        // Ids are positions in this ordering, so it must be total and stable. Ordering by simple
        // name is what the ids were originally assigned from and is kept; the fully-qualified name
        // breaks ties, which were previously resolved by hash-set enumeration order and so could
        // differ between builds.
        var ordered = concrete
            .OrderBy(t => t.Name, System.StringComparer.Ordinal)
            .ThenBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .Select((symbol, index) => new ComponentInfo(symbol, (uint)index))
            .ToImmutableArray();

        return new ComponentModel
        {
            Components = ordered,
            Categories = categories
                .OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
                .ToImmutableArray(),
            AllEventTypes = events
                .OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
                .ToImmutableArray(),
            ComponentBase = componentBase,
            EntityType = compilation.GetTypeByMetadataName(EntityMetadataName),
            ComponentEvent = componentEvent,
            ComponentEventHandler = compilation.GetTypeByMetadataName(ComponentEventHandlerMetadataName),
            RequiredAttribute = compilation.GetTypeByMetadataName(RequiredComponentMetadataName),
            OptionalAttribute = compilation.GetTypeByMetadataName(OptionalComponentMetadataName),
            Diagnostics = diagnostics.ToImmutable()
        };
    }

    /// <summary>
    /// Every named type in the assembly. Walking symbols avoids asking for a semantic model per
    /// candidate class — the old receiver collected every class with a base list in the compilation
    /// and then resolved each one, twice over.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(
        INamespaceOrTypeSymbol root, System.Threading.CancellationToken cancellationToken)
    {
        foreach (var member in root.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (member)
            {
                case INamespaceSymbol ns:
                    foreach (var nested in EnumerateTypes(ns, cancellationToken)) yield return nested;
                    break;
                case INamedTypeSymbol type:
                    yield return type;
                    foreach (var nested in EnumerateTypes(type, cancellationToken)) yield return nested;
                    break;
            }
        }
    }

    public static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        return false;
    }

    /// <summary>Reported once per type, not once per declaration site.</summary>
    private static bool IsPartial(INamedTypeSymbol type, System.Threading.CancellationToken cancellationToken)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(cancellationToken);
            if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax declaration
                && declaration.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                return true;
        }
        return false;
    }
}

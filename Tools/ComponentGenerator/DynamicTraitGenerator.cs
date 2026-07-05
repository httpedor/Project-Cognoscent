using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComponentGenerator;

[Generator]
public class DynamicTraitGenerator : ISourceGenerator
{
	private const string ComponentMetadataName = "Rpg.Entities.Component";
	private const string ComponentEventMetadataName = "Rpg.Entities.ComponentEvent";
	private const string ComponentEventHandlerMetadataName = "Rpg.Entities.ComponentEventHandler`1";
	private const string RequiredComponentAttributeMetadataName = "Rpg.Entities.RequiredComponentAttribute";
	private const string OptionalComponentAttributeMetadataName = "Rpg.Entities.OptionalComponentAttribute";
	private static readonly DiagnosticDescriptor MustBePartialDescriptor = new(
		id: "TG001",
		title: "Component must be partial",
		messageFormat: "Class '{0}' extends Component and must be declared as partial",
		category: "ComponentGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor InvalidDependencyMemberDescriptor = new(
		id: "TG002",
		title: "Invalid dependency",
		messageFormat: "In '{0}', member '{1}' with attribute '{2}' must be writable and have the same type as the attribute",
		category: "ComponentGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public void Initialize(GeneratorInitializationContext context)
	{
		context.RegisterForSyntaxNotifications(() => new Receiver());
	}

	public void Execute(GeneratorExecutionContext context)
	{
		if (context.SyntaxReceiver is not Receiver receiver)
		{
			return;
		}

		var componentSymbol = context.Compilation.GetTypeByMetadataName(ComponentMetadataName);
		if (componentSymbol is null)
		{
			return;
		}

		var requiredAttrSymbol = context.Compilation.GetTypeByMetadataName(RequiredComponentAttributeMetadataName);
		var optionalAttrSymbol = context.Compilation.GetTypeByMetadataName(OptionalComponentAttributeMetadataName);
		var componentEventSymbol = context.Compilation.GetTypeByMetadataName(ComponentEventMetadataName);
		var componentEventHandlerSymbol = context.Compilation.GetTypeByMetadataName(ComponentEventHandlerMetadataName);

		var derivedComponents = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
		foreach (var classDecl in receiver.CandidateClasses)
		{
			var model = context.Compilation.GetSemanticModel(classDecl.SyntaxTree);
			if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
			{
				continue;
			}

			if (SymbolEqualityComparer.Default.Equals(classSymbol, componentSymbol))
			{
				continue;
			}

			if (!InheritsFrom(classSymbol, componentSymbol))
			{
				continue;
			}

			derivedComponents.Add(classSymbol);
		}

		// (2) Emit an error if any class extending Component is not partial
		foreach (var type in derivedComponents)
		{
			foreach (var declRef in type.DeclaringSyntaxReferences)
			{
				if (declRef.GetSyntax(context.CancellationToken) is not ClassDeclarationSyntax decl || decl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
				{
					continue;
				}

				if (!decl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
				{
					context.ReportDiagnostic(Diagnostic.Create(
						MustBePartialDescriptor,
						decl.Identifier.GetLocation(),
						type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
				}
			}
		}

		// (1) "Folder" components: files under Rpg/Entities/Components/
		var folderComponents = derivedComponents
			.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsGenericType)
			.Where(IsInComponentsFolder)
			.OrderBy(t => t.Name, StringComparer.Ordinal)
			.ToImmutableArray();

		if (folderComponents.Length == 0)
		{
			return;
		}

		GenerateComponentIdConsts(context, folderComponents);
		GenerateComponentIdMap(context, folderComponents);
		GenerateComponentGetIdOverrides(context, folderComponents, componentSymbol);
		GenerateAbstractComponentIdArrays(context, derivedComponents, folderComponents);

		if (componentEventSymbol is not null && componentEventHandlerSymbol is not null)
		{
			GenerateComponentEventHandlerArrays(context, receiver.CandidateClasses, folderComponents, componentEventSymbol, componentEventHandlerSymbol);
		}

		if (requiredAttrSymbol is not null && optionalAttrSymbol is not null)
		{
			GenerateComponentDependencyArrays(context, derivedComponents, folderComponents, requiredAttrSymbol, optionalAttrSymbol);
		}

		GenerateEntityComponentAccessors(context, folderComponents);
	}

	private static void GenerateComponentGetIdOverrides(
		GeneratorExecutionContext context,
		ImmutableArray<INamedTypeSymbol> folderComponents,
		INamedTypeSymbol componentSymbol)
	{
		static bool NeedsGetId(INamedTypeSymbol type, INamedTypeSymbol componentBase)
		{
			// If the type already declares GetId(), skip generation to avoid a conflict.
			var hasDeclared = type.GetMembers()
				.OfType<IMethodSymbol>()
				.Any(m =>
					!m.IsStatic &&
					m.Name == "GetId" &&
					m.Parameters.Length == 0 &&
					m.ReturnType.SpecialType == SpecialType.System_UInt32);
			if (hasDeclared)
			{
				return false;
			}

			// If some base type already has a concrete implementation, skip generation too.
			for (var current = type.BaseType; current is not null; current = current.BaseType)
			{
				if (!InheritsFrom(type, componentBase) && !SymbolEqualityComparer.Default.Equals(type, componentBase))
				{
					break;
				}

				var baseGetId = current.GetMembers()
					.OfType<IMethodSymbol>()
					.FirstOrDefault(m =>
						!m.IsStatic &&
						m.Name == "GetId" &&
						m.Parameters.Length == 0 &&
						m.ReturnType.SpecialType == SpecialType.System_UInt32);
				if (baseGetId is not null && !baseGetId.IsAbstract)
				{
					return false;
				}
			}

			return true;
		}

		var candidates = folderComponents
			.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsGenericType)
			.Where(t => NeedsGetId(t, componentSymbol))
			.ToImmutableArray();

		if (candidates.Length == 0)
		{
			return;
		}

		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();

		// The generated file contains multiple namespaces.
		var groups = candidates
			.Select(sym => new
			{
				Symbol = sym,
				Namespace = sym.ContainingNamespace.IsGlobalNamespace ? null : sym.ContainingNamespace.ToDisplayString()
			})
			.GroupBy(x => x.Namespace, StringComparer.Ordinal);

		foreach (var group in groups)
		{
			if (!string.IsNullOrEmpty(group.Key))
			{
				sb.Append("namespace ").Append(group.Key).AppendLine();
				sb.AppendLine("{");
			}

			foreach (var item in group)
			{
				sb.Append("    public partial class ").Append(item.Symbol.Name).AppendLine();
				sb.AppendLine("    {");
				sb.AppendLine("        public override uint GetId() => ID;");
				sb.AppendLine("    }");
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(group.Key))
			{
				sb.AppendLine("}");
				sb.AppendLine();
			}
		}

		context.AddSource("Component.GetId.g.cs", sb.ToString());
	}

	private static void GenerateEntityComponentAccessors(
		GeneratorExecutionContext context,
		ImmutableArray<INamedTypeSymbol> folderComponents)
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();
		sb.AppendLine("namespace Rpg.Entities;");
		sb.AppendLine();
		sb.AppendLine("public partial class Entity");
		sb.AppendLine("{");

		var usedNames = new HashSet<string>(StringComparer.Ordinal);
		var reserved = new HashSet<string>(StringComparer.Ordinal)
		{
			"Id",
			"Name",
			"Board",
			"Components",
			"BBLink",
			"CreationTick",
			"ExistanceTicks",
			"Tick",
			"Initialize",
			"AddComponent",
			"RemoveComponent",
			"HasComponent",
			"GetComponent",
			"TryGetComponent",
			"DispatchEvent",
			"Destroy",
			"ToBytes",
		};

		foreach (var comp in folderComponents)
		{
			var typeName = comp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

			var propName = comp.Name;
			if (propName.EndsWith("Component", StringComparison.Ordinal) && propName.Length > "Component".Length)
			{
				propName = propName.Substring(0, propName.Length - "Component".Length);
			}

			if (reserved.Contains(propName) || !usedNames.Add(propName))
			{
				propName = comp.Name;
				if (reserved.Contains(propName) || !usedNames.Add(propName))
				{
					propName = comp.Name + "Component";
					if (!usedNames.Add(propName))
					{
						propName = comp.Name + "Component_" + usedNames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
						usedNames.Add(propName);
					}
				}
			}

			sb.Append("    public ");
			sb.Append(typeName);
			sb.Append("? ");
			sb.Append(propName);
			sb.Append(" => (");
			sb.Append(typeName);
			sb.Append("?)componentArray[");
			sb.Append(typeName);
			sb.AppendLine(".ID];");
		}

		sb.AppendLine("}");

		context.AddSource("Entity.ComponentAccessors.g.cs", sb.ToString());
	}

	private static void GenerateComponentEventHandlerArrays(
		GeneratorExecutionContext context,
		List<ClassDeclarationSyntax> candidateClasses,
		ImmutableArray<INamedTypeSymbol> folderComponents,
		INamedTypeSymbol componentEventSymbol,
		INamedTypeSymbol componentEventHandlerSymbol)
	{
		// (A) Discover concrete events (classes deriving from ComponentEvent)
		var eventTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
		foreach (var classDecl in candidateClasses)
		{
			var model = context.Compilation.GetSemanticModel(classDecl.SyntaxTree);
			if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol sym)
			{
				continue;
			}

			if (SymbolEqualityComparer.Default.Equals(sym, componentEventSymbol))
			{
				continue;
			}
			if (sym.TypeKind != TypeKind.Class || sym.IsAbstract || sym.IsGenericType)
			{
				continue;
			}
			if (!InheritsFrom(sym, componentEventSymbol))
			{
				continue;
			}

			eventTypes.Add(sym);
		}

		// (B) For each concrete component, discover which events it can handle via ComponentEventHandler<T>
		// We also add the handlers' T as known "eventTypes", so arrays/lookup are generated even when no
		// concrete ComponentEvent subclasses exist in the project yet.
		var handlerEventsByComponent = new Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
		foreach (var comp in folderComponents)
		{
			var handledSet = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
			foreach (var iface in comp.AllInterfaces)
			{
				if (iface is not INamedTypeSymbol namedIface)
				{
					continue;
				}
				if (!namedIface.IsGenericType || !SymbolEqualityComparer.Default.Equals(namedIface.OriginalDefinition, componentEventHandlerSymbol))
				{
					continue;
				}
				if (namedIface.TypeArguments.Length != 1)
				{
					continue;
				}
				if (namedIface.TypeArguments[0] is not INamedTypeSymbol ev)
				{
					continue;
				}
				if (!(SymbolEqualityComparer.Default.Equals(ev, componentEventSymbol) || InheritsFrom(ev, componentEventSymbol)))
				{
					continue;
				}
				handledSet.Add(ev);
			}

			var handled = handledSet
				.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
				.ToImmutableArray();

			handlerEventsByComponent[comp] = handled;

			foreach (var ev in handled)
			{
				eventTypes.Add(ev);
			}
		}

		var orderedEvents = eventTypes
			.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
			.ToImmutableArray();

		// (C) Emit arrays and lookup
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();
		sb.AppendLine("namespace Rpg.Entities;");
		sb.AppendLine();
		sb.AppendLine("public abstract partial class Component");
		sb.AppendLine("{");

		var usedArrayNames = new HashSet<string>(StringComparer.Ordinal);
		var emittedMappings = new List<(INamedTypeSymbol eventType, string arrayName)>();

		foreach (var ev in orderedEvents)
		{
			var matching = new List<INamedTypeSymbol>();
			foreach (var comp in folderComponents)
			{
				if (!handlerEventsByComponent.TryGetValue(comp, out var handled) || handled.Length == 0)
				{
					continue;
				}

				// Mapping is by the exact handler type (e.g. ComponentEventHandler<FooEvent>).
				// Derived events are served via the fallback in GetEventListenerComponentIds (walking up BaseType).
				var canHandle = handled.Any(h => SymbolEqualityComparer.Default.Equals(h, ev));
				if (canHandle)
				{
					matching.Add(comp);
				}
			}

			if (matching.Count == 0)
			{
				continue;
			}

			var name = ev.Name;
			if (name.EndsWith("Event", StringComparison.Ordinal) && name.Length > "Event".Length)
			{
				name = name.Substring(0, name.Length - "Event".Length);
			}
			var arrayName = name + "EventHandlerIDs";
			if (!usedArrayNames.Add(arrayName))
			{
				arrayName = name + "EventListenerIDs";
				if (!usedArrayNames.Add(arrayName))
				{
					arrayName = name + "EventHandlerIDs_" + usedArrayNames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
					usedArrayNames.Add(arrayName);
				}
			}

			sb.Append("    public static readonly uint[] ").Append(arrayName).AppendLine(" =");
			sb.AppendLine("        new uint[]");
			sb.AppendLine("        {");
			foreach (var comp in matching.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
			{
				var fq = comp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sb.Append("            ").Append(fq).AppendLine(".ID,");
			}
			sb.AppendLine("        };");
			sb.AppendLine();

			emittedMappings.Add((ev, arrayName));
		}

		sb.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, uint[]> __eventListenerComponentIdsByEventType =");
		sb.AppendLine("        new global::System.Collections.Generic.Dictionary<global::System.Type, uint[]>");
		sb.AppendLine("        {");
		foreach (var (eventType, arrayName) in emittedMappings)
		{
			var evFq = eventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			sb.Append("            { typeof(").Append(evFq).Append("), ").Append(arrayName).AppendLine(" },");
		}
		sb.AppendLine("        };");
		sb.AppendLine();
		sb.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Action<global::Rpg.Entities.Component, global::Rpg.Entities.ComponentEvent>> __eventDispatchersByEventType =");
		sb.AppendLine("        new global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Action<global::Rpg.Entities.Component, global::Rpg.Entities.ComponentEvent>>");
		sb.AppendLine("        {");
		foreach (var (eventType, _) in emittedMappings)
		{
			var evFq = eventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			sb.Append("            { typeof(").Append(evFq).Append("), static (c, e) => ((global::Rpg.Entities.ComponentEventHandler<").Append(evFq).Append(">)c).HandleEvent((").Append(evFq).Append(")e) },");
			sb.AppendLine();
		}
		sb.AppendLine("        };");
		sb.AppendLine();
		sb.AppendLine("    public static uint[] GetEventListenerComponentIds(global::System.Type eventType)");
		sb.AppendLine("    {");
		sb.AppendLine("        return GetEventListenerComponentIds(eventType, out _);");
		sb.AppendLine("    }");
		sb.AppendLine();
		sb.AppendLine("    public static uint[] GetEventListenerComponentIds(global::System.Type eventType, out global::System.Type? matchedEventType)");
		sb.AppendLine("    {");
		sb.AppendLine("        // Exact lookup + fallback to base types (allows handlers declared for base types). ");
		sb.AppendLine("        for (var t = eventType; t is not null && typeof(global::Rpg.Entities.ComponentEvent).IsAssignableFrom(t); t = t.BaseType)");
		sb.AppendLine("        {");
		sb.AppendLine("            if (__eventListenerComponentIdsByEventType.TryGetValue(t, out var ids))");
		sb.AppendLine("            {");
		sb.AppendLine("                matchedEventType = t;");
		sb.AppendLine("                return ids;");
		sb.AppendLine("            }");
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        matchedEventType = null;");
		sb.AppendLine("        return global::System.Array.Empty<uint>();");
		sb.AppendLine("    }");
		sb.AppendLine();
		sb.AppendLine("    public static uint[] GetEventListenerComponentIds<TEvent>() where TEvent : global::Rpg.Entities.ComponentEvent");
		sb.AppendLine("    {");
		sb.AppendLine("        return GetEventListenerComponentIds(typeof(TEvent));");
		sb.AppendLine("    }");
		sb.AppendLine();
		sb.AppendLine("    public static void DispatchEventToListener(global::Rpg.Entities.Component component, global::Rpg.Entities.ComponentEvent componentEvent, global::System.Type matchedEventType)");
		sb.AppendLine("    {");
		sb.AppendLine("        if (__eventDispatchersByEventType.TryGetValue(matchedEventType, out var dispatcher))");
		sb.AppendLine("        {");
		sb.AppendLine("            dispatcher(component, componentEvent);");
		sb.AppendLine("        }");
		sb.AppendLine("    }");

		sb.AppendLine("}");

		context.AddSource("ComponentEventHandlerIds.g.cs", sb.ToString());
	}

	private static void GenerateAbstractComponentIdArrays(
		GeneratorExecutionContext context,
		HashSet<INamedTypeSymbol> derivedComponents,
		ImmutableArray<INamedTypeSymbol> folderComponents)
	{
		var abstractComponents = derivedComponents
			.Where(t => t.TypeKind == TypeKind.Class && t.IsAbstract && !t.IsGenericType)
			.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
			.ToImmutableArray();

		var interfaceComponents = derivedComponents
			.SelectMany(t => t.AllInterfaces)
			.Where(t => t.TypeKind == TypeKind.Interface && !t.IsGenericType)
			.Where(IsInComponentsFolder)
			.Distinct(SymbolEqualityComparer.Default)
			.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
			.ToImmutableArray();

		if (abstractComponents.Length == 0 && interfaceComponents.Length == 0)
		{
			return;
		}

		// Map concrete components (with ID) -> symbol
		var concreteWithIds = folderComponents
			.Select((sym, id) => new { Symbol = sym, Id = (uint)id })
			.ToImmutableArray();

		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();
		sb.AppendLine("namespace Rpg.Entities;");
		sb.AppendLine();
		sb.AppendLine("public abstract partial class Component");
		sb.AppendLine("{");

		var usedArrayNames = new HashSet<string>(StringComparer.Ordinal);

		var emittedAny = false;
		foreach (var abs in abstractComponents)
		{
			var matching = concreteWithIds
				.Where(c => InheritsFrom(c.Symbol, abs))
				.ToImmutableArray();

			if (matching.Length == 0)
			{
				continue;
			}

			var name = abs.Name;
			if (name.EndsWith("Component", StringComparison.Ordinal))
			{
				name = name.Substring(0, name.Length - "Component".Length);
			}
			var arrayName = name + "IDs";
			if (!usedArrayNames.Add(arrayName))
			{
				arrayName = name + "AbstractIDs";
				usedArrayNames.Add(arrayName);
			}

			sb.Append("    public static readonly uint[] ").Append(arrayName).AppendLine(" =");
			sb.AppendLine("        new uint[]");
			sb.AppendLine("        {");
			foreach (var m in matching)
			{
				var fq = m.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sb.Append("            ").Append(fq).AppendLine(".ID,");
			}
			sb.AppendLine("        };");
			sb.AppendLine();
			emittedAny = true;
		}

		foreach (var iface in interfaceComponents)
		{
			var matching = concreteWithIds
				.Where(c => c.Symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface)))
				.ToImmutableArray();

			if (matching.Length == 0)
			{
				continue;
			}

			var name = iface.Name;
			if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
			{
				name = name.Substring(1);
			}
			if (name.EndsWith("Component", StringComparison.Ordinal))
			{
				name = name.Substring(0, name.Length - "Component".Length);
			}

			var arrayName = name + "IDs";
			if (!usedArrayNames.Add(arrayName))
			{
				arrayName = name + "InterfaceIDs";
				usedArrayNames.Add(arrayName);
			}

			sb.Append("    public static readonly uint[] ").Append(arrayName).AppendLine(" =");
			sb.AppendLine("        new uint[]");
			sb.AppendLine("        {");
			foreach (var m in matching)
			{
				var fq = m.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sb.Append("            ").Append(fq).AppendLine(".ID,");
			}
			sb.AppendLine("        };");
			sb.AppendLine();
			emittedAny = true;
		}

		sb.AppendLine("}");

		if (emittedAny)
		{
			context.AddSource("AbstractComponentIds.g.cs", sb.ToString());
		}
	}

	private static void GenerateComponentDependencyArrays(
		GeneratorExecutionContext context,
		HashSet<INamedTypeSymbol> derivedComponents,
		ImmutableArray<INamedTypeSymbol> folderComponents,
		INamedTypeSymbol requiredAttrSymbol,
		INamedTypeSymbol optionalAttrSymbol)
	{
		var idByType = new Dictionary<INamedTypeSymbol, uint>(SymbolEqualityComparer.Default);
		for (var i = 0; i < folderComponents.Length; i++)
		{
			idByType[folderComponents[i]] = (uint)i;
		}

		var candidates = derivedComponents
			.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsGenericType)
			.OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
			.ToImmutableArray();

		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();

		var groups = candidates
			.Select(sym => new
			{
				Symbol = sym,
				Namespace = sym.ContainingNamespace.IsGlobalNamespace ? null : sym.ContainingNamespace.ToDisplayString()
			})
			.GroupBy(x => x.Namespace, StringComparer.Ordinal);

		foreach (var group in groups)
		{
			if (!string.IsNullOrEmpty(group.Key))
			{
				sb.Append("namespace ").Append(group.Key).AppendLine();
				sb.AppendLine("{");
			}

			foreach (var item in group)
			{
				var type = item.Symbol;
				var (required, optional) = CollectDependencies(context, type, idByType, requiredAttrSymbol, optionalAttrSymbol);

				sb.Append("    public partial class ").Append(type.Name).AppendLine();
				sb.AppendLine("    {");
				sb.AppendLine("        private static readonly global::Rpg.Entities.Component.ComponentDependency[] __requiredComponentDependencies =");
				sb.AppendLine(required ?? "            global::System.Array.Empty<global::Rpg.Entities.Component.ComponentDependency>();");
				sb.AppendLine();
				sb.AppendLine("        private static readonly global::Rpg.Entities.Component.ComponentDependency[] __optionalComponentDependencies =");
				sb.AppendLine(optional ?? "            global::System.Array.Empty<global::Rpg.Entities.Component.ComponentDependency>();");
				sb.AppendLine();
				sb.AppendLine("        public override global::Rpg.Entities.Component.ComponentDependency[] RequiredComponentDependencies => __requiredComponentDependencies;");
				sb.AppendLine("        public override global::Rpg.Entities.Component.ComponentDependency[] OptionalComponentDependencies => __optionalComponentDependencies;");
				sb.AppendLine("    }");
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(group.Key))
			{
				sb.AppendLine("}");
				sb.AppendLine();
			}
		}

		context.AddSource("ComponentDependencies.g.cs", sb.ToString());
	}

	private static (string? requiredArrayExpr, string? optionalArrayExpr) CollectDependencies(
		GeneratorExecutionContext context,
		INamedTypeSymbol componentType,
		Dictionary<INamedTypeSymbol, uint> idByType,
		INamedTypeSymbol requiredAttrSymbol,
		INamedTypeSymbol optionalAttrSymbol)
	{
		var required = new List<string>();
		var optional = new List<string>();

		foreach (var member in componentType.GetMembers())
		{
			if (member is not IFieldSymbol and not IPropertySymbol)
			{
				continue;
			}
			if (member.IsStatic)
			{
				continue;
			}

			var attrs = member.GetAttributes();
			foreach (var attr in attrs)
			{
				var attrClass = attr.AttributeClass;
				if (attrClass is null)
				{
					continue;
				}

				var isRequired = SymbolEqualityComparer.Default.Equals(attrClass, requiredAttrSymbol);
				var isOptional = SymbolEqualityComparer.Default.Equals(attrClass, optionalAttrSymbol);
				if (!isRequired && !isOptional)
				{
					continue;
				}

				if (attr.ConstructorArguments.Length != 1)
				{
					continue;
				}

				if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol depType)
				{
					continue;
				}

				var memberType = member switch
				{
					IFieldSymbol f => f.Type,
					IPropertySymbol p => p.Type,
					_ => null
				};
				if (memberType is null || !SymbolEqualityComparer.Default.Equals(memberType, depType) || !IsWritable(member))
				{
					context.ReportDiagnostic(Diagnostic.Create(
						InvalidDependencyMemberDescriptor,
						member.Locations.FirstOrDefault(),
						componentType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
						member.Name,
						attrClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
					continue;
				}

				var depIdExpr = idByType.TryGetValue(depType, out var depId)
					? depId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "u"
					: "global::Rpg.Entities.Component.GetComponentTypeId(typeof(" + depType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "))";

				var componentFq = componentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var depFq = depType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var assign = member switch
				{
					IFieldSymbol => "((" + componentFq + ")self)." + member.Name + " = (" + depFq + ")value",
					IPropertySymbol => "((" + componentFq + ")self)." + member.Name + " = (" + depFq + ")value",
					_ => null
				};
				if (assign is null)
				{
					continue;
				}

				var entry =
					"            new global::Rpg.Entities.Component.ComponentDependency(" + depIdExpr +
					", static (global::Rpg.Entities.Component self, global::Rpg.Entities.Component value) => " + assign + ")";

				if (isRequired)
				{
					required.Add(entry);
				}
				else
				{
					optional.Add(entry);
				}
			}
		}

		string? requiredExpr = required.Count == 0
			? null
			: "            new global::Rpg.Entities.Component.ComponentDependency[]\n            {\n" + string.Join("\n", required.Select(x => x + ",")) + "\n            };";
		string? optionalExpr = optional.Count == 0
			? null
			: "            new global::Rpg.Entities.Component.ComponentDependency[]\n            {\n" + string.Join("\n", optional.Select(x => x + ",")) + "\n            };";

		// Only generate the partial if at least one of the two exists (we may choose not to touch the class otherwise).
		if (requiredExpr is null && optionalExpr is null)
		{
			return (null, null);
		}

		return (requiredExpr, optionalExpr);
	}

	private static bool IsWritable(ISymbol member)
	{
		return member switch
		{
			IFieldSymbol f => !f.IsReadOnly,
			IPropertySymbol p => p.SetMethod is not null && !p.SetMethod.IsInitOnly,
			_ => false
		};
	}

	private static bool IsInComponentsFolder(INamedTypeSymbol type)
	{
		foreach (var declRef in type.DeclaringSyntaxReferences)
		{
			var path = declRef.SyntaxTree.FilePath ?? string.Empty;
			// normalize to '/'
			path = path.Replace('\\', '/');
			if (path.Contains("/Rpg/Entities/Components/"))
			{
				return true;
			}
		}
		return false;
	}

	private static void GenerateComponentIdConsts(GeneratorExecutionContext context, ImmutableArray<INamedTypeSymbol> components)
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();

		// Do not use a file-scoped namespace here: the generated file contains multiple namespaces.
		var groups = components
			.Select((sym, id) => new
			{
				Symbol = sym,
				Id = (uint)id,
				Namespace = sym.ContainingNamespace.IsGlobalNamespace ? null : sym.ContainingNamespace.ToDisplayString()
			})
			.GroupBy(x => x.Namespace, StringComparer.Ordinal);

		foreach (var group in groups)
		{
			if (!string.IsNullOrEmpty(group.Key))
			{
				sb.Append("namespace ").Append(group.Key).AppendLine();
				sb.AppendLine("{");
			}

			foreach (var item in group)
			{
				sb.Append("    public partial class ").Append(item.Symbol.Name).AppendLine();
				sb.AppendLine("    {");
				sb.Append("        public const uint ID = ").Append(item.Id).AppendLine("u;");
				sb.AppendLine("    }");
				sb.AppendLine();
			}

			if (!string.IsNullOrEmpty(group.Key))
			{
				sb.AppendLine("}");
				sb.AppendLine();
			}
		}

		context.AddSource("ComponentIds.g.cs", sb.ToString());
	}

	private static void GenerateComponentIdMap(GeneratorExecutionContext context, ImmutableArray<INamedTypeSymbol> components)
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated />");
		sb.AppendLine("#nullable enable");
		sb.AppendLine();
		sb.AppendLine("namespace Rpg.Entities;");
		sb.AppendLine();
		sb.AppendLine("public abstract partial class Component");
		sb.AppendLine("{");
		sb.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<uint, global::System.Type> _componentTypesById =");
		sb.AppendLine("        new global::System.Collections.Generic.Dictionary<uint, global::System.Type>");
		sb.AppendLine("        {");
		for (var i = 0; i < components.Length; i++)
		{
			var symbol = components[i];
			var fq = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			sb.Append("            { ").Append(i).Append("u, typeof(").Append(fq).AppendLine(") },");
		}
		sb.AppendLine("        };");
		sb.AppendLine();
		sb.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, uint> _componentTypeIds =");
		sb.AppendLine("        new global::System.Collections.Generic.Dictionary<global::System.Type, uint>");
		sb.AppendLine("        {");
		for (var i = 0; i < components.Length; i++)
		{
			var symbol = components[i];
			var fq = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			sb.Append("            { typeof(").Append(fq).Append("), ").Append(i).AppendLine("u },");
		}
		sb.AppendLine("        };");
		sb.AppendLine();
		sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyDictionary<uint, global::System.Type> ComponentTypesById => _componentTypesById;");
		sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyDictionary<global::System.Type, uint> ComponentTypeIds => _componentTypeIds;");
		sb.AppendLine("}");

		context.AddSource("ComponentIdMap.g.cs", sb.ToString());
	}

	private static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
	{
		for (var current = symbol.BaseType; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current, baseType))
			{
				return true;
			}
		}
		return false;
	}

	private sealed class Receiver : ISyntaxReceiver
	{
		public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			if (syntaxNode is ClassDeclarationSyntax cds && cds.BaseList is not null)
			{
				CandidateClasses.Add(cds);
			}
		}
	}
}
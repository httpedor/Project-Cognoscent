using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ComponentGenerator;

/// <summary>
/// Emits, for each event type, the ids of the components that handle it and the delegate that
/// invokes the handler.
/// <para>
/// The event-to-listeners map is <b>flattened</b>: an event whose handlers are declared for a base
/// type gets its own direct entry, so dispatch is one dictionary lookup instead of a walk up the
/// base-type chain doing a lookup per level. The walk survives only as a fallback for event types
/// the generator could not see.
/// </para>
/// <para>
/// Only the tables are generated. The lookup and dispatch methods that read them are hand-written
/// in <c>Component.Events.cs</c>, where they can be read and stepped through.
/// </para>
/// </summary>
[Generator]
public sealed class ComponentEventGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(ComponentModel.Build);

        context.RegisterSourceOutput(model, static (spc, model) =>
        {
            if (model.IsEmpty || model.ComponentEvent is null || model.ComponentEventHandler is null) return;
            spc.AddSource("ComponentEventTables.g.cs", Emit(model));
        });
    }

    private static string Emit(ComponentModel model)
    {
        // Which events each component declares a handler for.
        var handledByComponent = new Dictionary<ComponentInfo, List<INamedTypeSymbol>>();
        var declaredEvents = new List<INamedTypeSymbol>();

        foreach (var component in model.Components)
        {
            var handled = component.Symbol.AllInterfaces
                .Where(i => i.IsGenericType
                            && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, model.ComponentEventHandler)
                            && i.TypeArguments.Length == 1)
                .Select(i => i.TypeArguments[0])
                .OfType<INamedTypeSymbol>()
                .Where(e => SymbolEqualityComparer.Default.Equals(e, model.ComponentEvent)
                            || ComponentModel.InheritsFrom(e, model.ComponentEvent!))
                .Distinct(SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>()
                .ToList();

            handledByComponent[component] = handled;
            foreach (var e in handled)
                if (!declaredEvents.Any(x => SymbolEqualityComparer.Default.Equals(x, e)))
                    declaredEvents.Add(e);
        }

        // Every concrete event in the assembly, so each can get a direct entry.
        var allEvents = declaredEvents
            .Concat(model.AllEventTypes)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .OrderBy(e => e.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal)
            .ToList();

        var listeners = new StringBuilder();
        var dispatchers = new StringBuilder();

        foreach (var eventType in allEvents)
        {
            // Walk up to the nearest ancestor any component actually declares a handler for. Doing
            // it here is what removes the walk from dispatch.
            INamedTypeSymbol? declaredFor = null;
            for (var current = eventType; current is not null; current = current.BaseType)
            {
                if (declaredEvents.Any(e => SymbolEqualityComparer.Default.Equals(e, current)))
                {
                    declaredFor = current;
                    break;
                }
                if (SymbolEqualityComparer.Default.Equals(current, model.ComponentEvent)) break;
            }
            if (declaredFor is null) continue;

            var matching = model.Components
                .Where(c => handledByComponent[c].Any(h => SymbolEqualityComparer.Default.Equals(h, declaredFor)))
                .ToList();
            if (matching.Count == 0) continue;

            var eventName = eventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var handlerName = declaredFor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            listeners.AppendLine($"            {{ typeof({eventName}), new uint[] {{ {string.Join(", ", matching.Select(m => m.FullyQualifiedName + ".ID"))} }} }},");
            dispatchers.AppendLine($"            {{ typeof({eventName}), static (c, e) => ((global::Rpg.Entities.ComponentEventHandler<{handlerName}>)c).HandleEvent(({handlerName})e) }},");
        }

        return ComponentIdGenerator.Header() + $$"""
            namespace Rpg.Entities;

            public abstract partial class Component
            {
                /// <summary>Component ids handling each event type, resolved through base types already.</summary>
                internal static readonly global::System.Collections.Generic.Dictionary<global::System.Type, uint[]> EventListenerIdsByEventType =
                    new global::System.Collections.Generic.Dictionary<global::System.Type, uint[]>
                    {
            {{listeners.ToString().TrimEnd()}}
                    };

                /// <summary>Invokes the handler a component declares for each event type.</summary>
                internal static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Action<global::Rpg.Entities.Component, global::Rpg.Entities.ComponentEvent>> EventDispatchersByEventType =
                    new global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Action<global::Rpg.Entities.Component, global::Rpg.Entities.ComponentEvent>>
                    {
            {{dispatchers.ToString().TrimEnd()}}
                    };
            }

            """;
    }
}

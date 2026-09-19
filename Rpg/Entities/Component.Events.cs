namespace Rpg.Entities;

/// <summary>
/// Event lookup and dispatch over the generated tables.
/// <para>
/// This was previously emitted as generated source — twenty-odd lines of method body assembled one
/// <c>AppendLine</c> at a time — even though none of it varies with the component set. Only the
/// tables need generating; the code that reads them lives here, where it can be read, stepped
/// through and changed without touching a generator.
/// </para>
/// </summary>
public abstract partial class Component
{
    /// <summary>Ids of the components that handle <paramref name="eventType"/>.</summary>
    public static uint[] GetEventListenerComponentIds(System.Type eventType)
        => GetEventListenerComponentIds(eventType, out _);

    /// <summary>
    /// Ids of the components that handle <paramref name="eventType"/>, along with the event type
    /// their handler was declared for — which is the type <see cref="DispatchEventToListener"/>
    /// needs in order to pick the right <c>HandleEvent</c> overload.
    /// </summary>
    public static uint[] GetEventListenerComponentIds(System.Type eventType, out System.Type? matchedEventType)
    {
        // The generated table is flattened: an event whose handlers were declared for a base type
        // still has its own entry, so this is one lookup rather than a walk.
        if (EventListenerIdsByEventType.TryGetValue(eventType, out var ids))
        {
            matchedEventType = eventType;
            return ids;
        }

        // Fallback for an event type the generator could not see — a closed generic constructed at
        // runtime, or one declared in another assembly.
        for (var type = eventType.BaseType;
             type is not null && typeof(ComponentEvent).IsAssignableFrom(type);
             type = type.BaseType)
        {
            if (!EventListenerIdsByEventType.TryGetValue(type, out ids)) continue;
            matchedEventType = type;
            return ids;
        }

        matchedEventType = null;
        return System.Array.Empty<uint>();
    }

    public static uint[] GetEventListenerComponentIds<TEvent>() where TEvent : ComponentEvent
        => GetEventListenerComponentIds(typeof(TEvent));

    /// <summary>Invokes <paramref name="component"/>'s handler for the event.</summary>
    public static void DispatchEventToListener(
        Component component, ComponentEvent componentEvent, System.Type matchedEventType)
    {
        if (EventDispatchersByEventType.TryGetValue(matchedEventType, out var dispatcher))
            dispatcher(component, componentEvent);
    }
}

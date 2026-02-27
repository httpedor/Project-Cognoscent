using System.Text.Json.Serialization;
using Rpg.Entities.Components;
using Rpg.Entities.Interfaces;

namespace Rpg.Entities;

public readonly struct EntityRef(string board, int id) : ISerializable
{
    public readonly string Board = board;
    public readonly int Id = id;

    [JsonIgnore]
    public Entity? Entity
    {
        get
        {
            Board? board = SidedLogic.Instance.GetBoard(Board);
            return board?.GetEntityById(Id);
        }
    }
    public EntityRef(Entity target) : this(target.Board.Name, target.Id)
    {
    }

    public EntityRef(Stream stream) : this(stream.ReadString(), stream.ReadInt32())
    {
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteString(Board);
        stream.WriteInt32(Id);
    }

    public static implicit operator Entity?(EntityRef entityRef)
    {
        return entityRef.Entity;
    }
}

public readonly struct EntityWith<T> where T : Component
{
    public readonly Entity Entity;
    public readonly T Component;

    public EntityWith(Entity entity, T component)
    {
        Entity = entity;
        Component = component;
        if (Component.Entity != entity)
            throw new InvalidOperationException("The provided component does not belong to the provided entity.");
    }
    public EntityWith(Entity entity) : this(entity, entity.GetComponent<T>()!)
    {
        if (Component == null)
            throw new InvalidOperationException("The provided entity does not have a component of the specified type.");
    }

    public static implicit operator Entity(EntityWith<T> entityWith)
    {
        return entityWith.Entity;
    }
    public static implicit operator T(EntityWith<T> entityWith)
    {
        return entityWith.Component;
    }
    public static implicit operator EntityWith<T>(Entity entity)
    {
        return new EntityWith<T>(entity);
    }
    public static implicit operator EntityWith<T>(T component)
    {
        return new EntityWith<T>(component.Entity, component);
    }
}
public abstract class EntityEvent : IImmediateEvent
{
    public readonly Entity Entity;

    protected EntityEvent(Entity entity)
    {
        Entity = entity;
    }
}
public class EntityReadyEvent(Entity entity) : EntityEvent(entity)
{
}
public partial class Entity : ISerializable
{
    // Aliases
    public FeaturesContainer? Features => FeaturesContainer;
    public StatsContainer? Stats => StatsContainer;

    private readonly Component?[] componentArray = new Component[Component.ComponentCount];
    private LinkedList<Component> nonNullComponents = new();
    private LinkedList<ComponentEvent> eventBus = new();
    public IEnumerable<Component> Components => nonNullComponents;
    public int Id { get; }

    public string Name;
    public string? Owner;
    public bool HasOwner => Owner != null;
    // ReSharper disable once InconsistentNaming
    [JsonIgnore]
    public string BBLink => "[url=gotoent " + Id + "]" + Name + "[/url]";

    public uint CreationTick { get; set; } = 0;
    [JsonIgnore]
    public uint ExistanceTicks => Board.CurrentTick - CreationTick;

    public Logger Logger;

    public Board Board { get; set; }
    public bool WasInitialized { get; private set; } = false;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public Entity(string? name = null)
    {
        Id = new Random().Next();
        Name = name ?? "Entity" + Id;
        Logger = new Logger(Name);
    }

    public Entity(Stream stream)
    {
        Id = stream.ReadInt32();
        Name = stream.ReadString();
        Owner = stream.ReadString();
        if (Owner == "")
            Owner = null;
        CreationTick = stream.ReadUInt32();
        for (int i = 0; i < componentArray.Length; i++)
        {
            bool hasComponent = stream.ReadBoolean();
            if (hasComponent)
            {
                var component = Component.FromBytes(stream);
                component.Entity = this;
                componentArray[i] = component;
                nonNullComponents.AddLast(component);
            }
        }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public void Tick()
    {
        foreach (var id in Component.TickableIDs)
        {
            if (componentArray[id] is ITickableComponent tickable)
            {
                tickable.PreTick();
            }
        }

        foreach (var id in Component.TickableIDs)
        {
            if (componentArray[id] is ITickableComponent tickable)
            {
                tickable.OnTick();
            }
        }
        foreach (var id in Component.TickableIDs)
        {
            if (componentArray[id] is ITickableComponent tickable)
            {
                tickable.PostTick();
            }
        }
        LinkedListNode<ComponentEvent>? current;
        do {
            current = eventBus.First;
            if (current == null)
                break;
            var ev = current.Value;
            DispatchEventImmediate(ev);
            eventBus.RemoveFirst();
            current = eventBus.First;
        } while (current != null);
    }

    public void Initialize()
    {
        foreach (var component in nonNullComponents)
        {
            Component.AssignDependencies(this, component);
            component.OnInit(this);
        }
        WasInitialized = true;
    }

    public void OnReady()
    {
        DispatchEvent(new EntityReadyEvent(this));
        foreach (var component in nonNullComponents)
        {
            component.OnReady();
        }
    }

    public void AddComponent(Component component)
    {
        uint typeId = component.GetId();
        foreach (var dep in component.RequiredComponentDependencies)
        {
            if (componentArray[dep.ComponentTypeId] is null)
            {
                throw new InvalidOperationException($"Cannot add component of type {component.GetType().Name} to entity {Id} because required component of type ID {dep.ComponentTypeId} is missing.");
            }
        }
        componentArray[typeId] = component;
        nonNullComponents.AddLast(component);
        Component.AssignDependencies(this, component);

        component.OnAddedTo(this);
        DispatchEvent(new ComponentAddedEvent(component));
    }
    public void RemoveComponent(uint typeId)
    {
        var component = componentArray[typeId];
        if (component == null)
            return;
        component.OnRemovedFrom(this);
        componentArray[typeId] = null;
        nonNullComponents.Remove(component);
        DispatchEvent(new ComponentRemovedEvent(component));
    }

    [Obsolete("Use HasComponent(uint typeId) instead. It's are more efficient.")]
    public bool HasComponent<T>() where T : Component
    {
        return componentArray[Component.GetComponentId<T>()] is not null;
    }
    public bool HasComponent(uint typeId)
    {
        return componentArray[typeId] is not null;
    }
    public Component? GetComponent(uint typeId)
    {
        return componentArray[typeId];
    }
    public T? GetComponent<T>() where T : Component
    {
        return (T?)componentArray[Component.GetComponentId<T>()];
    }
    
    public bool TryGetComponent<T>(out T component) where T : Component
    {
        var comp = componentArray[Component.GetComponentId<T>()];
        if (comp is T tComp)
        {
            component = tComp;
            return true;
        }
        component = null!;
        return false;
    }
    public bool TryGetComponent(uint typeId, out Component? component)
    {
        var comp = componentArray[typeId];
        if (comp is not null)
        {
            component = comp;
            return true;
        }
        component = null;
        return false;
    }

    public void Log(string message, LogLevel level = LogLevel.Info, ConsoleColor? color = null)
    {
        Logger.Log(message, level, color);
    }
    public void Log(Component component, string message, LogLevel level = LogLevel.Info)
    {
        Logger.Log($"[{component.GetType().Name}] {message}", level);
    }

    public void DispatchEvent(ComponentEvent componentEvent)
    {
        if (componentEvent is IImmediateEvent)
        {
            DispatchEventImmediate(componentEvent);
            return;
        }
        eventBus.AddLast(componentEvent);
    }
    public void DispatchEvent(EntityEvent entityEvent)
    {
        Board?.HandleEvent(entityEvent);
    }
    private void DispatchEventImmediate(ComponentEvent componentEvent)
    {
        var ids = Component.GetEventListenerComponentIds(componentEvent.GetType(), out var matchedEventType);
        if (matchedEventType is not null && ids.Length != 0)
        {
            foreach (var id in ids)
            {
                var comp = componentArray[id];
                if (comp != null)
                {
                    Component.DispatchEventToListener(comp, componentEvent, matchedEventType);
                    Board?.ComponentHandledEvent(comp, componentEvent);
                    if (componentEvent is CancellableComponentEvent cancellableEvent && cancellableEvent.Canceled)
                        return;
                }
            }
            Board?.HandleEvent(componentEvent);
        }
    }

    public void Destroy()
    {
        var current = nonNullComponents.First;
        while (current != null && current.Value != null)
        {
            current.Value.Destroy();
            current = current.Next;
        }
    }
    public virtual void ToBytes(Stream stream)
    {
        stream.WriteInt32(Id);
        stream.WriteString(Name);
        stream.WriteString(Owner ?? "");
        stream.WriteUInt32(CreationTick);
        foreach (var component in componentArray)
        {
            stream.WriteBoolean(component is not null);
            component?.ToBytes(stream);
        }
    }
}

public class ComponentAddedEvent(Component component) : ComponentEvent(component), IImmediateEvent
{
}

public class ComponentRemovedEvent(Component component) : ComponentEvent(component), IImmediateEvent
{
}
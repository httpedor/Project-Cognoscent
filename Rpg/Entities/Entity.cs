using System.Text.Json.Serialization;

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

public partial class Entity : ISerializable
{
    private readonly Component?[] componentArray = new Component[Component.ComponentCount];
    private LinkedList<Component> nonNullComponents = new();
    private LinkedList<ComponentEvent> eventBus = new();
    public IEnumerable<Component> Components => nonNullComponents;
    public int Id { get; }

    public string Name;
    // ReSharper disable once InconsistentNaming
    [JsonIgnore]
    public string BBLink => "[url=gotoent " + Id + "]" + Name + "[/url]";

    public uint CreationTick { get; set; } = 0;
    [JsonIgnore]
    public uint ExistanceTicks => Board.CurrentTick - CreationTick;

    public Logger Logger;

    public Board Board { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected Entity()
    {
        Id = new Random().Next();
        Name = "Entity" + Id;
        Logger = new Logger(Name);
    }

    public Entity(Stream stream)
    {
        Id = stream.ReadInt32();
        Name = stream.ReadString();
        CreationTick = stream.ReadUInt32();
        for (int i = 0; i < componentArray.Length; i++)
        {
            bool hasComponent = stream.ReadBoolean();
            if (hasComponent)
            {
                var component = Component.FromBytes(stream);
                componentArray[i] = component;
                nonNullComponents.AddLast(component);
            }
        }
        Logger = new Logger(stream);
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
        var current = eventBus.First;
        //Avoid foreach so that we can add events while iterating
        while (current != null)
        {
            var ev = current.Value;
            var ids = Component.GetEventListenerComponentIds(ev.GetType(), out var matchedEventType);
            if (matchedEventType is not null && ids.Length != 0)
            {
                foreach (var id in ids)
                {
                    var comp = componentArray[id];
                    if (comp != null)
                    {
                        Component.DispatchEventToListener(comp, ev, matchedEventType);
                    }
                }
                Board.HandleEvent(ev);
            }

            current = current.Next;
        }
    }

    public void Initialize()
    {
        
        foreach (var component in nonNullComponents)
        {
            Component.AssignDependencies(this, component);
            component.OnInit(this);
        }
    }

    public void AddComponent(Component component)
    {
        uint typeId = Component.GetComponentId(component.GetType());
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

        if (Board != null)
        {
            Board.OnEntityComponentAdded(this, typeId);
        }

        component.OnAddedTo(this);

        foreach (var otherComponent in nonNullComponents)
        {
            if (otherComponent != component)
            {
                otherComponent.OnComponentAdded(this, component);
            }
        }
    }
    public void RemoveComponent(uint typeId)
    {
        var component = componentArray[typeId];
        if (component == null)
            return;
        component.OnRemovedFrom(this);
        componentArray[typeId] = null;
        nonNullComponents.Remove(component);
        if (Board != null)
        {
            Board.OnEntityComponentRemoved(this, typeId);
        }
        foreach (var otherComponent in nonNullComponents)
        {
            otherComponent.OnComponentRemoved(this, component!);
        }
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
        eventBus.AddLast(componentEvent);
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
        stream.WriteUInt32(CreationTick);
        foreach (var component in componentArray)
        {
            stream.WriteBoolean(component is not null);
            component?.ToBytes(stream);
        }
        Logger.ToBytes(stream);
    }
}

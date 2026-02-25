using System;
using System.Text.Json.Serialization;

namespace Rpg.Entities;

public abstract class ComponentEvent
{
    public Component Component;
    public ComponentEvent(Component component) {
        Component = component;
    }
}
public interface IImmediateEvent
{
}
public abstract class CancellableComponentEvent : ComponentEvent, IImmediateEvent
{
    public bool Canceled = false;
    public CancellableComponentEvent(Component component) : base(component)
    {
    }
}
public interface ComponentEventHandler<T> where T : ComponentEvent
{
    void HandleEvent(T componentEvent);
}
public class ComponentRef<T> where T : Component {
    public uint ComponentTypeId;
    public EntityRef EntityRef;
    public T? Component => (T?)(EntityRef.Entity?.GetComponent(ComponentTypeId));

    public ComponentRef(Entity entity)
    {
        EntityRef = new EntityRef(entity);
        ComponentTypeId = Rpg.Entities.Component.GetComponentId(typeof(T));
    }
    public ComponentRef(T component)
    {
        ComponentTypeId = Entities.Component.GetComponentId(typeof(T));
        EntityRef = new EntityRef(component.Entity);
    }
    public ComponentRef(Stream stream)
    {
        ComponentTypeId = stream.ReadUInt32();
        EntityRef = new EntityRef(stream);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteUInt32(ComponentTypeId);
        EntityRef.ToBytes(stream);
    }
}
public class GenericComponentRef : ISerializable {
    public uint ComponentTypeId;
    public EntityRef EntityRef;
    public Component? Component => EntityRef.Entity?.GetComponent(ComponentTypeId);

    public GenericComponentRef(Entity entity, uint componentTypeId)
    {
        EntityRef = new EntityRef(entity);
        ComponentTypeId = componentTypeId;
    }
    public GenericComponentRef(EntityRef entityRef, uint componentTypeId)
    {
        EntityRef = entityRef;
        ComponentTypeId = componentTypeId;
    }
    public GenericComponentRef(Component component)
    {
        ComponentTypeId = Entities.Component.GetComponentId(component.GetType());
        EntityRef = new EntityRef(component.Entity);
    }

    public GenericComponentRef(Stream stream)
    {
        ComponentTypeId = stream.ReadUInt32();
        EntityRef = new EntityRef(stream);
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteUInt32(ComponentTypeId);
        EntityRef.ToBytes(stream);
    }
}
public abstract partial class Component : ISerializable
{
    public readonly struct ComponentDependency(uint componentTypeId, Action<Component, Component> assign)
    {
        public readonly uint ComponentTypeId = componentTypeId;
        public readonly Action<Component, Component> Assign = assign;
    }

    public required Entity Entity;

    [JsonIgnore]
    public Board Board => Entity.Board;
    public bool WasInitialized {get; private set;} = false;
    private readonly Dictionary<string, List<(int entityId, uint componentId)>> entitiesToFind = new();

    public Component()
    {

    }
    public Component(Stream stream)
    {

    }

    public static implicit operator ComponentRef<Component>(Component component) => new(component);
    public static implicit operator Entity(Component component) => component.Entity;

    public virtual ComponentDependency[] RequiredComponentDependencies => Array.Empty<ComponentDependency>();
    public virtual ComponentDependency[] OptionalComponentDependencies => Array.Empty<ComponentDependency>();

    public virtual bool CanBeAddedTo(Entity entity)
    {
        return true;
    }
    public virtual void OnInit(Entity entity)
    {
        foreach (var (group, list) in entitiesToFind)
        {
            List<Component> foundComponents = new();
            foreach (var (entityId, componentId) in list)
            {
                Entity? targetEntity = Board?.GetEntityById(entityId);
                if (targetEntity != null)
                {
                    Component? component = targetEntity.GetComponent(componentId);
                    if (component != null)
                    {
                        foundComponents.Add(component);
                    }
                }
            }
            OnFoundComponents(group, foundComponents);
        }
        WasInitialized = true;
    }
    public virtual void OnReady()
    {
    }
    public virtual void OnAddedTo(Entity entity)
    {
    }
    public virtual void OnRemovedFrom(Entity entity)
    {
    }
    protected virtual void OnFoundComponents(string group, List<Component> components)
    {
    }

    public abstract uint GetId();

    public virtual void Destroy()
    {
        Entity.RemoveComponent(Component.GetComponentId(GetType()));
    }

    protected void LookForComponent<T>(string group, int entityId) where T : Component
    {
        if (!entitiesToFind.ContainsKey(group))
            entitiesToFind[group] = new List<(int, uint)>();
        entitiesToFind[group].Add((entityId, Component.GetComponentId<T>()));
    }
    protected void SaveComponentRef<T>(Stream stream, T component) where T : Component
    {
        stream.WriteInt32(component.Entity.Id);
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteUInt32(_componentTypeIds[GetType()]);
    }

    public static Component FromBytes(Stream stream)
    {
        uint id = stream.ReadUInt32();

        if (!_componentTypesById.TryGetValue(id, out var type))
        {
            throw new InvalidOperationException($"Unknown component type with ID {id}");
        }
        var component = (Component)Activator.CreateInstance(type, [stream])!;
        return component;
    }
    public static void AssignDependencies(Entity entity, Component component)
    {
        foreach (var dep in component.RequiredComponentDependencies)
        {
            var depComponent = entity.GetComponent(dep.ComponentTypeId)
                ?? throw new InvalidOperationException($"Entity {entity.Id} is missing required component of type ID {dep.ComponentTypeId} for component of type {component.GetType().Name}");
            dep.Assign(component, depComponent);
        }
        foreach (var dep in component.OptionalComponentDependencies)
        {
            var depComponent = entity.GetComponent(dep.ComponentTypeId);
            if (depComponent != null)
            {
                dep.Assign(component, depComponent);
            }
        }
    }
    public static uint GetComponentId<T>() where T : Component
    {
        return _componentTypeIds[typeof(T)];
    }
    public static uint GetComponentId(Type type)
    {
        return _componentTypeIds[type];
    }
    public static int ComponentCount => _componentTypesById.Count;
}
using Godot;
using Rpg.Entities;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

public abstract partial class ComponentRendererBase : Node2D, IContextMenuProvider
{
    public Component ComponentGeneric {get; private set;}
    public EntityRenderer EntityRenderer { get; }
    public ClientBoard Board { get; }

    public ComponentRendererBase(Component component, EntityRenderer entity)
    {
        ComponentGeneric = component;
        Board = (ClientBoard)entity.Entity.Board;
        EntityRenderer = entity;
    }

    public virtual void OnReady()
    {
        
    }

    public virtual void AddContextMenuOptions()
    {

    }
    public virtual void AddGMContextMenuOptions()
    {

    }

    public virtual void MouseEntered()
    {

    }
    public virtual void MouseExited()
    {

    }
    public virtual void OnClick()
    {

    }

    public virtual void EventFired(ComponentEvent e)
    {
        
    }
    public virtual void EventHandled(ComponentEvent e)
    {

    }
}
public abstract partial class ComponentRenderer<T> : ComponentRendererBase, IContextMenuProvider where T : Component
{
    public T Component { get; private set; }
    public Entity Entity => Component.Entity;

    public ComponentRenderer(T component, EntityRenderer entity) : base(component, entity)
    {
        Component = component;
    }

}
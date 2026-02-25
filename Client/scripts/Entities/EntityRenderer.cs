using System;
using System.Collections.Generic;
using Godot;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

using RendererBuilder = Func<Component, EntityRenderer, ComponentRendererBase>;

public partial class EntityRenderer : Node2D, IContextMenuProvider
{
    private readonly static RendererBuilder?[] rendererBuilders = new RendererBuilder[Component.ComponentCount];
    static EntityRenderer()
    {
        rendererBuilders[Token.ID] = (component, entity) => new TokenRenderer((Token)component, entity);
        rendererBuilders[Light.ID] = (component, entity) => new LightRenderer((Light)component, entity);
        rendererBuilders[SkillExecutor.ID] = (component, entity) => new SkillExecutorRenderer((SkillExecutor)component, entity);
        rendererBuilders[Body.ID] = (component, entity) => new BodyRenderer((Body)component, entity);
    }

    private readonly ComponentRendererBase?[] componentArray = new ComponentRendererBase[Component.ComponentCount];
    private readonly LinkedList<ComponentRendererBase> componentRenderers = new();
    public bool Hoverable;
    public bool Clickable;
    public bool NameKnown;
    public MidiaNode? Display { get; private set; }
    public Entity Entity { get; }
    public ClientBoard Board { get; }
    public EntityRenderer(Entity entity, ClientBoard board)
    {
        Entity = entity;
        Board = board;

        foreach (var component in entity.Components)
        {
            var builder = rendererBuilders[component.GetId()];
            if (builder != null)
            {
                var renderer = builder(component, this);
                componentArray[component.GetId()] = renderer;
                componentRenderers.AddLast(renderer);
                AddChild(renderer);
                if (renderer is TokenRenderer tokenRenderer)
                {
                    Display = tokenRenderer.Display;
                }
            }
        }
    }

    public void OnReady()
    {
        foreach (var componentRenderer in componentRenderers)
        {
            componentRenderer.OnReady();
        }
    }

    public void OnClick()
    {
        if (!Clickable)
            return;
        Board.SelectedEntity = Entity;
        foreach (var componentRenderer in componentRenderers)
        {
            componentRenderer.OnClick();
        }
    }

    public void MouseEntered()
    {
        if (!Hoverable)
            return;
        foreach (var componentRenderer in componentRenderers)
        {
            componentRenderer.MouseEntered();
        }
        if (Hoverable)
        {
            Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
            InputManager.RequestPriority(this);
        }
    }
    public void MouseExited()
    {
        foreach (var componentRenderer in componentRenderers)
        {
            componentRenderer.MouseExited();
        }
        if (InputManager.HasPriority(this))
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        InputManager.ReleasePriority(this);
    }

    public void AddGMContextMenuOptions()
    {
        foreach (var componentRenderer in componentRenderers)
        {
            componentRenderer.AddGMContextMenuOptions();
        }
    }

    public void AddContextMenuOptions()
    {
        foreach (var componentRenderer in componentRenderers)
        {
            componentRenderer.AddContextMenuOptions();
        }
    }

    public ComponentRendererBase? GetComponentRenderer(uint componentId)
    {
        if (componentId >= componentArray.Length)
            return null;
        return componentArray[componentId];
    }
    public T? GetComponentRenderer<T>(uint componentId) where T : ComponentRendererBase
    {
        return GetComponentRenderer(componentId) as T;
    }
}
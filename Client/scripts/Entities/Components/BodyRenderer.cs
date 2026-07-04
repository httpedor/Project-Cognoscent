using Godot;
using Rpg.Entities;
using Rpg.Entities.Components.Health;

namespace TTRpgClient.scripts;

public partial class BodyRenderer : ComponentRenderer<Body>
{
    public BodyRenderer(Body component, EntityRenderer entity) : base(component, entity)
    {
        
    }

    public override void EventHandled(ComponentEvent e)
    {
        base.EventHandled(e);
        if (e is BodyLayerInjuryAddedEvent)
        {
            EntityRenderer.Modulate = new Color(1, 0, 0, Modulate.A);
        }
        if (e is BodyLayerInjuryRemovedEvent)
        {
            EntityRenderer.Modulate = new Color(0, 1, 0, Modulate.A);
        }
    }
}
using Rpg.Entities;
using Rpg.Entities.Components;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts;

public partial class SkillExecutorRenderer : ComponentRenderer<SkillExecutor>
{
    public SkillExecutorRenderer(SkillExecutor component, EntityRenderer entity) : base(component, entity)
    {
        
    }

    public override void EventFired(ComponentEvent e)
    {
        base.EventFired(e);
        if (e is ActionLayerChangedEvent && Board.TurnMode)
            InitiativeBar.PopulateWithBoard(Board);
        if (e is ActionLayerRemovedEvent && Board.TurnMode)
            InitiativeBar.PopulateWithBoard(Board);
    }
}
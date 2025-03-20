using Godot;
using Rpg.Entities;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

public partial class PropNode : EntityNode
{
    PropEntity Prop;
    public PropNode(PropEntity ent, ClientBoard board) : base(ent, board)
    {
        CircleMask = false;
        Prop = ent;
    }

    protected override void MouseEntered()
    {
        if (GameManager.IsGm && (GameManager.Instance.CurrentBoard == null || !(GameManager.Instance.CurrentBoard.SelectedEntity is Creature)))
            base.MouseEntered();
        else if (Prop.MidiaToShow != null && Prop.MidiaToShow.Length > 0)
        {
            Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
            InputManager.RequestPriority(this);
        }
    }
    protected override void MouseExited()
    {
        if (GameManager.IsGm && (GameManager.Instance.CurrentBoard == null || !(GameManager.Instance.CurrentBoard.SelectedEntity is Creature)))
            base.MouseExited();
        else if (Prop.MidiaToShow != null && Prop.MidiaToShow.Length > 0)
        {
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            InputManager.ReleasePriority(this);
        }
    }

    public override void OnClick()
    {
        if (GameManager.IsGm && (GameManager.Instance.CurrentBoard == null || !(GameManager.Instance.CurrentBoard.SelectedEntity is Creature)))
            base.OnClick();
        else if (Prop.MidiaToShow != null && Prop.MidiaToShow.Length > 0)
        {
            //TODO: Show midia in a popup
        }
    }
}

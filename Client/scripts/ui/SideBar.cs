using Godot;

namespace TTRpgClient.scripts.ui;

public partial class SideBar : Control
{
    private TabContainer tabs = null!;
    private Button visionButton = null!;
    public override void _Ready()
    {
        base._Ready();
        tabs = GetNode<TabContainer>("TabContainer");
		visionButton = GetNode<Button>("VisionBtn");
		
		visionButton.Pressed += () =>
		{
			if (AnchorLeft < 1)
			{
				// Should close
				Tween tween = GetTree().CreateTween()
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Bounce);
				tween.TweenProperty(this, "anchor_left", 1, .8);
				tween.TweenCallback(Callable.From(() =>
				{
					tabs.Visible = false;
				}));
				visionButton.Text = "<";
			}
			else
			{
				// Should open
				Tween tween = GetTree().CreateTween()
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);
				tween.TweenProperty(this, "anchor_left", .85, .8);
				tabs.Visible = true;
				visionButton.Text = ">";
			}
		};

        for (int i = 0; i < tabs.GetTabCount(); i++)
        {
            tabs.SetTabIconMaxWidth(i, 24);
        }
        tabs.SetTabIcon(0, Icons.Chat);
        tabs.SetTabIcon(1, Icons.Book);
        tabs.SetTabTitle(0, "");
        tabs.SetTabTitle(1, "");
    }
}
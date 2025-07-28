using Godot;
using Rpg;
using System;
using System.Linq;

public partial class BPSelect : ColorRect
{
	[Export]
	public String BodyPartPath;
	public BodyPart? BodyPart
	{
		get;
		private set;
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Color = new Color(Color, 0.6f);
		MouseEntered += () => {
			Color = new Color(Color, 0.8f);
		};
		MouseExited += () => {
			Color = new Color(Color, 0.6f);
		};
	
		BodyInspector.Instance.BodyChanged += (old, @new) => {
			foreach (var child in GetChildren())
			{
				child.QueueFree();
			}
			var settings = BodyInspector.Instance.Settings;
			BodyPart = @new?.GetBodyPart(BodyPartPath);
			if (BodyPart == null || (settings.Predicate != null && !settings.Predicate(BodyPart) && (BodyPart.InternalOrgans.Count() + BodyPart.OverlappingParts.Count()) <= 0))
				Visible = false;
			else
			{
				Visible = true;
				if (settings.BackgroundColor != null)
					Color = new Color(settings.BackgroundColor(BodyPart), 0.6f);
				if (settings.Icon != null)
				{
					AddChild(new TextureRect
					{
						Texture = settings.Icon(BodyPart),
						ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
						AnchorBottom = 1,
						AnchorTop = 0,
						AnchorLeft = 0,
						AnchorRight = 1
					});
				}
				if (settings.InText != null)
				{
					AddChild(new Label
					{
						Text = settings.InText(BodyPart),
						LabelSettings = new LabelSettings
						{
							FontSize = 15,
							FontColor = Colors.Black,
							OutlineColor = Colors.White,
							OutlineSize = 4
						},
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						AnchorBottom = 1,
						AnchorTop = 0,
						AnchorLeft = 0,
						AnchorRight = 1
					});
				}
			}
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
		if (!Visible)
			return;

		AcceptEvent();
		if (@event is InputEventMouseButton e && e.IsPressed())
		{
			BodyInspector.Instance.Current = BodyPart;
			if (e.IsDoubleClick())
				BodyInspector.Instance.SelectCurrent();
		}
    }
}

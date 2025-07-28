using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using TTRpgClient.scripts;

public record RadialMenuOption
{
	public string Title;
	public string? Description;
	public Action<Vector2> Action;
    public Texture2D? Icon;

    public RadialMenuOption(string title, Action<Vector2> action)
    {
		Title = title;
        Action = action;
    }
	public RadialMenuOption(string title, string description, Action<Vector2> action)
	{
		Title = title;
		Description = description;
		Action = action;
	}

	public RadialMenuOption(Texture2D icon, string title, Action<Vector2> action)
	{
		Title = title;
		Action = action;
		Icon = icon;
	}

	public RadialMenuOption(Texture2D icon, string title, string description, Action<Vector2> action)
	{
		Title = title;
		Description = description;
		Action = action;
		Icon = icon;
	}
}

public partial class RadialMenu : Control
{
	private Dictionary<string, RadialMenuOption> options = new();
	private Vector2 menuOpenedPosition;
	private float childrenFactor = 1;
	private int centerInfoIndex = -2;
	private RichTextLabel? centerInfo;
	public static RadialMenu Instance
	{
		get;
		private set;
	}

	public Control GDRadialMenu
	{
		get;
		private set;
	}

	public bool FirstInCenter
	{
		get => GDRadialMenu.Get("first_in_center").AsBool();
		set => GDRadialMenu.Set("first_in_center", value);
	}

	public int Selection
	{
		get => GDRadialMenu.Get("selection").AsInt32();
		set => GDRadialMenu.Set("selection", value);
	}

	public int InnerRadius
	{
		get => GDRadialMenu.Get("arc_inner_radius").AsInt32();
		set => GDRadialMenu.Set("arc_inner_radius", value);

	}

	public int OptionCount => options.Count;

	public RadialMenu()
	{
		Instance = this;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GDRadialMenu = GetNode<Control>("RadialMenu");
		GDRadialMenu.Connect("slot_selected", new Callable(this, MethodName.OnSlotSelected));

		childrenFactor = (float)GDRadialMenu.Get("children_auto_sizing_factor");

		Hide();
	}

	private void OnSlotSelected(Variant slot, int index)
	{
		if (options.Count == 0)
			return;
		if (options.Count == 1)
		{
			if (index == -1 && FirstInCenter)
				options.Values.First().Action(menuOpenedPosition);
			return;
		}
		options[((Node)slot).Name].Action(menuOpenedPosition);
	}

	public bool IsOpen
	{
		get
		{
			return Visible;
		}
	}

	public void Show(bool changePos)
	{
		if (options.Count == 0)
			return;

		GDRadialMenu.QueueRedraw();
		GDRadialMenu.Set("enabled", true);
		GDRadialMenu.Visible = true;
		Visible = true;
		if (options.Count == 1)
		{
			var element = (Control)GDRadialMenu.GetChild(0);
			element.GlobalPosition = GDRadialMenu.Position + GDRadialMenu.Size / 2 - element.Size / 2;
			FirstInCenter = true;
		}
		else
			FirstInCenter = false;
		Tween tween = GetTree().CreateTween()
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quint);
		var viewportSize = GetViewportRect().Size;
		var outer_radius = Math.Min(viewportSize.X/5, viewportSize.Y/5);
		if (changePos)
		{
			GDRadialMenu.Position = GetViewport().GetMousePosition() - viewportSize/2;
			menuOpenedPosition = InputManager.Instance.MousePosition;
		}
		tween.Parallel().TweenProperty(GDRadialMenu, "circle_radius", outer_radius, .3);
		tween.Parallel().TweenProperty(GDRadialMenu, "arc_inner_radius", outer_radius/3 * 2, .3);
		tween.Parallel().TweenProperty(GDRadialMenu, "children_auto_sizing_factor", childrenFactor, .3);
	}

	public new void Show()
	{
		Show(true);
	}
	
	public void Hide(Action? onHide = null)
	{
		Tween tween = GetTree().CreateTween()
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(GDRadialMenu, "circle_radius", 2, .3);
		tween.Parallel().TweenProperty(GDRadialMenu, "arc_inner_radius", 1, .3);
		tween.Parallel().TweenProperty(GDRadialMenu, "children_auto_sizing_factor", childrenFactor, .3);
		tween.Finished += () =>
		{
			GDRadialMenu.Set("enabled", false);
			Visible = false;
			GDRadialMenu.Visible = false;
			ClearOptions();
            onHide?.Invoke();
        };
	}

	public void AddOption(RadialMenuOption option)
	{
		options[option.Title] = option;
		if (option.Icon != null)
		{
			GDRadialMenu.AddChild(new TextureRect
			{
				Texture = option.Icon,
				Name = option.Title,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			});
		}
		else
		{
			GDRadialMenu.AddChild(new Label
			{
				Name = option.Title,
				Text = option.Title,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				LabelSettings = new LabelSettings
				{
					FontColor = new Color(1, 1, 1, 1)
				}
			});
		}
	}

	public void AddOption(string label, Action<Vector2> action)
	{
		AddOption(new RadialMenuOption(label, action));
	}

	public void ClearOptions()
	{
		options.Clear();
		foreach (var child in GDRadialMenu.GetChildren())
		{
			child.QueueFree();
		}
	}

    public override void _Process(Double delta)
    {
        base._Process(delta);


		var sel = Selection;
		if (IsOpen && !FirstInCenter && sel != -2)
		{
			if (centerInfoIndex != sel)
			{
				Control node = (Control)GDRadialMenu.Get("childs").AsGodotDictionary()[sel.ToString()];
				var option = options[node.Name];

				if (centerInfo != null)
				{
					centerInfo.QueueFree();
					centerInfo = null;
				}

				centerInfo = new RichTextLabel {
					Text = $"[center][font_size=28][b]{option.Title}[/b][/font_size]\n[font_size=18]{option.Description}[/font_size]",
					BbcodeEnabled = true,
					FitContent = true,
					ScrollActive = false,
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					Size = new Vector2(InnerRadius*2/Mathf.Sqrt(2), InnerRadius*2/Mathf.Sqrt(2)),
				};
				centerInfo.GlobalPosition = GDRadialMenu.Position + GDRadialMenu.Size / 2 - centerInfo.Size / 2;
				AddChild(centerInfo);

				centerInfoIndex = sel;
			}
		}
		else if (centerInfo != null)
		{
			centerInfo.QueueFree();
			centerInfo = null;
		}
    }
}

using Godot;
using System;
using System.Collections.Generic;

public partial class ChatControl : Control
{
	public static ChatControl Instance
	{
		get;
		private set;
	}
	public bool IsMouseIn
	{
		get
		{
			return GetGlobalRect().HasPoint(GetGlobalMousePosition());
		}
	}
	public bool IsInputFocused
	{
		get
		{
			return Input.HasFocus();
		}
	}
	private ColorRect BG;
	private Button VisionButton;
	private LineEdit Input;
	private VBoxContainer vBox;
	private ScrollContainer scrollContainer;
	public Color BackgroundColor
	{
		get
		{
			return BG.Color;
		}
		set
		{
			BG.Color = value;
		}
	}

	public ChatControl()
	{
		Instance = this;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		VisionButton = GetNode<Button>("VisionBtn");
		BG = GetNode<ColorRect>("BG");
		Input = GetNode<LineEdit>("Input");
		Input.TextSubmitted += (string text) =>
		{
			if (text.Length > 0)
			{
				if (Input.Text.StartsWith("/"))
				{
					var command = Input.Text.Substring(1);
					GameManager.Instance.ExecuteCommand(command);
					Input.Text = "";
					return;
				}
				var board = GameManager.Instance.CurrentBoard;
				if (board != null)
					board.BroadcastMessage(text);
				Input.Text = "";
			}
		};
		scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
		vBox = scrollContainer.GetNode<VBoxContainer>("VBoxContainer");
		VisionButton.Pressed += () =>
		{
			if (AnchorLeft < 1)
			{
				// Should close
				Tween tween = GetTree().CreateTween()
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Bounce);
				tween.TweenProperty(this, "anchor_left", 1, .8);
				VisionButton.Text = "<";
				Input.Visible = false;
			}
			else
			{
				// Should open
				Tween tween = GetTree().CreateTween()
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);
				tween.TweenProperty(this, "anchor_left", .85, .8);
				VisionButton.Text = ">";
				Input.Visible = true;
			}
		};

		GetWindow().SizeChanged += () =>
		{
			foreach (var child in vBox.GetChildren())
			{
				if (child is RichTextLabel rtl)
					rtl.CustomMinimumSize = new Vector2(Size.X, 0);
			}
		};
	}

	public void AddMessage(string message)
	{
		vBox.AddChild(new RichTextLabel()
		{
			Text = message,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(Size.X, 0),
		});

		scrollContainer.SetDeferred("scroll_vertical", (int)Math.Ceiling(scrollContainer.GetVScrollBar().MaxValue));
	}

	public void SetMessageHistory(List<string> messages)
	{
		foreach (var child in vBox.GetChildren())
		{
			child.QueueFree();
		}
		foreach (var message in messages)
		{
			AddMessage(message);
		}
	}
}

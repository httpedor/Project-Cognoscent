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
	public bool IsMouseIn => GetGlobalRect().HasPoint(GetGlobalMousePosition());

	public bool IsInputFocused => Input.HasFocus();
	private LineEdit Input;
	private VBoxContainer vBox;
	private ScrollContainer scrollContainer;
	public int MsgIndex = -1;

	public ChatControl()
	{
		Instance = this;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Input = GetNode<LineEdit>("Input");
		Input.TextSubmitted += text =>
		{
			if (text.Length > 0)
			{
				if (Input.Text.StartsWith('/'))
				{
					string command = Input.Text[1..];
					GameManager.Instance.ExecuteCommand(command);
					Input.Text = "";
					return;
				}
				var board = GameManager.Instance.CurrentBoard;
				board?.BroadcastMessage(text);
				Input.Text = "";
			}
		};
		scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
		vBox = scrollContainer.GetNode<VBoxContainer>("VBoxContainer");

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
		var rtl = new RichTextLabel
		{
			Text = message,
			FitContent = true,
			ScrollActive = false,
			CustomMinimumSize = new Vector2(Size.X, 0),
			BbcodeEnabled = true
		};
		rtl.MetaClicked += (Variant metaVar) =>
		{
			string meta = metaVar.AsString();
			GameManager.Instance.ExecuteCommand(meta);
		};
		vBox.AddChild(rtl);

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

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
		if (@event is InputEventKey keyEvent && IsInputFocused)
		{
			if (keyEvent.Pressed && keyEvent.Keycode == Key.Up)
			{
				AcceptEvent();
				MsgIndex = Math.Min(MsgIndex + 1, vBox.GetChildCount() - 1);
			}
			else if (keyEvent.Pressed && keyEvent.Keycode == Key.Down)
			{
				MsgIndex = Math.Max(MsgIndex - 1, -1);
			}
			Input.Text = vBox.GetChild(vBox.GetChildCount() - 1 - MsgIndex).Get("text").ToString();
		}
    }
}

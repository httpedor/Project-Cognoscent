using Godot;
using Rpg.Entities;
using System;
using System.Threading.Tasks;

public partial class BodyInspector : Control
{
	public class BodyInspectorSettings {
		public static readonly BodyInspectorSettings EMPTY = new BodyInspectorSettings();
		public static readonly BodyInspectorSettings HEALTH = new BodyInspectorSettings()
		{
			InText = (bp) => {
				return bp.Health + "/" + bp.MaxHealth;
			},
			CustomText = (bp) => {
				var txt =  "Feridas:\n";
				foreach (var injury in bp.Injuries)
				{
					txt += injury.Type.Translation + "(" + injury.Severity + ")\n";
				}
				return txt;
			}
		};
		public Action<BodyPart?>? OnPick = null;
		public Predicate<BodyPart>? Predicate = null;
		public Func<BodyPart, string>? CustomText = null;
		public Func<BodyPart, string>? InText = null;
		public bool CloseAfterSelected = true;

		public BodyInspectorSettings()
		{

		}
		public BodyInspectorSettings(BodyInspectorSettings clone)
		{
			OnPick = clone.OnPick;
			Predicate = clone.Predicate;
			CustomText = clone.CustomText;
			InText = clone.InText;
			CloseAfterSelected = clone.CloseAfterSelected;
		}
	}
	public event Action<Body?, Body?>? BodyChanged;
	public BodyInspectorSettings Settings
	{
		get;
		private set;
	} = BodyInspectorSettings.EMPTY;
	private Body? _body;
	public Body? Body
	{
		get
		{
			return _body;
		}
		private set
		{
			BodyChanged?.Invoke(_body, value);
			_body = value;
			humanoidBodySelector.Visible = false;
			if (Body != null)
			{
				switch (Body.Type)
				{
					case BodyType.Humanoid:
					{
						humanoidBodySelector.Visible = true;
						break;
					}
					default:
					{
						break;
					}
				}
			}
		}
	}
	private BodyPart? _current;
	public BodyPart? Current
	{
		get
		{
			return _current;
		}
		set
		{
			if (value != null && value.BodyInfo != Body)
				GD.PushWarning("BodyPart from other body selected on BPSelector screen");
			_current = value;
			foreach (var control in specificsContainer.GetChildren())
			{
				if (control is Button)
					control.QueueFree();
			}
			customLabel.Text = "";

			if (_current != null)
			{
				humanoidSelectedLbl.Text = BodyPart.Parts.Translate(_current.Name);
				if (Settings.CustomText != null)
					customLabel.Text = Settings.CustomText.Invoke(_current);
				foreach (var child in _current.Children)
				{
					if ((child.IsInternal || child.OverlapsParent) && (Settings.Predicate == null || Settings.Predicate(child)))
					{
						var btn = new Button()
						{
							Name = child.Name,
							Text = BodyPart.Parts.Translate(child.Name),
						};
						btn.Pressed += () => {
							Current = child;
						};
						specificsContainer.AddChild(btn);
					}
				}
				if (specificsContainer.GetChildren().Count == 1)
					specificsContainer.Visible = false;
				else
					specificsContainer.Visible = true;
			}
			else
			{
				humanoidSelectedLbl.Text = "Nenhuma Parte Selecionada";
			}
		}
	}
	private VBoxContainer specificsContainer;
	private Label customLabel;
	private Label humanoidSelectedLbl;
	private Button readyBtn;
	private TextureRect hideBtn;
	private Control humanoidBodySelector;
	private static BodyInspector _instance;
	public static BodyInspector Instance
	{
		get {
			return _instance;
		}
	}
	public BodyInspector()
	{
		_instance = this;
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		humanoidBodySelector = GetNode<Control>("HumanoidBody");
		humanoidSelectedLbl = humanoidBodySelector.GetNode<Label>("Label");
		specificsContainer = GetNode<VBoxContainer>("Specifics");
		customLabel = GetNode<Label>("Customs/Label");
		readyBtn = GetNode<Button>("Button");
		readyBtn.Pressed += () => {
			if (Settings.OnPick != null && Current != null)
			{
				Settings.OnPick(Current);
				if (Settings.CloseAfterSelected)
				{
					Settings.OnPick = null;
					Hide();
				}
			}
		};
		hideBtn = GetNode<TextureRect>("CloseBtn");
		hideBtn.GuiInput += (ev) => {
			if (ev is InputEventMouseButton iemb && iemb.Pressed)
				Hide();
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Show(Body body, BodyInspectorSettings settings)
	{
		Settings = settings;
		Body = body;
		Visible = true;
		

		if (settings.OnPick == null)
			readyBtn.Visible = false;
		else
			readyBtn.Visible = true;
	}

	public void Hide()
	{
		if (Settings.OnPick != null)
		{
			Settings.OnPick(null);
		}

		Visible = false;
		Body = null;
		Current = null;
	}

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
		if (@event is InputEventMouseButton iemb && iemb.Pressed)
		{
			AcceptEvent();
			Current = null;
		}
    }
}

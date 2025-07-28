using Godot;
using Rpg;
using System;
using System.Threading.Tasks;
using TTRpgClient.scripts.ui;

public partial class BodyInspector : Control
{
	public class BodyInspectorSettings {
		public static readonly BodyInspectorSettings EMPTY = new BodyInspectorSettings();
		public static readonly BodyInspectorSettings HEALTH = new BodyInspectorSettings
		{
			InText = bp => bp.Health + "/" + bp.MaxHealth,
			CustomText = bp => {
				string txt =  "Feridas:\n";
				foreach (var injury in bp.Injuries)
				{
					txt += injury.Type.Name + "(" + injury.Severity + ")\n";
				}
				return txt;
			},
			BackgroundColor = bp =>
			{
				double health = bp.Health;
				if (health <= 0)
					return Colors.DimGray;
				return Colors.Green.Lerp(Colors.Red, 1f - (float)(health / bp.MaxHealth));
			}
		};
		public Action<BodyPart?>? OnPick;
		public Predicate<BodyPart>? Predicate;
		public Func<BodyPart, string>? CustomText;
		public Func<BodyPart, string>? InText;
		public Func<BodyPart, Texture2D>? Icon;
		public Func<BodyPart, Color>? BackgroundColor;
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
			Icon = clone.Icon;
			BackgroundColor = clone.BackgroundColor;
		}
	}
	public event Action<Body?, Body?>? BodyChanged;
	public BodyInspectorSettings Settings
	{
		get;
		private set;
	} = BodyInspectorSettings.EMPTY;

	public Body? Body
	{
		get;
		private set
		{
			BodyChanged?.Invoke(field, value);
			field = value;
			humanoidBodySelector.Visible = false;
			if (Body == null) return;
			if (Body.IsHumanoid)
				humanoidBodySelector.Visible = true;
		}
	}

	public BodyPart? Current
	{
		get;
		set
		{
			if (value != null && value.Body != Body)
				GD.PushWarning("BodyPart from other body selected on BPSelector screen");
			field = value;
			foreach (var control in specificsContainer.GetChildren())
			{
				if (control is Button)
					control.QueueFree();
			}
			customLabel.Text = "";

			if (field != null)
			{
				selectedLbl.Text = field.Name;
				if (Settings.CustomText != null)
					customLabel.Text = Settings.CustomText.Invoke(field);
				foreach (var child in field.Children)
				{
					if ((child.IsInternal || child.OverlapsParent) && (Settings.Predicate == null || Settings.Predicate(child) || child.Children.Count > 0))
					{
						var btn = new Button
						{
							Name = child.Name,
							Text = child.Name,
						};
						btn.Pressed += () => {
							Current = child;

							readyBtn.Disabled = Settings.Predicate != null && Settings.Predicate(child);
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
				selectedLbl.Text = "Nenhuma Parte Selecionada";
			}
		}
	}

	private VBoxContainer specificsContainer;
	private Label customLabel;
	private Label selectedLbl;
	private Button readyBtn;
	private TextureRect hideBtn;
	private Control humanoidBodySelector;
	private static BodyInspector _instance;
	public static BodyInspector Instance => _instance;

	public BodyInspector()
	{
		_instance = this;
	}

	public void SelectCurrent()
	{
		if (Settings.OnPick == null || Current == null) return;
		Settings.OnPick(Current);
		if (!Settings.CloseAfterSelected) return;
		
		Settings.OnPick = null;
		Hide();
	}
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		humanoidBodySelector = GetNode<Control>("HumanoidBody");
		selectedLbl = GetNode<Label>("Label");
		specificsContainer = GetNode<VBoxContainer>("Specifics");
		customLabel = GetNode<Label>("Customs/Label");
		readyBtn = GetNode<Button>("Button");
		readyBtn.Pressed += SelectCurrent;
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

		if (body.IsHumanoid)
			humanoidBodySelector.Visible = true;
		else
			humanoidBodySelector.Visible = false;

		readyBtn.Visible = settings.OnPick != null;
		ActionBar.Hide();
	}

	public new void Hide()
	{
		if (Settings.OnPick != null)
		{
			Settings.OnPick(null);
		}

		Visible = false;
		Body = null;
		Current = null;
		ActionBar.Show();
	}

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
		if (@event is InputEventMouseButton { Pressed: true })
		{
			AcceptEvent();
			Current = null;
		}
    }
}

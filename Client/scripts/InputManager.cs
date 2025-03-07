using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Godot;
using Rpg;
using Rpg.Entities;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts;

public partial class InputManager : SubViewportContainer
{
	public static InputManager Instance
	{
		get;
		private set;
	}
	private GodotObject? toast;
	private Predicate<Entity>? entityPredicate = null;
	private Action<Entity?>? entityCallback = null;
	private Predicate<Vector3>? positionPredicate = null;
	private Action<Vector3?>? positionCallback = null;

	public Vector2 MousePosition
	{
		get;
		private set;
	}

	private Line2D? line;
	private ulong lastMovementTick;

	private static Node2D? InputPriority;
	public InputManager() : base()
	{
		Instance = this;
		Stretch = true;

		Name = "InputManager";

		SetAnchorsPreset(LayoutPreset.FullRect);
	}

	public static void RequestPriority(Node2D requester){
		InputPriority = requester;
	}
	public static void ReleasePriority(Node2D requester){
		if (InputPriority == requester)
			InputPriority = null;
	}
	public static bool HasPriority(Node2D? requester){
		return InputPriority == requester;
	}
	private void RemoveToast()
	{
		if (toast != null)
		{
			toast.Call("destroy");
			toast = null;
		}
	}
	public void RequestEntity(Action<Entity?> callback, Predicate<Entity>? predicate = null)
	{
		if (entityCallback != null)
		{
			CancelEntityRequest();
		}
		entityCallback = callback;
		entityPredicate = predicate;
		RemoveToast();
		toast = ToastParty.Show(new ToastParty.Config{
			Text = "Select an entity",
			Duration = -1
		});
	}
	public async Task<Entity?> RequestEntityAsync(Predicate<Entity>? predicate = null)
	{
		TaskCompletionSource<Entity?> result = new();
		RequestEntity((ent) => {
			result.SetResult(ent);
		}, predicate);
		return await result.Task;
	}
	public void RequestPosition(Action<Vector3?> callback, Predicate<Vector3>? predicate = null)
	{
		if (positionCallback != null)
			CancelPositionRequest();
		positionCallback = callback;
		positionPredicate = predicate;
		RemoveToast();
		toast = ToastParty.Show(new ToastParty.Config{
			Text = "Select a position",
			Duration = -1
		});
	}

	public async Task<Vector3?> RequestPositionAsync(Predicate<Vector3>? predicate = null)
	{
		TaskCompletionSource<Vector3?> result = new();
		RequestPosition((pos) => {
			result.SetResult(pos);
		}, predicate);
		return await result.Task;
	}

	public async Task<BodyPart?> RequestBodyPartAsync(Body body, Predicate<BodyPart>? predicate = null)
	{
		TaskCompletionSource<BodyPart?> result = new();
		BodyInspector.Instance.Show(body, new BodyInspector.BodyInspectorSettings()
		{
			CloseAfterSelected = true,
			Predicate = predicate,
			OnPick = (bp) => {
				result.SetResult(bp);
			}
		});

		return await result.Task;
	}

	private void SupplyEntity(Entity entity)
	{
		if (entityCallback != null)
		{
			entityCallback(entity);
			entityCallback = null;
			RemoveToast();
		}
	}
	public void CancelEntityRequest()
	{
		if (entityCallback != null)
		{
			entityCallback(null);
			entityCallback = null;
			entityPredicate = null;
			RemoveToast();
		}
	}
	public void CancelPositionRequest()
	{
		if (positionCallback != null)
		{
			positionCallback(null);
			positionCallback = null;
			positionPredicate = null;
			RemoveToast();
		}
	}
	private void SupplyPosition(Vector3 position)
	{
		if (positionCallback != null)
		{
			positionCallback(position);
			positionCallback = null;
			RemoveToast();
		}
	}

	private void CancelRequests()
	{
		CancelPositionRequest();
		CancelEntityRequest();
	}

	private void ExecuteContextMenuAction(Creature creature, Rpg.Skill action, Vector2 pos)
	{
		//NetworkManager.Instance.SendPacket(new CreatureActionExecutePacket(action, creature));
		RadialMenu.Instance.Hide();
	}

	private void AddActionsToRadialMenu(Creature creature)
	{
		foreach (var pair in creature.AvailableSkills)
		{
			var source = pair.Item1;
			var action = pair.Item2;

			RadialMenu.Instance.AddOption(new RadialMenuOption(
				Icons.GetIcon(action.GetIconName()),
				action.GetName(),
				action.GetDescription(),
				(pos) => {
					ExecuteContextMenuAction(creature, action, pos);
				}
			));
		}
	}

	private void HandleContextMenu(ClientBoard board)
	{
		if (entityCallback != null || positionCallback != null)
			return;
		var hovering = InputPriority as EntityNode;
		if (hovering != null)
		{
			if (GameManager.IsGm)
			{
				ContextMenu.AddOption("Destruir Entidade", (_) => {
					if (!Input.IsKeyPressed(Key.Shift))
						Modal.OpenConfirmationDialog("Deletar Entidade", "Deseja deletar/remover essa entidade?", (remove) => {
							if (remove)
								NetworkManager.Instance.SendPacket(new EntityRemovePacket(hovering.Entity));
						}, "Sim", "Não");
					else
						NetworkManager.Instance.SendPacket(new EntityRemovePacket(hovering.Entity));
					ContextMenu.Hide();
				});
				if (hovering.Entity is Creature hoveringCreature)
				{
					ContextMenu.AddOption("Renomear", (_) => {
						Modal.OpenStringDialog("Renomear Entidade", (name) => {
							if (name == null)
								return;
							
							hoveringCreature.Name = name;
						}, true);
					});

					ContextMenu.AddSeparator();
					ContextMenu.AddOption("Danificar Parte", (_) => {
						BodyInspector.Instance.Show(hoveringCreature.Body,
							new BodyInspector.BodyInspectorSettings(BodyInspector.BodyInspectorSettings.HEALTH)
							{
								OnPick = (bp) => {
									Modal.OpenOptionsDialog("Tipo de Ferida", "Selecione o tipo de ferida que deseja aplicar", InjuryType.GetInjuryTypes().Select((i) => i.Translation).ToArray(), (typeTranslation) => {
										if (typeTranslation == null)
											return;
										InjuryType type = InjuryType.GetTypeByTranslation(typeTranslation);
										Modal.OpenStringDialog("Severidade da Ferida", (sevStr) => {
											float severity;
											if (Single.TryParse(sevStr, out severity))
											{
												NetworkManager.Instance.SendPacket(new EntityBodyPartInjuryPacket(bp, new Injury(type, severity)));
											}
										});
									});
								}
							}
						);
						ContextMenu.Hide();
					});
					ContextMenu.AddOption("Curar Ferida", (_) => {
						BodyInspector.Instance.Show(hoveringCreature.Body, new BodyInspector.BodyInspectorSettings(BodyInspector.BodyInspectorSettings.HEALTH)
						{
							OnPick = (bp) => {
								Modal.OpenOptionsDialog("Ferida", "Selecione a ferida que deseja curar", bp.Injuries.Select((inj) => {return inj.Type.Translation + " - " + inj.Severity;}).ToArray(), (selected) => {
									if (selected == null)
										return;
									var splitted = selected.Split(" - ");
									InjuryType it = InjuryType.GetTypeByTranslation(splitted[0]);
									float severity;
									if (Single.TryParse(splitted[1], out severity))
										NetworkManager.Instance.SendPacket(new EntityBodyPartInjuryPacket(bp, new Injury(it, severity), true));
								});
							}
						});
						ContextMenu.Hide();
					});
				}
			}
			ContextMenu.AddSeparator();
		}
		
		if (board.SelectedEntity != null && (GameManager.IsGm || (board.SelectedEntity is Creature creature && creature.Owner == GameManager.Instance.Username)))
		{
			var entity = board.SelectedEntity;

			ContextMenu.AddOption("Olhar Aqui", (pos) => {
				var worldPos = board.PixelToWorld(pos);
				var rotation = (worldPos - entity.Position.ToGodot().ToV2()).Angle();
				NetworkManager.Instance.SendPacket(new EntityRotationPacket(entity, rotation));
				ContextMenu.Hide();
			});

			if (GameManager.IsGm)
			{
				ContextMenu.AddOption("Teleportar Aqui", (pos) => {
					var worldPos = board.CurrentFloor.PixelToTileCenter(pos);
					NetworkManager.Instance.SendPacket(new EntityPositionPacket(entity, new System.Numerics.Vector3(worldPos.ToNumerics(), entity.FloorIndex)));
					ContextMenu.Hide();
				});
				ContextMenu.AddOption("Teleportar Exatamente Aqui", (pos) => {
					var worldPos = board.CurrentFloor.PixelToWorld(pos);
					NetworkManager.Instance.SendPacket(new EntityPositionPacket(entity, new System.Numerics.Vector3(worldPos.ToNumerics(), entity.FloorIndex)));
					ContextMenu.Hide();
				});
			}
		}


		ContextMenu.Show();
	}
	private void HandleMovement(ClientBoard board, Creature? creature)
	{
		var move_dir = new Vector2();
		if (Input.IsActionPressed("move_n"))
			move_dir.Y -= 1;
		if (Input.IsActionPressed("move_s"))
			move_dir.Y += 1;
		if (Input.IsActionPressed("move_w"))
			move_dir.X -= 1;
		if (Input.IsActionPressed("move_e"))
			move_dir.X += 1;
		if (Input.IsActionPressed("move_nw"))
			move_dir += new Vector2(-1, -1);
		if (Input.IsActionPressed("move_ne"))
			move_dir += new Vector2(1, -1);
		if (Input.IsActionPressed("move_sw"))
			move_dir += new Vector2(-1, 1);
		if (Input.IsActionPressed("move_se"))
			move_dir += new Vector2(1, 1);

		
		if (move_dir.X != 0 || move_dir.Y != 0){
			if (creature != null)
			{
				if (Time.GetTicksMsec() - lastMovementTick < 100)
					return;
				lastMovementTick = Time.GetTicksMsec();

				var targetPos = creature.Position.ToGodot().ToV2() + (move_dir * 0.5f);
				NetworkManager.Instance.SendPacket(new EntityMovePacket(creature, targetPos.ToNumerics()));
			}
			else
			{
				var cam = GridCamera.Instance;
				cam.Position += (move_dir * 2) / cam.Zoom;
			}
		}
	}
	private void HandleRequests(ClientBoard board)
	{
		if (line == null && board.SelectedEntity != null && positionCallback != null)
		{
			line = new Line2D()
			{
				Material = new CanvasItemMaterial()
				{
					LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
				},
				ZIndex = board.FloorIndex * 100 + 20
			};
			GameManager.Instance.UINode.AddChild(line);
		}
		else if (line != null && (board.SelectedEntity == null || positionCallback == null))
		{
			line.QueueFree();
			line = null;
		}

		if (line != null)
		{
			var start = board.WorldToPixel(board.SelectedEntity.Position.ToGodot().ToV2());
			var end = MousePosition;
			line.Points = new Vector2[] { start, end };
		}
	}
	public override void _Process(double delta)
	{
		base._Process(delta);
		
		if (Input.IsActionJustPressed("debug"))
		{
			GD.Print("Toggling BodyViewer");
			((Control)BodyViewer.Instance.GetParent()).Visible = !((Control)BodyViewer.Instance.GetParent()).Visible;
		}

		ClientBoard? board = GameManager.Instance.CurrentBoard;
		if (board == null)
			return;

  
		HandleRequests(board);

		if (ChatControl.Instance.IsInputFocused)
			return;

		if ((Input.IsActionJustPressed("radial_menu") || Input.IsActionJustPressed("ui_cancel")) && RadialMenu.Instance.IsOpen){
			RadialMenu.Instance.Hide();
			return;
		}

		if (Input.IsActionJustPressed("context_menu") && ContextMenu.IsOpen){
			ContextMenu.Hide();
		}

		if (Input.IsActionJustPressed("ui_cancel")){
			if (entityCallback != null || positionCallback != null)
			{
				CancelRequests();
				return;
			}
			if (BodyInspector.Instance.Visible)
			{
				BodyInspector.Instance.Hide();
				return;
			}
			NetworkManager.Instance.Disconnect();
		}

		if (Input.IsActionJustPressed("context_menu"))
		{
			HandleContextMenu(board);
		}

		if (board.SelectedEntity != null && board.SelectedEntity is Creature creature && (creature.Owner.Equals(GameManager.Instance.Username) || GameManager.IsGm)){
			if (Input.IsActionJustPressed("radial_menu"))
			{
				AddActionsToRadialMenu(creature);
				RadialMenu.Instance.Show();
			}

			if (!board.CombatMode)
				HandleMovement(board, creature);
		}

		if (board.SelectedEntity == null)
		{
			HandleMovement(board, null);
		}
  
		if (Input.IsActionJustPressed("camera_up"))
			board.FloorIndex++;
		if (Input.IsActionJustPressed("camera_down"))
			board.FloorIndex = Math.Max(0, board.FloorIndex - 1);
	}

    public override void _GuiInput(InputEvent @event)
    {
		base._GuiInput(@event);

		var board = GameManager.Instance.CurrentBoard;
		if (board == null)
			return;
		if (@event is InputEventMouse iem)
			MousePosition = (iem.Position - board.Node.GetViewport().CanvasTransform.Origin) / board.Node.GetViewport().GetCamera2D().Zoom;

		if (RadialMenu.Instance.IsOpen)
			return;

		// Mouse is over something
		if (InputPriority != null)
		{
			// That thing is an entity, and the event is a left click
			if (@event is InputEventMouseButton iemb2 && iemb2.Pressed && iemb2.ButtonIndex == MouseButton.Left && InputPriority is EntityNode entityNode)
			{
				if (entityCallback != null)
				{
					if (entityPredicate == null || entityPredicate(entityNode.Entity))
						SupplyEntity(entityNode.Entity);
					AcceptEvent();
					return;
				}

				entityNode.OnClick(iemb2);
				AcceptEvent();
			}
			return;
		}

		if (@event is InputEventMouseButton iemb){
			var pos = board.CurrentFloor.PixelToWorld(MousePosition);
			if (iemb.ButtonIndex == MouseButton.Left && iemb.Pressed){
				if (positionCallback != null && (positionPredicate == null || positionPredicate(new Vector3(pos.X, pos.Y, board.FloorIndex))))
					SupplyPosition(new Vector3(pos.X, pos.Y, board.FloorIndex));
				else
					board.SelectedEntity = null;
				AcceptEvent();
			}
		}
    }
}

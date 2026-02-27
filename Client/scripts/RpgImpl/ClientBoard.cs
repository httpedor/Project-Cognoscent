using System;
using System.Collections.Generic;
using Godot;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts.RpgImpl;

public class ClientBoard : Board
{
	private readonly Dictionary<int, EntityRenderer> entityRenderers = new();
	private readonly HashSet<int> localEntityIds = new();
    public Node2D Node {get; }

    public Entity? SelectedEntity
    {
	    get;
	    set
	    {
		    if (field != null){
				if (field.HasComponent(Token.ID))
					GetTokenRenderer(field.Token!)?.Outline = new Color(1, 0, 0, 0);
			    if (GameManager.IsGm)
			    {
				    GameManager.Instance.VisionManager.RemoveVisionPoint(field.Id.ToString());
				    ActionBar.Clear();
			    }
		    }
		    field = value;
		    if (field != null){
				var token = field.Token;
				if (token == null)
					return;

			    if (token.FloorIndex < 0 || token.FloorIndex >= GetFloorCount())
				    return;
			    GetTokenRenderer(token).Outline = new Color(1, 0, 0, 1f);
				if (GameManager.IsGm)
					GameManager.Instance.VisionManager.AddVisionPoint(new VisionPoint(token));
				if (GameManager.IsGm || field.Owner == GameManager.Username)
				{
					ActionBar.Clear();
					if (field.HasComponent(SkillExecutor.ID))
						ActionBar.PopulateWithSkills(field.SkillExecutor!);
				}
		    }

		    if (GameManager.Instance.VisionManager.VisionPointCount > 0)
		    {
			    CurrentFloor.UpdateAmbientModulate();
		    }
		    else
		    {
			    if (CurrentFloor.AmbientLightColor.Luminance < .2 && GameManager.IsGm)
				    CurrentFloor.AmbientLightModulate.Color = Color.Color8(50, 50, 50, 255);
		    }
	    }
    }

    public Entity? OwnedSelectedEntity
	{
		get
		{
			if ((SelectedEntity?.Owner?.Equals(GameManager.Username) ?? false) || GameManager.IsGm)
				return SelectedEntity;
			return null;
		}
	}

	public Camera2D Camera{
		get;
	}
	private Node floorsNode;
	private Node2D gridNode;
	public bool GridEnabled{
		get => gridNode.Visible;
		set => gridNode.Visible = value;
	}

	public int FloorIndex
	{
		get;
		set
		{
			if (floorsNode.GetChildCount() == 0)
				return;
			value = Mathf.Clamp(value, 0, floorsNode.GetChildCount()-1);
			field = value;
			ClientFloor? floor = GetFloor(value);
			if (floor != null)
			{
				gridNode.QueueRedraw();
				if (VisionManager.Instance.VisionPointCount > 0)
					floor.UpdateAmbientModulate();
				else if (GameManager.IsGm && floor.AmbientLightColor.Luminance < .2)
					floor.AmbientLightModulate.Color = Color.Color8(50, 50, 50, 255);
			}
			else
				GridEnabled = false;

			for (int i = 0; i < GetFloorCount(); i++){
				ClientFloor f = GetFloor(i);
				Node2D node = f.Node;
				node.Visible = i <= value;
				if (!node.Visible) continue;
				f.SetOcclusion(i == value);
				f.SetCollision(i == value);
				node.Modulate = node.Modulate with {A = 1/Mathf.Pow(2, value-i)};
			}
		}
	} = 0;

	public ClientFloor CurrentFloor => GetFloor(FloorIndex)!;
	private GodotObject? turnModeToast;

    public ClientBoard()
    {
        Node = new Node2D();
		Node.SetMeta("Board", Name);

        gridNode = new GridLines(this)
        {
            Name = "Grid",
            ZIndex = 10,
			Visible = false
        };
        Node.AddChild(gridNode);

        floorsNode = new Node2D
        {
            Name = "Floors"
        };
        Node.AddChild(floorsNode);

		Camera = new GridCamera();
		Node.AddChild(Camera);
    }

    public new ClientFloor? GetFloor(int index){
        return base.GetFloor(index) as ClientFloor;
    }

	public Vector2 WorldToPixel(Vector2 world){
		return CurrentFloor.WorldToPixel(world);
	}
	public System.Numerics.Vector2 PixelToWorld(System.Numerics.Vector2 pixel){
		return CurrentFloor.PixelToWorld(pixel);
	}

	public Vector2 PixelToWorld(Vector2 pixel){
		return CurrentFloor.PixelToWorld(pixel);
	}
	public System.Numerics.Vector2 WorldToPixel(System.Numerics.Vector2 world){
		return CurrentFloor.WorldToPixel(world);
	}

	public void UpdateTurnModeToast()
	{
		if (TurnMode && turnModeToast == null)
		{
			turnModeToast = ToastParty.Show(new ToastParty.Config
			{
				Text = "Modo de Turno",
				Duration = -1
			});
		}

		if (!TurnMode && turnModeToast != null)
		{
			turnModeToast.Call("destroy");
			turnModeToast = null;
		}
	}
	
	public override void StartTurnMode()
	{
		base.StartTurnMode();
		InitiativeBar.Show();
		InitiativeBar.PopulateWithBoard(this);
		UpdateTurnModeToast();
	}

	public override void EndTurnMode()
	{
		base.EndTurnMode();
		InitiativeBar.Hide();
		UpdateTurnModeToast();
	}

	public override List<Entity> GetEntitiesByOwner(string owner)
	{
		if (!owner.Equals(GameManager.Username)) return base.GetEntitiesByOwner(owner);
		
		var ret = new List<Entity>();
		foreach (int id in localEntityIds)
		{
			Entity? entity = GetEntityById(id);
			if (entity != null)
				ret.Add(entity);
		}
		return ret;
	}

    public override void SetFloor(int index, Floor? toSet)
    {
        base.SetFloor(index, toSet);

		if (toSet == null){
			floorsNode.GetChild(index).QueueFree();
			return;
		}

        if (toSet is not ClientFloor floor)
        {
            throw new Exception("Invalid floor type. Somehow a non-ClientFloor was added to a ClientBoard.");
        }
        floor.Node.ZIndex = index * 100;
		if (index >= floorsNode.GetChildCount())
		{
			floorsNode.AddChild(floor.Node);
		}
		else{
			floorsNode.GetChildren()[index].QueueFree();
			
			floorsNode.AddChild(floor.Node);
			floorsNode.MoveChild(floor.Node, index);
		}
		floor.Node.Name = index.ToString();
		
		if (GetFloorCount() == 1)
			FloorIndex = 0;
    }

    public override void AddEntity(Entity entity, bool initialize = true)
    {
        base.AddEntity(entity, initialize);

		EntityRenderer node;
		node = new EntityRenderer(entity, this);

		entityRenderers[entity.Id] = node;
		if (entity.HasComponent(SkillExecutor.ID) && TurnMode)
			InitiativeBar.PopulateWithBoard(this);
		if (!GameManager.IsGm)
		{
			if (entity.Owner?.Equals(GameManager.Username) ?? false)
			{
				localEntityIds.Add(entity.Id);
				if (entity.HasComponent(Token.ID))
				{
					VisionManager.Instance.ClearVisionPoints();
					foreach (var entId in localEntityIds)
					{
						Entity? ent = GetEntityById(entId);
						if (ent != null && ent.HasComponent(Token.ID))
							VisionManager.Instance.AddVisionPoint(new VisionPoint(ent.Token!));
					}
				}
			}

			if (localEntityIds.Count == 0 && entity.HasOwner && entity.HasComponent(Token.ID))
				VisionManager.Instance.AddVisionPoint(new VisionPoint(entity.Token!));
		}
    }

	public override void RemoveEntity(Entity? entity)
	{
		if (entity == null)
			return;
		base.RemoveEntity(entity);
		if (localEntityIds.Contains(entity.Id))
			localEntityIds.Remove(entity.Id);
		if (SelectedEntity == entity)
			SelectedEntity = null;

		var token = entity.Token;
		if (token == null)
			return;
		GetTokenRenderer(token).QueueFree();

		GameManager.Instance.VisionManager.RemoveVisionPoint(token);

	}

	public EntityRenderer? GetEntityRenderer(Entity entity){
		return entityRenderers.TryGetValue(entity.Id, out var node) ? node : null;
	}
	public EntityRenderer? GetEntityRenderer(Component component)
	{
		return GetEntityRenderer(component.Entity);
	}
	public ComponentRendererBase? GetComponentRenderer(Entity entity, uint componentId)
	{
		var entityRenderer = GetEntityRenderer(entity);
		return entityRenderer?.GetComponentRenderer(componentId);
	}
	public ComponentRendererBase? GetComponentRenderer(Component component)
	{
		var entityRenderer = GetEntityRenderer(component.Entity);
		return entityRenderer?.GetComponentRenderer(component.GetId());
	}
	public ComponentRenderer<T>? GetComponentRenderer<T>(Entity entity) where T : Component
	{
		var entityRenderer = GetEntityRenderer(entity);
		var compRenderer = entityRenderer?.GetComponentRenderer(Component.GetComponentId<T>());
		if (compRenderer == null)
			return null;
		return (ComponentRenderer<T>?)compRenderer;
	}
	public ComponentRenderer<T>? GetComponentRenderer<T>(T component) where T : Component
	{
		var entityRenderer = GetEntityRenderer(component.Entity);
		var compRenderer = entityRenderer?.GetComponentRenderer(component.GetId());
		if (compRenderer == null)
			return null;
		return (ComponentRenderer<T>?)compRenderer;
	}
	public TokenRenderer GetTokenRenderer(Token token)
	{
		return GetEntityRenderer(token.Entity).GetComponentRenderer<TokenRenderer>(Token.ID);
	}

    public override void BroadcastMessage(string message)
    {
		NetworkManager.Instance.SendPacket(new ChatPacket(this, message));
    }

	public void CenterOn(Token token)
	{
		Tween tween = Node.GetTree().CreateTween();
		tween.SetParallel(true);
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenProperty(Camera, "zoom", new Vector2(2, 2), 0.5f);
		tween.TweenProperty(Camera, "position", WorldToPixel(token.Position.ToV2()).ToGodot(), 0.5f);

		tween.Finished += () => {
			tween.Kill();
			SelectedEntity = token.Entity;
		};
	}

    public override void ComponentHandledEvent(Component comp, ComponentEvent e)
    {
        base.ComponentHandledEvent(comp, e);
		GetComponentRenderer(comp)?.EventHandled(e);
    }

    public override void HandleEvent(ComponentEvent e)
    {
        base.HandleEvent(e);
		GetComponentRenderer(e.Component)?.EventFired(e);
    }

    public override void HandleEvent(EntityEvent e)
    {
        base.HandleEvent(e);

		GetEntityRenderer(e.Entity)?.OnReady();
    }

}

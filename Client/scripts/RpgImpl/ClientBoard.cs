using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Rpg;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts.RpgImpl;

public class ClientBoard : Board
{
	private static Dictionary<EntityType, Func<Entity, ClientBoard, EntityNode>> nodeConstructors = new()
	{
		{EntityType.Door, (ent, board) => new DoorNode((DoorEntity)ent, board)},
		{EntityType.Light, (ent, board) => new LightNode((LightEntity)ent, board)},
		{EntityType.Prop, (ent, board) => new PropNode((PropEntity)ent, board)},
		{EntityType.Creature, (ent, board) => new CreatureNode((Creature)ent, board)},
		{EntityType.Item, (ent, board) => new ItemNode((ItemEntity)ent, board)}
	};
	private readonly Dictionary<int, EntityNode> entityNodesCache = new();
	private readonly List<int> localEntityIds = new();
    public Node2D Node {get; }

    public Entity? SelectedEntity
    {
	    get;
	    set
	    {
		    if (field != null){
			    GetEntityNode(field).Outline = new Color(1, 0, 0, 0);
			    if (GameManager.IsGm)
			    {
				    GameManager.Instance.VisionManager.RemoveVisionPoint(field.Id.ToString());
				    ActionBar.Clear();
			    }
		    }
		    field = value;
		    if (field != null){
			    if (field.FloorIndex < 0 || field.FloorIndex >= GetFloorCount())
				    return;
			    GetEntityNode(field).Outline = new Color(1, 0, 0, 1f);
			    if (field is Creature selectedCreature)
			    {
				    if (GameManager.IsGm)
					    GameManager.Instance.VisionManager.AddVisionPoint(new VisionPoint(selectedCreature));
				    if (GameManager.IsGm || selectedCreature.Owner == GameManager.Instance.Username)
				    {
					    ActionBar.Clear();
					    ActionBar.PopulateWithSkills(selectedCreature);
				    }
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

    public Creature? OwnedSelectedEntity
	{
		get
		{
			if (SelectedEntity is Creature c && (c.Owner.Equals(GameManager.Instance.Username) || GameManager.IsGm))
				return c;
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

	public override List<Creature> GetCreaturesByOwner(string owner)
	{
		if (!owner.Equals(GameManager.Instance.Username)) return base.GetCreaturesByOwner(owner);
		
		var ret = new List<Creature>();
		foreach (int id in localEntityIds)
		{
			Creature? entity = (Creature?)GetEntityById(id);
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

    public override void AddEntity(Entity entity)
    {
        base.AddEntity(entity);
		EntityNode node;
		if (nodeConstructors.ContainsKey(entity.GetEntityType()))
			node = nodeConstructors[entity.GetEntityType()](entity, this);
		else
			node = new EntityNode(entity, this);

		entityNodesCache[entity.Id] = node;
		GetFloor(entity.FloorIndex)?.EntitiesNode.AddChild(node);
		if (entity is Creature creature)
		{
			creature.ActionLayerChanged += (layer) =>
			{
				if (TurnMode)
					InitiativeBar.PopulateWithBoard(this);
			};
			creature.ActionLayerRemoved += (layer) =>
			{
				if (TurnMode)
					InitiativeBar.PopulateWithBoard(this);
			};
			if (TurnMode)
				InitiativeBar.PopulateWithBoard(this);
			if (!GameManager.IsGm)
			{
				if (creature.Owner.Equals(GameManager.Instance.Username))
				{
					foreach (var e in entities)
					{
						if (e is Creature c && c.Owner.Equals(GameManager.Instance.Username))
							GameManager.Instance.VisionManager.RemoveVisionPoint(creature);
					}
					VisionManager.Instance.AddVisionPoint(new VisionPoint(creature));
					localEntityIds.Add(entity.Id);
				}

				if (localEntityIds.Count == 0 && creature.HasOwner())
					VisionManager.Instance.AddVisionPoint(new VisionPoint(creature));
			}
		}
    }

	public override void RemoveEntity(Entity? entity)
	{
		if (entity == null)
			return;
		base.RemoveEntity(entity);
		GetEntityNode(entity).QueueFree();

		if (localEntityIds.Contains(entity.Id))
			localEntityIds.Remove(entity.Id);

		GameManager.Instance.VisionManager.RemoveVisionPoint(entity);

		if (SelectedEntity == entity)
			SelectedEntity = null;
	}

	public EntityNode GetEntityNode(Entity entity){
		return entityNodesCache[entity.Id];
	}

    public override void BroadcastMessage(string message)
    {
		NetworkManager.Instance.SendPacket(new ChatPacket(this, message));
    }

	public void CenterOn(Entity entity)
	{
		Tween tween = Node.GetTree().CreateTween();
		tween.SetParallel(true);
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenProperty(Camera, "zoom", new Vector2(2, 2), 0.5f);
		tween.TweenProperty(Camera, "position", WorldToPixel(entity.Position.ToV2()).ToGodot(), 0.5f);

		tween.Finished += () => {
			tween.Kill();
			SelectedEntity = entity;
		};
	}
}

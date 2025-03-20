using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Rpg;
using Rpg.Entities;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts.RpgImpl;

public class ClientBoard : Board
{
	private static Dictionary<EntityType, Func<Entity, ClientBoard, EntityNode>> nodeConstructors = new()
	{
		{EntityType.Door, (ent, board) => new DoorNode((Door)ent, board)},
		{EntityType.Light, (ent, board) => new LightNode((LightEntity)ent, board)},
		{EntityType.Prop, (ent, board) => new PropNode((PropEntity)ent, board)}
	};
	private Dictionary<int, EntityNode> entityNodesCache = new Dictionary<int, EntityNode>();
	private List<Int32> localEntityIds = new List<Int32>();
    public Node2D Node {get; protected set;}

	private Entity? _selectedEntity;
	public Entity? SelectedEntity{
		get{
			return _selectedEntity;
		}
		set{
			var owned = GetCreaturesByOwner(GameManager.Instance.Username);
			if (_selectedEntity != null){
				GetEntityNode(_selectedEntity).Outline = new Color(1, 0, 0, 0);
				if (GameManager.IsGm)
				{
					GameManager.Instance.VisionManager.RemoveVisionPoint(_selectedEntity.Id.ToString());
					ActionBar.Clear();
				}
			}
			_selectedEntity = value;
			if (_selectedEntity != null){
				if (_selectedEntity.FloorIndex < 0 || _selectedEntity.FloorIndex >= GetFloorCount())
					return;
				GetEntityNode(_selectedEntity).Outline = new Color(1, 0, 0, 1f);
				if (_selectedEntity is Creature selectedCreature)
				{
					if (GameManager.IsGm )
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

	public Camera2D Camera{
		get;
		protected set;
	}
	private Node floorsNode;
    private int floorIndex = 0;
	private Node2D gridNode;
	public bool GridEnabled{
		get{
			return gridNode.Visible;
		}
		set{
			gridNode.Visible = value;
		}
	}
	public int FloorIndex{
		get{
			return floorIndex;
		}
		set{
			if (floorsNode.GetChildCount() == 0)
				return;
			value = Mathf.Clamp(value, 0, floorsNode.GetChildCount()-1);
			floorIndex = value;
			var floor = GetFloor(value);
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
				if (node.Visible){
					f.SetOcclusion(i == value);
					f.SetCollision(i == value);
					node.Modulate = node.Modulate with {A = 1/Mathf.Pow(2, value-i)};
				}
			}
		}
	}
	public ClientFloor CurrentFloor => GetFloor(FloorIndex);

    public ClientBoard() : base(){
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

    public new ClientFloor GetFloor(int index){
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

	public override List<Creature> GetCreaturesByOwner(string owner)
	{
		if (owner.Equals(GameManager.Instance.Username))
		{
			List<Creature> ret = new List<Creature>();
			foreach (var id in localEntityIds)
			{
				Creature? entity = (Creature?)GetEntityById(id);
				if (entity != null)
					ret.Add(entity);
			}
			return ret;
		}
		return base.GetCreaturesByOwner(owner);
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
		if (index >= floorsNode.GetChildCount()){
			if (floor != null)
				floorsNode.AddChild(floor.Node);
		}
		else{
			floorsNode.GetChildren()[index].QueueFree();
			if (floor != null){
				floorsNode.AddChild(floor.Node);
				floorsNode.MoveChild(floor.Node, index);
			}
		}
		if (floor != null)
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
		GetFloor(entity.FloorIndex).EntitiesNode.AddChild(node);

		if (!GameManager.IsGm && entity is Creature creature){
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

	public override void RemoveEntity(Entity? entity)
	{
		if (entity == null)
			return;
		base.RemoveEntity(entity);
		if (entity is Door d)
		{
			//TODO: Remove door occluder
		}
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

	public void GrabEntity(Action<Entity> action, Predicate<Entity> predicate = null)
	{

	}
}

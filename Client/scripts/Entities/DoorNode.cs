using Godot;
using Rpg;
using System;
using System.IO;
using System.Linq;
using TTRpgClient.scripts;
using TTRpgClient.scripts.RpgImpl;

public partial class DoorNode : EntityNode
{
	private Vector2 A;
	private Vector2 B;
	private Line2D Line;
	public readonly DoorEntity Door;
	private RectangleShape2D shape;
	private LightOccluder2D occluder;

	public DoorNode(DoorEntity door, ClientBoard board) : base(door, board)
	{
		Door = door;
		shape = new RectangleShape2D
		{
			Size = new Vector2((door.Bounds[0] - door.Bounds[1]).Length() * door.Floor.TileSize.X, door.Floor.TileSize.Y/6)
		};
		Hitbox.GetChild<CollisionShape2D>(0).Shape = shape;
		occluder = new LightOccluder2D
		{
			OccluderLightMask = 3,
		};
		AddChild(occluder);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Line = new Line2D
		{
			Visible = false,
			Width = Door.Floor.TileSize.X/8
		};
		occluder.Occluder = new OccluderPolygon2D
		{
			Polygon = Door.Bounds.Select(p => (p * Door.Floor.TileSize).ToGodot()).ToArray(),
			Closed = true
		};
		Door.Floor.OnMidiaChanged += newMidia =>
		{
			if (newMidia.Type != MidiaType.Image)
				return;
			
			using (var img = System.Drawing.Image.FromStream(new MemoryStream(newMidia.Bytes)))
			{
				var bitmap = new System.Drawing.Bitmap(img);
				int imgX = (int)(Door.Position.X * Door.Floor.TileSize.X);
				int imgY = (int)(Door.Position.Y * Door.Floor.TileSize.Y);
				var c = bitmap.GetPixel(imgX, imgY);
				Line.DefaultColor = new Color(c.R/255f, c.G/255f, c.B/255f, c.A/255f);
				AddChild(Line);
			}
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		base._Process(delta);
		Rotation = 0;

		var bound2 = Door.Closed ? Door.Bounds[1].ToGodot() : Door.OpenBound2.ToGodot();
		Line.Visible = !Door.Closed;
		A = Door.Bounds[0].ToGodot();
		B = B.Lerp(bound2, (float)delta*10);

		Line.GlobalPosition = Vector2.Zero;
		Line.Points = new[]{A * Door.Floor.TileSize.X, B * Door.Floor.TileSize.X};

		Hitbox.GlobalPosition = ((A + B)/2) * Door.Floor.TileSize.X;
		Hitbox.Rotation = Mathf.Atan2(B.Y - A.Y, B.X - A.X);
		shape.Size = new Vector2((A - B).Length() * Door.Floor.TileSize.X, Door.Floor.TileSize.Y/8);

		if (!Door.BlocksVision)
		{
			occluder.Visible = false;
			return;
		}
		else
			occluder.Visible = true;
		occluder.GlobalPosition = Vector2.Zero;
		occluder.Occluder.Polygon = new Godot.Vector2[2]{Door.Bounds[0].ToGodot() * Door.Floor.TileSize.ToGodot(), occluder.Occluder.Polygon[1].Lerp(bound2 * Door.Floor.TileSize.ToGodot(), (float)(delta * 10))};

	}

    public override void OnClick()
    {
		if (GameManager.Instance.CurrentBoard != null
		&& GameManager.Instance.CurrentBoard.SelectedEntity is Creature c && (c.Owner == GameManager.Instance.Username || GameManager.IsGm)
		&& Door.CanBeOpenedBy(c))
			NetworkManager.Instance.SendPacket(new DoorInteractPacket(Door));
    }

    public override void AddGMContextMenuOptions()
    {
        base.AddGMContextMenuOptions();
		var door = Door;
		void updateDoor()
		{
			NetworkManager.Instance.SendPacket(new DoorUpdatePacket(door));
		}
		ContextMenu.AddOption(door.Closed ? "Abrir Forçado" : "Fechar Forçado", (_) => {
			door.Closed = !door.Closed;
			updateDoor();
			ContextMenu.Hide();
		});
		ContextMenu.AddOption(door.Slide ? "Transformar em Pivotante" : "Transformar em Deslizante", (_) => {
			door.Slide = !door.Slide;
			updateDoor();
		});
		ContextMenu.AddOption(door.Locked ? "Destrancar Forçado" : "Trancar Forçado", (_) => {
			door.Locked = !door.Locked;
			updateDoor();
		});
    }

    public override void AddContextMenuOptions()
    {
		base.AddContextMenuOptions();

		var selected = GameManager.Instance.CurrentBoard.SelectedEntity;
		if (selected != null && selected is Creature creature && (GameManager.IsGm || creature.Owner == GameManager.Instance.Username))
		{
			if (Door.CanBeOpenedBy(creature))
			{
				ContextMenu.AddOption(Door.Closed ? "Abrir" : "Fechar", (_) => {
					NetworkManager.Instance.SendPacket(new DoorInteractPacket(Door));
					ContextMenu.Hide();
				});
				ContextMenu.AddSeparator();
			}
		}
    }
}

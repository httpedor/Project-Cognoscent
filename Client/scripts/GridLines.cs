using Godot;
using Rpg;
using System;
using System.Collections.Generic;
using TTRpgClient.scripts;
using TTRpgClient.scripts.RpgImpl;

public partial class GridLines : Area2D
{
	ClientBoard board;
	CollisionShape2D collision;
	public GridLines(ClientBoard board){
		this.board = board;
		collision = new CollisionShape2D()
		{
			DebugColor = new Color(0.2f, 0.2f, 0.2f, 0.2f)
		};
		Monitorable = false;
		Monitoring = false;
		CollisionLayer = 3;
		CollisionMask = 0;
		
	}
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AddChild(collision);
		QueueRedraw();
	}

    public override void _Draw()
    {
        base._Draw();
		ClientFloor floor = board.CurrentFloor;

		collision.Shape = new RectangleShape2D(){
			Size = floor.SizePixels
		};
		collision.Position = floor.SizePixels/2;

		ZIndex = board.FloorIndex * 100 + 1;

		if (floor == null)
			return;
		var tileSize = floor.TileSize;
		var size = floor.Size;
		for (int x = 0; x < size.X + 1; x++){
			DrawLine(new Vector2(x * tileSize.X, 0), new Vector2(x * tileSize.X, size.Y * tileSize.Y), Colors.White);
		}
		for (int y = 0; y < size.X + 1; y++){
			DrawLine(new Vector2(0, y * tileSize.Y), new Vector2(size.X * tileSize.X, y * tileSize.Y), Colors.White);
		}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.RpgImpl;

public class ClientFloor : Floor
{
    public Node2D Node {get; }
    public MidiaNode DisplayNode {get; }
    public Node EntitiesNode {get; }
    public Node OccludersNode {get; }
    public StaticBody2D Collision {get; }
    public CanvasModulate AmbientLightModulate {get; }

    public Vector2 SizePixels => new Vector2(Size.X * TileSize.X, Size.Y * TileSize.Y);
    public Color AmbientLightColor {
        get
        {
            byte alpha = (byte)((AmbientLight >> 24) & 0xFF);
            byte red = (byte)((AmbientLight >> 16) & 0xFF);
            byte green = (byte)((AmbientLight >> 8) & 0xFF);
            byte blue = (byte)(AmbientLight & 0xFF);
            return Color.Color8(red, green, blue, alpha);
        }
    }

    public ClientFloor(Vector2 size, Vector2 tileSize, uint ambientLight) : base(size.ToNumerics(), tileSize.ToNumerics(), ambientLight){
        Node = new Node2D
        {
            Visible = false
        };

        DisplayNode = new MidiaNode
        {
            Name = "Midia",
            Position = new Vector2(Size.X * TileSize.X / 2, Size.Y * TileSize.Y / 2)
        };

        EntitiesNode = new Node();
        EntitiesNode.Name = "Entities";

        AmbientLightModulate = new CanvasModulate();
        UpdateAmbientModulate();

        OccludersNode = new Node();
        OccludersNode.Name = "Occluders";

        Collision = new StaticBody2D
        {
            Name = "Collision",
            CollisionMask = 2
        };

        Node.AddChild(DisplayNode);
        Node.AddChild(EntitiesNode);
        Node.AddChild(AmbientLightModulate);
        Node.AddChild(OccludersNode);
        Node.AddChild(Collision);
        Node.Ready += () => {
            var tex = GD.Load<Texture2D>("res://assets/light.webp");
            for (int i = 0; i < LineOfSight.Length; i++)
            {
                var lineOfSightNode = new LightOccluder2D 
                {
                    Name = "LineOfSight" + i,
                    Occluder = new OccluderPolygon2D
                    {
                        Closed = false,
                        Polygon = LineOfSight[i].points.Select(p => (p * TileSize).ToGodot()).ToArray()
                    },
                    OccluderLightMask = 3
                };

                OccludersNode.AddChild(lineOfSightNode);
            }
            for (int i = 0; i < Walls.Length; i++)
            {
                Collision.AddChild(new CollisionPolygon2D
                {
                    Name = "Wall" + i,
                    Polygon = Walls[i].points.Select(p => (p * TileSize).ToGodot()).ToArray(),
                    BuildMode = CollisionPolygon2D.BuildModeEnum.Segments
                });
            }
        };
    }

    public Vector2 WorldToPixel(Vector2 world){
        return world * TileSize.ToGodot();
    }
    public Vector2 PixelToWorld(Vector2 pixel){
        return pixel / TileSize.ToGodot();
    }
    public Vector2 PixelToTileCenter(Vector2 pixel){
        return WorldToTileCenter(PixelToWorld(pixel).ToNumerics()).ToGodot();
    }

    public void SetOcclusion(bool occlusion){
        foreach (LightOccluder2D occluder in OccludersNode.GetChildren())
            occluder.Visible = occlusion;
    }

    public void SetCollision(bool collision){
        Collision.ProcessMode = collision ? Godot.Node.ProcessModeEnum.Inherit : Godot.Node.ProcessModeEnum.Disabled;
    }

    public override void SetMidia(Midia midia)
    {
        base.SetMidia(midia);

        DisplayNode.Midia = midia;
    }

    public override System.Numerics.Vector2? GetIntersection(System.Numerics.Vector2 start, System.Numerics.Vector2 end, out System.Numerics.Vector2? normal)
    {
        var godot = GetIntersection(start.ToGodot(), end.ToGodot(), out Vector2? godotNormal);
        if (godot == null)
        {
            normal = null;
            return null;
        }
        normal = ((Vector2)godotNormal).ToNumerics();
        return ((Vector2)godot).ToNumerics();
    }
    
    public Vector2? GetIntersection(Vector2 start, Vector2 end){
        return GetIntersection(start, end, out _);
    }

    public Vector2? GetIntersection(Vector2 start, Vector2 end, out Vector2? normal){
        var spaceState = Node.GetWorld2D().DirectSpaceState;
        var query = PhysicsRayQueryParameters2D.Create(WorldToPixel(start), WorldToPixel(end), 1);
        var result = spaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            normal = null;
            return null;
        }
        
        normal = PixelToWorld((Vector2)result["normal"]);
        return PixelToWorld((Vector2)result["position"]);
    }

    public void UpdateAmbientModulate()
    {
        byte alpha = (byte)((AmbientLight >> 24) & 0xFF);
        byte red = (byte)((AmbientLight >> 16) & 0xFF);
        byte green = (byte)((AmbientLight >> 8) & 0xFF);
        byte blue = (byte)(AmbientLight & 0xFF);
        AmbientLightModulate.Color = Color.Color8(Math.Max(red, (byte)1), Math.Max(green, (byte)1), Math.Max(blue, (byte)1), Math.Max(alpha, (byte)1));
    }

    public System.Numerics.Vector2? GetCircleIntersection(System.Numerics.Vector2 pos, Single radius)
    {
        var godot = GetCircleIntersection(pos.ToGodot(), radius);
        return godot?.ToNumerics();
    }

    public Vector2? GetCircleIntersection(Vector2 pos, float radius){
        var spaceState = Node.GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D
            {
                Radius = radius
            },
            Transform = new Transform2D(0, pos),
            CollideWithAreas = false,
            CollideWithBodies = true,
            CollisionMask = 1,
        };
        var result = spaceState.IntersectShape(query, 1);
        if (result.Count == 0)
            return null;
        
        return PixelToWorld((Vector2)result[0]["position"]);
    }

    public bool IsTransparent(Vector2 pixel){
        if (DisplayNode.Sprite.Texture == null)
            return true;
        return DisplayNode.Sprite.Texture.GetImage().GetPixel((int)pixel.X, (int)pixel.Y).A == 0;
    }

    public override IEnumerable<Line> PossibleOBBIntersections(OBB obb)
    {
        throw new NotImplementedException();
    }
}

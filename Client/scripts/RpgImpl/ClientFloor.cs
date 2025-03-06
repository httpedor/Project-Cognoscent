using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Rpg;

namespace TTRpgClient.scripts.RpgImpl;

public class ClientFloor : Floor
{
    public Node2D Node {get; protected set;}
    public Sprite2D Sprite {get; protected set;}
    public Node EntitiesNode {get; protected set;}
    public Node LightsNode {get; protected set;}
    public Node OccludersNode {get; protected set;}
    public StaticBody2D Collision {get; protected set;}
    public CanvasModulate AmbientLightModulate {get; protected set;}

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
        Node = new Node2D()
        {
            Visible = false
        };

        Sprite = new Sprite2D()
        {
            Name = "Image",
            Centered = false
        };

        EntitiesNode = new Node();
        EntitiesNode.Name = "Entities";

        AmbientLightModulate = new CanvasModulate();
        UpdateAmbientModulate();

        LightsNode = new Node();
        LightsNode.Name = "Lights";

        OccludersNode = new Node();
        OccludersNode.Name = "Occluders";

        Collision = new StaticBody2D()
        {
            Name = "Collision",
            CollisionMask = 2
        };

        Node.AddChild(Sprite);
        Node.AddChild(EntitiesNode);
        Node.AddChild(AmbientLightModulate);
        Node.AddChild(LightsNode);
        Node.AddChild(OccludersNode);
        Node.AddChild(Collision);
        Node.Ready += () => {
            var tex = GD.Load<Texture2D>("res://assets/light.webp");
            for (int i = 0; i < Lights.Length; i++)
            {
                Light light = Lights[i];
                byte alpha = (byte)((light.Color >> 24) & 0xFF);
                byte red = (byte)((light.Color >> 16) & 0xFF);
                byte green = (byte)((light.Color >> 8) & 0xFF);
                byte blue = (byte)(light.Color & 0xFF);
                var lightNode = new PointLight2D
                {
                    Name = "Light" + i,
                    Position = (light.Position * TileSize).ToGodot(),
                    Texture = tex,
                    Color = Color.Color8(red, green, blue, alpha),
                    Energy = light.Intensity,
                    ShadowEnabled = light.Shadows,
                    Scale = new Vector2(TileSize.X / tex.GetWidth() * light.Range, TileSize.Y / tex.GetHeight() * light.Range)
                };
                LightsNode.AddChild(lightNode);
            }
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
        if (OccludersNode == null)
            return;
        foreach (LightOccluder2D occluder in OccludersNode.GetChildren())
            occluder.Visible = occlusion;
    }

    public void SetCollision(bool collision){
        if (Collision == null)
            return;
        Collision.ProcessMode = collision ? Godot.Node.ProcessModeEnum.Inherit : Godot.Node.ProcessModeEnum.Disabled;
    }

    public override void SetImage(byte[] image)
    {
        base.SetImage(image);

        if (Sprite.Texture != null)
            Sprite.Texture.Free();

        if (image.Length == 0)
            return;

        Image img = new Image();
        img.LoadPngFromBuffer(image);
        if (!img.IsEmpty())
            Sprite.Texture = ImageTexture.CreateFromImage(img);
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
        if (godot == null)
            return null;
        return ((Vector2)godot).ToNumerics();
    }

    public Vector2? GetCircleIntersection(Vector2 pos, float radius){
        var spaceState = Node.GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D()
        {
            Shape = new CircleShape2D()
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
        if (Sprite.Texture == null)
            return true;
        return Sprite.Texture.GetImage().GetPixel((int)pixel.X, (int)pixel.Y).A == 0;
    }

    public override IEnumerable<Line> PossibleOBBIntersections(OBB obb)
    {
        throw new NotImplementedException();
    }
}

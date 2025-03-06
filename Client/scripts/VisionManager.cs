

using System.Collections.Generic;
using Godot;
using Rpg.Entities;
using TTRpgClient.scripts.RpgImpl;
public struct VisionPoint{
    public VisionPoint(Vector2 position, float radius){
        Position = position;
        Radius = radius;
        Entity = null;
    }
    public VisionPoint(Creature entity){
        Entity = entity;
        Position = null;
        Radius = entity.GetStatValueOrDefault(CreatureStats.SIGHT);
    }
    public Vector2? Position;
    public Creature? Entity;
    public float Radius;
}
public partial class VisionManager : SubViewport
{
    private Dictionary<string, VisionPoint> visionPoints = new Dictionary<string, VisionPoint>();
    private Dictionary<string, Light2D> lights = new Dictionary<string, Light2D>();
    private Sprite2D renderer;
    public int VisionPointCount => visionPoints.Count;
    public static VisionManager Instance
    {
        get
        {
            return GameManager.Instance.VisionManager;
        }
    }
    private static Texture2D TEX = GD.Load<Texture2D>("res://assets/light.webp");
    public VisionManager()
    {
        Name = "VisionLayer";
        CanvasCullMask = 2;
        World2D = GameManager.Instance.WorldViewport.World2D;
        Disable3D = true;
        HandleInputLocally = false;
        GuiDisableInput = true;
        RenderTargetUpdateMode = UpdateMode.Always;
        TransparentBg = true;

        ColorRect visionBackground = new ColorRect
        {
            Color = new Color(1, 1, 1, 1),
            LightMask = 2,
            VisibilityLayer = 2
        };
        visionBackground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        
        AddChild(visionBackground);

        renderer = new Sprite2D()
        {
            Name = "VisionRenderer",
            Visible = false,
            Texture = GetTexture(),
            Centered = false,
            Material = new ShaderMaterial()
            {
                Shader = GD.Load<Shader>("res://scripts/shaders/vision.gdshader"),
                ResourceLocalToScene = true
            },
        };
    }

    public override void _Ready()
    {
        base._Ready();

        GameManager.Instance.UINode.CallDeferred("add_child", renderer);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        var board = GameManager.Instance.CurrentBoard;
        if (board == null){
            renderer.Visible = false;
            return;
        }

        ((ShaderMaterial)renderer.Material).SetShaderParameter("ambient_color", board.CurrentFloor.AmbientLightModulate.Color);
        if (visionPoints.Count == 0){
            renderer.Visible = false;
            return;
        }
        renderer.Visible = true;

        renderer.ZIndex = board.FloorIndex * 100 + 50;
        
        var TileSize = board.CurrentFloor.TileSize;
        foreach (var entry in visionPoints)
        {
            lights[entry.Key].Visible = true;
            var point = entry.Value;
            if (point.Position != null)
                lights[entry.Key].Position = (Vector2)point.Position;
            else if (point.Entity != null)
            {
                //TODO: Ambient light-based sight
                point.Radius = point.Entity.GetStatValueOrDefault(CreatureStats.SIGHT) * 10;
                lights[entry.Key].Position = board.GetEntityNode(point.Entity).Position;
                if (point.Entity.FloorIndex != board.FloorIndex)
                {
                    for (int i = board.FloorIndex; i > point.Entity.FloorIndex; i--)
                    {
                        var floor = board.GetFloor(i);
                        var pos = new Vector2(point.Entity.Position.X * floor.TileSize.X, point.Entity.Position.Y * floor.TileSize.Y);
                        if (!board.GetFloor(i).IsTransparent(pos))
                        {
                            lights[entry.Key].Visible = false;
                        }
                    }
                }
            }
            lights[entry.Key].Scale = new Vector2(TileSize.X / TEX.GetWidth() * point.Radius * 2, TileSize.Y / TEX.GetHeight() * point.Radius * 2);
        }
    }

    public void AddVisionPoint(string id, VisionPoint point)
    {
        var board = GameManager.Instance.CurrentBoard;
        if (board == null)
            return;
        
        Size = (Vector2I)board.CurrentFloor.SizePixels;

        var TileSize = board.CurrentFloor.TileSize;
        visionPoints[id] = point;
        var light = new PointLight2D()
        {
            Texture = TEX,
            Position = point.Position == null ? board.GetEntityNode(point.Entity).Position : (Vector2)point.Position,
            Color = new Color(1, 1, 1, 1),
            ShadowEnabled = true,
            Scale = new Vector2(TileSize.X / TEX.GetWidth() * point.Radius * 2, TileSize.Y / TEX.GetHeight() * point.Radius * 2),
            BlendMode = Light2D.BlendModeEnum.Sub,
            RangeItemCullMask = 2,
            VisibilityLayer = 2,
            ShadowItemCullMask = 2
        };
        lights[id] = light;
        AddChild(light);
    }
    public void AddVisionPoint(VisionPoint point)
    {
        if (point.Entity != null)
            AddVisionPoint(point.Entity.Id.ToString(), point);
        else if (point.Position != null)
            AddVisionPoint(point.Position.ToString(), point);
        else
            throw new System.Exception("Invalid vision point");
    }
    public VisionPoint GetVisionPoint(string id){
        return visionPoints[id];
    }
    public void RemoveVisionPoint(string id)
    {
        if (!visionPoints.ContainsKey(id))
            return;
        visionPoints.Remove(id);
        lights[id].QueueFree();
        lights.Remove(id);
    }
    public void RemoveVisionPoint(Entity id)
    {
        RemoveVisionPoint(id.Id.ToString());
    }
    public void ClearVisionPoints()
    {
        visionPoints.Clear();
        foreach (var light in lights.Values)
        {
            light.QueueFree();
        }
        lights.Clear();
    }
}
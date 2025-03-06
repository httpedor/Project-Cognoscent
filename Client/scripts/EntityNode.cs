using System;
using Godot;
using Rpg;
using Rpg.Entities;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

public partial class EntityNode : Node2D
{
    private static ShaderMaterial MATERIAL = GD.Load<ShaderMaterial>("res://materials/entity.material");
    public ClientBoard Board {get; protected set;}
    public Entity Entity {get; protected set;}
    public Sprite2D Sprite {get; protected set;}
    public Area2D Hitbox {get; protected set;}
#pragma warning disable CS8602 // Dereference of a possibly null reference.
    public Color Outline
    {

        get => (Sprite.Material as ShaderMaterial).GetShaderParameter("color").As<Color>();

        set
        {
            (Sprite.Material as ShaderMaterial).SetShaderParameter("color", value);
        }
    }
    public Color SpriteModulate
    {
        get => (Sprite.Material as ShaderMaterial).GetShaderParameter("modulate").As<Color>();
        set
        {
            (Sprite.Material as ShaderMaterial).SetShaderParameter("modulate", value);
        }
    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    public EntityNode(Entity ent, ClientBoard board)
    {
        VisibilityLayer = 5;
        Board = board;
        Entity = ent;
        Name = ent.Id.ToString();
        Sprite = new Sprite2D
        {
            Material = (Material)MATERIAL.Duplicate(),
            Centered = true
        };
        AddChild(Sprite);
        SetImage(ent.Image);
        Outline = new Color(1, 0, 0, 0);

        Hitbox = new Area2D
        {
            Monitorable = false,
            Monitoring = false,
            CollisionLayer = 2,
            CollisionMask = 0
        };
        var collision = new CollisionShape2D();
        var shape = new CircleShape2D
        {
            Radius = (float)(board.GetFloor(ent.FloorIndex).TileSize.X / 2)
        };
        collision.Shape = shape;
        Hitbox.AddChild(collision);
        Hitbox.MouseEntered += () => {
            if (board.FloorIndex < Entity.FloorIndex)
                return;
            if (board.FloorIndex > Entity.FloorIndex)
            {
                for (int i = board.FloorIndex; i > Entity.FloorIndex; i--)
                {
                    if (!board.GetFloor(i).IsTransparent(Position))
                        return;
                }
            }
            if (board.SelectedEntity != Entity)
                Outline = new Color(.5f, .5f, .5f, 1);
            Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
            InputManager.RequestPriority(this);
        };
        Hitbox.MouseExited += () => {
            if (board.SelectedEntity != Entity)
                Outline = new Color(1, 0, 0, 0);
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            InputManager.ReleasePriority(this);
        };

        AddChild(Hitbox);

        ent.OnImageChanged += SetImage;
        ent.OnPositionChanged += OnMove;

        if (ent is Creature creature)
        {
            creature.Body.OnInjuryAdded += (bp, inj) => {
                Modulate = new Color(1, 0, 0, Modulate.A);
            };
            creature.Body.OnInjuryRemoved += (bp, inj) => {
                Modulate = new Color(0, 1, 0, Modulate.A);
            };
        }
    }


    public void OnClick(InputEventMouseButton e){
        if (e.ButtonIndex == MouseButton.Left && e.Pressed){
            Board.SelectedEntity = Entity;
            GetViewport().SetInputAsHandled();
        }
    }

    protected void SetImage(byte[] data){
        if (Sprite.Texture != null)
            Sprite.Texture.Free();
        if (data == null || data.Length == 0)
            return;

        Image img = new Image();
        img.LoadPngFromBuffer(data);
        if (!img.IsEmpty())
            Sprite.Texture = ImageTexture.CreateFromImage(img);
    }

    protected void OnMove(System.Numerics.Vector3 newPos, System.Numerics.Vector3 oldPos){
        if (newPos.Z != oldPos.Z){
            Board.GetFloor((int)oldPos.Z).EntitiesNode.RemoveChild(this);
            Board.GetFloor((int)newPos.Z).EntitiesNode.AddChild(this);
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        ClientFloor floor = Board.GetFloor((int)Entity.Position.Z);
        Position = Position.Lerp(new Vector2(floor.TileSize.X * Entity.Position.X, floor.TileSize.Y * Entity.Position.Y), (float)delta * 10);
        Sprite.Scale = Sprite.Scale.Lerp(new Vector2(floor.TileSize.X / Sprite.Texture.GetSize().X * Entity.Size.X, floor.TileSize.Y / Sprite.Texture.GetSize().Y * Entity.Size.Y), (float)delta * 10);
        Rotation = Mathf.LerpAngle(Rotation, Entity.Rotation - (MathF.PI/2 * (Entity is Creature ? 1 : 0)), (float)delta * 10);
        if (Board.FloorIndex > Entity.Position.Z + Entity.Size.Z)
        {
            Modulate = Modulate with {A = 1/MathF.Pow(2, Board.FloorIndex - (Entity.Position.Z + Entity.Size.Z)) + 0.3f};
            SpriteModulate = Modulate;
            Visible = true;
        }
        else if (Board.FloorIndex <= Entity.Position.Z + Entity.Size.Z)
        {
            Modulate = Modulate with {A = 1};
            SpriteModulate = Modulate;
            Visible = true;
        }
        else if (Board.FloorIndex < Entity.FloorIndex)
            Visible = false;

        Modulate = Modulate.Lerp(new Color(1, 1, 1, Modulate.A), (float)delta * 5f);
        
        ZIndex = (Int32)(MathF.Round(Entity.Position.Z * 100) + 15);
    }

    public override void _Ready()
    {
        base._Ready();
    }
}

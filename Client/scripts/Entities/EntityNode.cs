using System;
using Godot;
using Rpg;
using TTRpgClient.scripts.RpgImpl;

namespace TTRpgClient.scripts;

public partial class EntityNode : Node2D, IContextMenuProvider
{
    private static ShaderMaterial MATERIAL = GD.Load<ShaderMaterial>("res://materials/entity.material");
    public ClientBoard Board { get; }
    public Entity Entity { get; }
    public MidiaNode Display { get; protected set; }
    public Area2D Hitbox {get; protected set;}
    protected Label label;
    public string? Label
    {
        get => label.Visible ? label.Text : null;
        set
        {
            label.Text = value;
            label.Visible = string.IsNullOrEmpty(value);
        }
    }

    private ColorRect loadBarBgColorRect;
    private ColorRect loadBarColorRect;
    private Label loadBarLabel;
    private Container loadBar;
    [Export]
    public float LoadBarFilling
    {
        get => loadBarColorRect.Size.X / loadBar.Size.X;
        set
        {
            if (value > 1)
                value = 1;
            var result = new Vector2(value * loadBar.Size.X, loadBarColorRect.Size.Y);
            if (result.X < 0)
            {
                loadBarColorRect.Visible = false;
                loadBarBgColorRect.Visible = false;
            }
            else
            {
                loadBarColorRect.Size = result;
                loadBarColorRect.Visible = true;
                loadBarBgColorRect.Visible = true;
            }
        }
    }

    [Export]
    public string? LoadBarLabel
    {
        get => loadBarLabel.Visible ? loadBarLabel.Text : null;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                loadBarLabel.Visible = false;
                return;
            }
            loadBarLabel.Text = value;
            loadBarLabel.Visible = true;
        }
    }
#pragma warning disable CS8602 // Dereference of a possibly null reference.
    public Color Outline
    {

        get => (Display.Sprite.Material as ShaderMaterial).GetShaderParameter("color").As<Color>();
        set => (Display.Sprite.Material as ShaderMaterial).SetShaderParameter("color", value);
    }
    public Color SpriteModulate
    {
        get => (Display.Sprite.Material as ShaderMaterial).GetShaderParameter("modulate").As<Color>();
        set => (Display.Sprite.Material as ShaderMaterial).SetShaderParameter("modulate", value);
    }
    public bool CircleMask
    {
        get => (Display.Sprite.Material as ShaderMaterial).GetShaderParameter("mask_circle").As<bool>();
        set => (Display.Sprite.Material as ShaderMaterial).SetShaderParameter("mask_circle", value);
    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    public EntityNode(Entity ent, ClientBoard board)
    {
        VisibilityLayer = 5;
        Board = board;
        Entity = ent;
        Name = ent.Id.ToString();
        Display = new MidiaNode
        {
            Midia = ent.Display
        };
        Display.Sprite.Centered = true;
        Display.Sprite.Material = (Material)MATERIAL.Duplicate();
        AddChild(Display);

        ent.OnDisplayChanged += (newDisplay) =>
        {
            Display.Midia = newDisplay;
        };

        label = new Label
        {
            Text = "",
            Size = new Vector2(ent.PixelSize.X, ent.PixelSize.Y/8),
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.Position = new Vector2(-ent.PixelSize.X / 2, (ent.PixelSize.Y / 2) - label.Size.Y);
        AddChild(label);
        
        var loadBarSize = new Vector2(ent.PixelSize.X, ent.PixelSize.Y / 8);

        loadBar = new Container
        {
            Size = loadBarSize
        };
        loadBar.Position = new Vector2(-loadBarSize.X / 2, -(ent.PixelSize.Y/2) - loadBarSize.Y);
        loadBarLabel = new Label
        {
            Size = loadBar.Size,
            Visible = false,
            ZIndex = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(0, -loadBarSize.Y/4)
        };
        loadBar.AddChild(loadBarLabel);
        loadBarColorRect = new ColorRect
        {

            Size = loadBar.Size,
            Color = Colors.DarkGray,
            Visible = false,
        };
        loadBarBgColorRect = new ColorRect()
        {
            Size = loadBar.Size,
            Color = Colors.DimGray,
            Visible = false,
            ZIndex = -1
        };
        loadBar.AddChild(loadBarBgColorRect);
        loadBar.AddChild(loadBarColorRect);
        
        AddChild(loadBar);
        

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
            Radius = ent.Floor.TileSize.X / 2
        };
        collision.Shape = shape;
        Hitbox.AddChild(collision);
        Hitbox.MouseEntered += () => {
            if (Board.FloorIndex < Entity.FloorIndex)
                return;
            if (Board.FloorIndex > Entity.FloorIndex)
            {
                for (int i = Board.FloorIndex; i > Entity.FloorIndex; i--)
                {
                    if (!Board.GetFloor(i).IsTransparent(Position))
                        return;
                }
            }
            MouseEntered();
        };
        Hitbox.MouseExited += MouseExited;

        AddChild(Hitbox);

        ent.OnDisplayChanged += _ => {
            CircleMask = ent.Display.Type == MidiaType.Image;
        };
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

        CircleMask = ent.Display.Type == MidiaType.Image;
    }

    protected void OnMove(System.Numerics.Vector3 newPos, System.Numerics.Vector3 oldPos)
    {
        if (Math.Abs(newPos.Z - oldPos.Z) < 0.0001) return;
        
        Board.GetFloor((int)oldPos.Z).EntitiesNode.RemoveChild(this);
        Board.GetFloor((int)newPos.Z).EntitiesNode.AddChild(this);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        var loadBarSize = new Vector2(Mathf.Max(Entity.PixelSize.X, loadBarLabel.Size.X), Entity.PixelSize.Y / 8);
        loadBar.Size = loadBarSize;
        loadBarLabel.Size = loadBarSize;
        loadBarColorRect.Size = loadBarSize;
        loadBarBgColorRect.Size = loadBarSize;
        loadBar.Position = new Vector2(-loadBarSize.X / 2, -(Entity.PixelSize.Y/2) - loadBarSize.Y);


        ClientFloor floor = Board.GetFloor((int)Entity.Position.Z);
        Position = Position.Lerp(new Vector2(floor.TileSize.X * Entity.Position.X, floor.TileSize.Y * Entity.Position.Y), (float)delta * 10);
        if (Display.Sprite.Texture != null)
            Display.Scale = Display.Scale.Lerp(new Vector2(floor.TileSize.X / Display.Sprite.Texture.GetSize().X * Entity.Size.X, floor.TileSize.Y / Display.Sprite.Texture.GetSize().Y * Entity.Size.Y), (float)delta * 10);
        var rot = Mathf.LerpAngle(Display.Rotation, Entity.Rotation - (MathF.PI/2 * (Entity is Creature ? 1 : 0)), (float)delta * 10);
        Display.Rotation = rot;
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
        
        ZIndex = (int)(MathF.Round(Entity.Position.Z * 100) + 15);
    }

    protected virtual void MouseEntered()
    {
        if (Board.SelectedEntity != Entity)
            Outline = new Color(.5f, .5f, .5f, 1);
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
        InputManager.RequestPriority(this);
    }

    protected virtual void MouseExited()
    {
        if (Board.SelectedEntity != Entity)
            Outline = new Color(1, 0, 0, 0);
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        InputManager.ReleasePriority(this);
    }

    public virtual void OnClick()
    {
        Board.SelectedEntity = Entity;
    }

    public virtual void AddGMContextMenuOptions()
    {
        ContextMenu.AddOption("Mudar Exibição", (_) =>
        {
            Modal.OpenFormDialog("Mudar Exibição", (info) =>
            {
                Midia img = (Midia)info["image"];
                NetworkManager.Instance.SendPacket(new EntityMidiaPacket(Entity, img));
            }, ("image", new Midia(), (obj) => obj is Midia { Type: MidiaType.Image or MidiaType.Video}));
        });
        ContextMenu.AddOption("Mudar Tamanho(Real)", (_) =>
        {
            Modal.OpenFormDialog("Mudar Tamanho(Real)", (info) =>
            {
                Vector3 size = (Vector3)info["Tamanho"];
                //TODO: This
            }, ("Tamanho", Entity.Size.ToGodot(), null));
        });
        ContextMenu.AddOption("Mudar Tamanho(Imagem)", (_) =>
        {
            Modal.OpenFormDialog("Mudar Tamanho(Imagem)", (info) =>
            {
                Vector2 size = (Vector2)info["Tamanho"];
                Entity.Display.Scale = size.ToNumerics();
                NetworkManager.Instance.SendPacket(new EntityMidiaPacket(Entity, Entity.Display));
            }, ("Tamanho", Entity.Display.Scale.ToGodot(), null));
        });
        ContextMenu.AddOption("Destruir Entidade", (_) => {
            if (!Input.IsKeyPressed(Key.Shift))
                Modal.OpenConfirmationDialog("Deletar Entidade", "Deseja deletar/remover essa entidade?", (remove) => {
                    if (remove)
                        NetworkManager.Instance.SendPacket(new EntityRemovePacket(Entity));
                }, "Sim", "Não");
            else
                NetworkManager.Instance.SendPacket(new EntityRemovePacket(Entity));
        });
        ContextMenu.AddOption("Copiar ID", (_) => {
            DisplayServer.ClipboardSet(Entity.Id.ToString());
        });
    }
    public virtual void AddContextMenuOptions()
    {
    }

    public virtual void HideLoadBar()
    {
        LoadBarFilling = -1;
        LoadBarLabel = null;
    }
}

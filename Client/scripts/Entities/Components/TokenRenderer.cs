using System;
using Godot;
using Rpg;
using Rpg.Entities;
using Rpg.Entities.Components;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

namespace TTRpgClient.scripts;

public partial class TokenRenderer : ComponentRenderer<Token>
{
    private static ShaderMaterial MATERIAL = GD.Load<ShaderMaterial>("res://materials/entity.material");

    
    public MidiaNode Display { get; protected set; }
    public Area2D Hitbox {get; protected set;}

    protected Label label;
    public string? Label
    {
        get => label.Visible ? label.Text : null;
        set
        {
            label.Text = value;
            label.Visible = !string.IsNullOrEmpty(value);
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
    public TokenRenderer(Token token, EntityRenderer entityRenderer) : base(token, entityRenderer)
    {
        VisibilityLayer = 5;
        Name = "Token";
                Display = new MidiaNode
        {
            Midia = token.Midia
        };
        Display.Sprite.Centered = true;
        Display.Sprite.Material = (Material)MATERIAL.Duplicate();
        AddChild(Display);

        Hitbox = new Area2D
        {
            Monitorable = false,
            Monitoring = false,
            CollisionLayer = 2,
            CollisionMask = 0
        };
        var collision = new CollisionShape2D();
        label = new Label
        {
            Text = "",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        loadBar = new Container();
        loadBarLabel = new Label
        {
            Size = loadBar.Size,
            Visible = false,
            ZIndex = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        loadBarColorRect = new ColorRect
        {
            Color = Colors.DarkGray,
            Visible = false,
        };
        loadBarBgColorRect = new ColorRect()
        {
            Color = Colors.DimGray,
            Visible = false,
            ZIndex = -1
        };

        var pixelSize = token.PixelSize;
        if (pixelSize.HasValue)
        {
            var shape = new CircleShape2D
            {
                Radius = token.Floor!.TileSize.X / 2
            };
            collision.Shape = shape;
            var pixelSizeVal = pixelSize.Value;
            label.Size = new Vector2(pixelSizeVal.X, pixelSizeVal.Y/8);
            label.Position = new Vector2(-pixelSizeVal.X / 2, (pixelSizeVal.Y / 2) - label.Size.Y);
            AddChild(label);
            
            var loadBarSize = new Vector2(pixelSizeVal.X, pixelSizeVal.Y / 8);

            loadBar.Size = loadBarSize;
            loadBar.Position = new Vector2(-loadBarSize.X / 2, -(pixelSizeVal.Y/2) - loadBarSize.Y);
            loadBarLabel.Position = new Vector2(0, -loadBarSize.Y/4);
            loadBarColorRect.Size = loadBarSize;
            loadBarBgColorRect.Size = loadBarSize;
            
        }
        loadBar.AddChild(loadBarLabel);
        loadBar.AddChild(loadBarBgColorRect);
        loadBar.AddChild(loadBarColorRect);
        AddChild(loadBar);

        Hitbox.AddChild(collision);

        Hitbox.MouseEntered += () => {
            if (Board.FloorIndex < token.FloorIndex)
                return;
            if (Board.FloorIndex > token.FloorIndex)
            {
                for (int i = Board.FloorIndex; i > token.FloorIndex; i--)
                {
                    if (!Board.GetFloor(i)?.IsTransparent(Position) ?? false)
                        return;
                }
            }
            _MouseEntered();
        };
        Hitbox.MouseExited += _MouseExited;

        AddChild(Hitbox);

        Outline = new Color(1, 0, 0, 0);

        CircleMask = Component.Midia?.Type == MidiaType.Image;
    }

    public override void OnReady()
    {
        if (EntityRenderer.GetParent() != null)
            EntityRenderer.GetParent().RemoveChild(EntityRenderer);
		Board.GetFloor(Component.FloorIndex)?.EntitiesNode.AddChild(EntityRenderer);
    }


    protected void OnMove(System.Numerics.Vector3 newPos, System.Numerics.Vector3 oldPos)
    {
        if (Math.Abs(newPos.Z - oldPos.Z) < 0.0001) return;
        
        Board.GetFloor((int)oldPos.Z)?.EntitiesNode.RemoveChild(EntityRenderer);
        Board.GetFloor((int)newPos.Z)?.EntitiesNode.AddChild(EntityRenderer);
    }

    public override void _Process(double delta)
    {
        //TODO: Positioning not quite working, probably because I changed from adding this to the entitiesnode to adding the EntityRenderer to the entitiesnode
        base._Process(delta);

        var token = Component;
        var pixelSizeMaybe = token.PixelSize;
        if (pixelSizeMaybe.HasValue)
        {
            var pixelSize = pixelSizeMaybe.Value;
            var loadBarSize = new Vector2(Mathf.Max(pixelSize.X, loadBarLabel.Size.X), pixelSize.Y / 8);
            loadBar.Size = loadBarSize;
            loadBarLabel.Size = loadBarSize;
            loadBarColorRect.Size = loadBarSize;
            loadBarBgColorRect.Size = loadBarSize;
            loadBar.Position = new Vector2(-loadBarSize.X / 2, -(pixelSize.Y/2) - loadBarSize.Y);
        }


        ClientFloor? floor = Board.GetFloor((int)token.Position.Z);
        if (floor != null)
        {
            EntityRenderer.Position = EntityRenderer.Position.Lerp(new Vector2(floor.TileSize.X * token.Position.X, floor.TileSize.Y * token.Position.Y), (float)delta * 10);
            if (Display.Sprite.Texture != null)
                Display.Scale = Display.Scale.Lerp(new Vector2(floor.TileSize.X / Display.Sprite.Texture.GetSize().X * token.Size.X, floor.TileSize.Y / Display.Sprite.Texture.GetSize().Y * token.Size.Y), (float)delta * 10);
        }
        var rot = Mathf.LerpAngle(Display.Rotation, token.Rotation - MathF.PI/2, (float)delta * 10);
        Display.Rotation = rot;
        var top = token.Position.Z + token.Size.Z;
        if (Board.FloorIndex > top)
        {
            EntityRenderer.Modulate = EntityRenderer.Modulate with {A = 1/MathF.Pow(2, Board.FloorIndex - top) + 0.3f};
            SpriteModulate = Modulate;
            Visible = true;
        }
        else if (Board.FloorIndex <= top)
        {
            EntityRenderer.Modulate = Modulate with {A = 1};
            SpriteModulate = Modulate;
            Visible = true;
        }

        EntityRenderer.Modulate = EntityRenderer.Modulate.Lerp(new Color(1, 1, 1, EntityRenderer.Modulate.A), (float)delta * 5f);
        
        EntityRenderer.ZIndex = (int)(MathF.Round(token.Position.Z * 100) + 15);
    }

    // This is called by the hitbox, and will call the EntityRenderer's MouseEntered, which will call all ComponentRenderer's MouseEntered
    private void _MouseEntered()
    {
        EntityRenderer.MouseEntered();
    }

    // This is called by the EntityRenderer
    public override void MouseEntered()
    {
        base.MouseEntered();
        if (Board.SelectedEntity != Entity)
            Outline = new Color(.5f, .5f, .5f, 1);
    }


    private void _MouseExited()
    {
        EntityRenderer.MouseExited();
    }
    public override void MouseExited()
    {
        base.MouseExited();
        if (Board.SelectedEntity != Entity)
            Outline = new Color(1, 0, 0, 0);
    }

    
    public virtual void HideLoadBar()
    {
        LoadBarFilling = -1;
        LoadBarLabel = null;
    }

    public override void EventFired(ComponentEvent e)
    {
        if (e is TokenUpdateEvent tue)
        {
            if (tue.OldMidia != Component.Midia)
            {
                Display.Midia = Component.Midia;
                if (Component.Midia != null)
                    CircleMask = Component.Midia.Type == MidiaType.Image;
            }
            if (tue.OldPosition != Component.Position)
                OnMove(Component.Position, tue.OldPosition);
        }
    }

}

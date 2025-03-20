using System;
using System.IO;
using System.Linq;
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
    public VideoStreamPlayer VideoPlayer {get; protected set;}
    public Sprite2D Sprite {get; protected set;}
    public Area2D Hitbox {get; protected set;}
#pragma warning disable CS8602 // Dereference of a possibly null reference.
    public Color Outline
    {

        get => (Sprite.Material as ShaderMaterial).GetShaderParameter("color").As<Color>();

        set => (Sprite.Material as ShaderMaterial).SetShaderParameter("color", value);
    }
    public Color SpriteModulate
    {
        get => (Sprite.Material as ShaderMaterial).GetShaderParameter("modulate").As<Color>();
        set => (Sprite.Material as ShaderMaterial).SetShaderParameter("modulate", value);
    }
    public bool CircleMask
    {
        get => (Sprite.Material as ShaderMaterial).GetShaderParameter("mask_circle").As<bool>();
        set => (Sprite.Material as ShaderMaterial).SetShaderParameter("mask_circle", value);
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
            Centered = true,
        };
        AddChild(Sprite);

        VideoPlayer = new VideoStreamPlayer()
        {
            Loop = true,
            Visible = false
        };
        AddChild(VideoPlayer);

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
        Hitbox.MouseExited += () => {
            MouseExited();
        };

        AddChild(Hitbox);

        ent.OnDisplayChanged += (Midia data) => {
            if (!data.IsVideo)
                SetImage(data.Bytes);
            else
                SetVideo(data.Bytes);
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

        if (!ent.Display.IsVideo)
            SetImage(ent.Display.Bytes);
        else
            SetVideo(ent.Display.Bytes);
    }

    protected void SetImage(byte[] data){
        if (Sprite.Texture != null && !(Sprite.Texture is CompressedTexture2D))
            Sprite.Texture.Free();
        if (VideoPlayer.Stream != null)
            VideoPlayer.Stream.Free();
        if (data == null || data.Length == 0)
            return;

        Image img = new Image();
        img.LoadPngFromBuffer(data);
        if (!img.IsEmpty())
            Sprite.Texture = ImageTexture.CreateFromImage(img);

        CircleMask = true;
    }
    protected void SetVideo(byte[] data)
    {
        if (Sprite.Texture != null && !(Sprite.Texture is CompressedTexture2D))
            Sprite.Texture.Free();
        if (VideoPlayer.Stream != null)
            VideoPlayer.Stream.Free();
        if (data == null || data.Length == 0)
            return;
        
        string filePath = Path.Combine(OS.GetCacheDir(), "Temp", "entity_" + Entity.Id);
        using (var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write))
        {
            file.StoreBuffer(data);
        }

        var videoStream = ResourceLoader.Load<VideoStream>("res://assets/ffmpeg.tres");
        videoStream.File = filePath;
        VideoPlayer.Stream = videoStream;

        Sprite.Texture = VideoPlayer.GetVideoTexture();
        CircleMask = false;
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
        if (!VideoPlayer.IsPlaying())
            VideoPlayer.Play();
        ClientFloor floor = Board.GetFloor((int)Entity.Position.Z);
        Position = Position.Lerp(new Vector2(floor.TileSize.X * Entity.Position.X, floor.TileSize.Y * Entity.Position.Y), (float)delta * 10);
        if (Sprite.Texture != null)
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
        ContextMenu.AddOption("Destruir Entidade", (_) => {
            if (!Input.IsKeyPressed(Key.Shift))
                Modal.OpenConfirmationDialog("Deletar Entidade", "Deseja deletar/remover essa entidade?", (remove) => {
                    if (remove)
                        NetworkManager.Instance.SendPacket(new EntityRemovePacket(Entity));
                }, "Sim", "Não");
            else
                NetworkManager.Instance.SendPacket(new EntityRemovePacket(Entity));
            ContextMenu.Hide();
        });
        if (Entity is Creature hoveringCreature)
        {
            ContextMenu.AddOption("Renomear", (_) => {
                Modal.OpenStringDialog("Renomear Entidade", (name) => {
                    if (name == null)
                        return;
                    
                    hoveringCreature.Name = name;
                }, true);
            });

            ContextMenu.AddOption("Danificar Parte", (_) => {
                BodyInspector.Instance.Show(hoveringCreature.Body,
                    new BodyInspector.BodyInspectorSettings(BodyInspector.BodyInspectorSettings.HEALTH)
                    {
                        OnPick = (bp) => {
                            Modal.OpenOptionsDialog("Tipo de Ferida", "Selecione o tipo de ferida que deseja aplicar", InjuryType.GetInjuryTypes().Select((i) => i.Translation).ToArray(), (typeTranslation) => {
                                if (typeTranslation == null)
                                    return;
                                InjuryType type = InjuryType.GetTypeByTranslation(typeTranslation);
                                Modal.OpenStringDialog("Severidade da Ferida", (sevStr) => {
                                    float severity;
                                    if (Single.TryParse(sevStr, out severity))
                                    {
                                        NetworkManager.Instance.SendPacket(new EntityBodyPartInjuryPacket(bp, new Injury(type, severity)));
                                    }
                                });
                            });
                        }
                    }
                );
                ContextMenu.Hide();
            });
            ContextMenu.AddOption("Curar Ferida", (_) => {
                BodyInspector.Instance.Show(hoveringCreature.Body, new BodyInspector.BodyInspectorSettings(BodyInspector.BodyInspectorSettings.HEALTH)
                {
                    OnPick = (bp) => {
                        Modal.OpenOptionsDialog("Ferida", "Selecione a ferida que deseja curar", bp.Injuries.Select((inj) => {return inj.Type.Translation + " - " + inj.Severity;}).ToArray(), (selected) => {
                            if (selected == null)
                                return;
                            var splitted = selected.Split(" - ");
                            InjuryType it = InjuryType.GetTypeByTranslation(splitted[0]);
                            float severity;
                            if (Single.TryParse(splitted[1], out severity))
                                NetworkManager.Instance.SendPacket(new EntityBodyPartInjuryPacket(bp, new Injury(it, severity), true));
                        });
                    }
                });
                ContextMenu.Hide();
            });
        }
    }
    public virtual void AddContextMenuOptions()
    {
    }

    public override void _Ready()
    {
        base._Ready();
    }
}

using System;
using System.IO;
using Godot;
using Rpg;
using FileAccess = Godot.FileAccess;

namespace TTRpgClient.scripts;

public partial class MidiaNode : Node2D
{
    public readonly Sprite2D Sprite;
    public readonly VideoStreamPlayer VideoPlayer;

    public Texture2D Texture
    {
        get
        {
            if (Midia is { Type: MidiaType.Video })
                return VideoPlayer.GetVideoTexture();
            return Sprite.Texture;
        }
    }

    public Midia? Midia
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            if (Sprite.Texture != null && Sprite.Texture is not CompressedTexture2D)
                Sprite.Texture.Free();
            VideoPlayer.Stream?.Free();
            
            if (value == null)
            {
                Visible = false;
                return;
            }

            Visible = true;

            if (value.Type == MidiaType.Video)
            {
                string filePath = Path.Combine(OS.GetCacheDir(), new Random().Next() + ".midia");
                using (FileAccess? file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write))
                {
                    file.StoreBuffer(value.Bytes);
                }

                var videoStream = ResourceLoader.Load<VideoStream>("res://assets/ffmpeg.tres");
                videoStream.File = filePath;
                VideoPlayer.Stream = videoStream;

                Sprite.Texture = VideoPlayer.GetVideoTexture();
                DirAccess.RemoveAbsolute(filePath);
            }
            else if (value.Type == MidiaType.Image)
            {
                if (value.Bytes.Length <= 0)
                    return;

                var img = new Image();
                var imageError = TryLoadImageFromBuffer(img, value.Bytes);

                if (imageError == Error.Ok && !img.IsEmpty())
                    Sprite.Texture = ImageTexture.CreateFromImage(img);
                else
                {
                    GD.PushWarning($"Failed to decode image bytes for MidiaNode: {imageError}");
                }
            }
            //TODO: Directional audio(or smth like that)
            else
            {
                GD.PushWarning($"Midia of type {value.Type} cannot be shown in a node");
            }
        }
    }

    public MidiaNode()
    {
        Sprite = new Sprite2D();
        VideoPlayer = new VideoStreamPlayer
        {
            Loop = true,
            Visible = false
        };
        AddChild(Sprite);
        AddChild(VideoPlayer);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        
        if (!VideoPlayer.IsPlaying())
            VideoPlayer.Play();

        Sprite.Scale = Sprite.Scale.Lerp(Midia?.Scale.ToGodot() ?? Vector2.One, (float)delta);
    }

    public void SetImage(Texture2D tex)
    {
        Midia = null;
        Sprite.Texture = tex;
        Visible = true;
    }

    public void SetTexture(Texture2D tex)
    {
        SetImage(tex);
    }

    private static Error TryLoadImageFromBuffer(Image image, byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A)
        {
            return image.LoadPngFromBuffer(bytes);
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
        {
            return image.LoadJpgFromBuffer(bytes);
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            return image.LoadWebpFromBuffer(bytes);
        }

        return Error.FileUnrecognized;
    }
}
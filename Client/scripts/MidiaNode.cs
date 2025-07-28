using System;
using System.Collections.Generic;
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
            if (Midia is { IsVideo: true })
                return VideoPlayer.GetVideoTexture();
            return Sprite.Texture;
        }
    }

    public Midia? Midia
    {
        get;
        set
        {
            field = value;
            if (Sprite.Texture != null && Sprite.Texture is not CompressedTexture2D)
                Sprite.Texture.Free();
            VideoPlayer.Stream?.Free();
            
            if (!value.HasValue)
            {
                Visible = false;
                return;
            }

            Visible = true;

            if (value.Value.IsVideo)
            {
                string filePath = Path.Combine(OS.GetCacheDir(), new Random().Next() + ".midia");
                using (FileAccess? file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write))
                {
                    file.StoreBuffer(value.Value.Bytes);
                }

                var videoStream = ResourceLoader.Load<VideoStream>("res://assets/ffmpeg.tres");
                videoStream.File = filePath;
                VideoPlayer.Stream = videoStream;

                Sprite.Texture = VideoPlayer.GetVideoTexture();
                DirAccess.RemoveAbsolute(filePath);
            }
            else
            {
                if (value.Value.Bytes.Length <= 0)
                    return;
                
                var img = new Image();
                img.LoadPngFromBuffer(value.Value.Bytes);
                if (!img.IsEmpty())
                    Sprite.Texture = ImageTexture.CreateFromImage(img);
                else
                {
                    img.LoadJpgFromBuffer(value.Value.Bytes);
                    if (!img.IsEmpty())
                        Sprite.Texture = ImageTexture.CreateFromImage(img);
                    else
                    {
                        img.LoadWebpFromBuffer(value.Value.Bytes);
                        if (!img.IsEmpty())
                            Sprite.Texture = ImageTexture.CreateFromImage(img);
                    }
                }
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
}
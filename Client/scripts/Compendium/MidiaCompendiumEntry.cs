using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Rpg;
using FileAccess = Godot.FileAccess;

namespace TTRpgClient.scripts.ui;

public partial class MidiaCompendiumEntry(string entryId, JsonElement json)
    : CompendiumEntry(Compendium.GetFolderName<Midia>(), entryId, json)
{
    public override Texture2D GetIcon()
    {
            MidiaType type;
            string fName = json.GetProperty("fileName").GetString()!;
            byte[] data = Convert.FromBase64String(json.GetProperty("data")!.GetString()!);
            if (json.TryGetProperty("type", out var prop))
                Enum.TryParse(prop.GetString(), out type);
            else
                type = Midia.GetFilenameType(fName);
            switch (type)
            {
                case MidiaType.Image:
                {
                    Image image = new Image();
                    if (fName.EndsWith(".png"))
                        image.LoadPngFromBuffer(data);
                    else if (fName.EndsWith(".jpg") || fName.EndsWith(".jpeg") || fName.EndsWith(".jfif"))
                        image.LoadJpgFromBuffer(data);
                    else if (fName.EndsWith(".webp"))
                        image.LoadWebpFromBuffer(data);
                    else if (fName.EndsWith(".svg"))
                        image.LoadSvgFromBuffer(data);
                    return ImageTexture.CreateFromImage(image);
                }
                case MidiaType.Video:
                {
                    string filePath = Path.Combine(OS.GetCacheDir(), new Random().Next() + ".midia");
                    using (FileAccess? file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write))
                    {
                        file.StoreBuffer(data);
                    }
                    var videoStream = ResourceLoader.Load<VideoStream>("res://assets/ffmpeg.tres");
                    videoStream.File = filePath;
                    var player = new VideoStreamPlayer();
                    player.Stream = videoStream;
                    var ret = ImageTexture.CreateFromImage(player.GetVideoTexture().GetImage());
                    
                    DirAccess.RemoveAbsolute(filePath);
                    return ret;
                }
                case MidiaType.Audio:
                {
                    return Icons.Audio;
                }
                case MidiaType.Binary:
                default:
                    return Icons.File;
            }
    }
}
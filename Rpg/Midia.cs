
using System.Numerics;
using System.Text.Json.Serialization;

namespace Rpg;

public enum MidiaType
{
    Image,
    Video,
    Audio,
    Binary
}
public class Midia : ISerializable
{
    [JsonIgnore]
    public byte[] Bytes;
    public string Base64 => Convert.ToBase64String(Bytes);
    public MidiaType Type;
    public Vector2 Scale = Vector2.One;
    public Midia() : this([])
    {

    }
    public Midia(byte[] bytes, string fileName) : this(bytes, GetFilenameType(fileName))
    {

    }
    public Midia(string fileName) : this(File.Exists(fileName) ? File.ReadAllBytes(fileName) : Array.Empty<Byte>(), GetFilenameType(fileName))
    {

    }
    public Midia(byte[] bytes, MidiaType type = MidiaType.Binary)
    {
        Bytes = bytes;
        Type = type;
    }
    public Midia(Stream stream)
    {
        Type = (MidiaType)stream.ReadByte();
        Scale = stream.ReadVec2();
        Bytes = stream.ReadExactly(stream.ReadUInt32());
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteByte((byte)Type);
        stream.WriteVec2(Scale);
        stream.WriteUInt32((uint)Bytes.Length);
        stream.Write(Bytes);
    }

    public static MidiaType GetFilenameType(string? fileName)
    {
        if (fileName == null)
            return MidiaType.Binary;
        switch (fileName.Substring(fileName.LastIndexOf('.')+1))
        {
            case "webm":
            case "mp4":
            case "mkv":
                return MidiaType.Video;
            case "bmp":
            case "webp":
            case "jpg":
            case "png":
            case "jpeg":
            case "jfif":
                return MidiaType.Image;
            case "ogg":
            case "wav":
                return MidiaType.Audio;
            default:
                return MidiaType.Binary;
        }
    }
}

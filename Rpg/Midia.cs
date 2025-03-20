
namespace Rpg;

public struct Midia : ISerializable
{
    public byte[] Bytes;
    public bool IsVideo = false;
    public Midia() : this(Array.Empty<Byte>(), false)
    {

    }
    public Midia(byte[] bytes, string fileName) : this(bytes, IsFilenameVideo(fileName))
    {

    }
    public Midia(string fileName) : this(File.Exists(fileName) ? File.ReadAllBytes(fileName) : Array.Empty<Byte>(), IsFilenameVideo(fileName))
    {

    }
    public Midia(byte[] bytes, bool isVideo = false)
    {
        Bytes = bytes;
        IsVideo = isVideo;
    }
    public Midia(Stream stream)
    {
        IsVideo = stream.ReadByte() != 0;
        Bytes = stream.ReadExactly(stream.ReadUInt32());
    }
    
    public void ToBytes(Stream stream)
    {
        stream.WriteByte((Byte)(IsVideo ? 1 : 0));
        stream.WriteUInt32((UInt32)Bytes.Length);
        stream.Write(Bytes);
    }

    public static bool IsFilenameVideo(string fileName)
    {
        return fileName != null && (fileName.EndsWith(".webm") || fileName.EndsWith(".mp4"));
    }
}

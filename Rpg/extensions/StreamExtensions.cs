using System.Numerics;

namespace Rpg;

public static class StreamExtensions
{
    public static byte[] ReadExactly(this Stream stream, uint count)
    {
        var ret = new byte[count];
        stream.ReadExactly(ret);
        return ret;
    }
    public static void Splice(this MemoryStream stream, int offset, int count)
    {
        byte[] buf = stream.GetBuffer();
        Buffer.BlockCopy(buf, offset+count, buf, offset, count);
        stream.SetLength(stream.Length - count);
    }
    public static void Splice(this MemoryStream stream, uint offset, uint count)
    {
        byte[] buf = stream.GetBuffer();
        Array.Copy(buf, offset + count, buf, offset, buf.Length - offset - count);
        stream.SetLength(stream.Length - count);
    }

    public static void WriteString(this Stream stream, string? str)
    {
        if (str == null)
        {
            stream.WriteByte(0);
            return;
        }
        if (str.Length > byte.MaxValue)
            throw new Exception("String too long");
        stream.WriteByte((byte)str.Length);
        stream.Write(str.ToBytes());
    }

    public static void WriteLongString(this Stream stream, string? str)
    {
        if (str == null)
        {
            stream.WriteUInt16(0);
            return;
        }
        
        if (str.Length > ushort.MaxValue)
            throw new Exception("String too long");
        
        stream.WriteUInt16((ushort)str.Length);
        stream.Write(str.ToBytes());
    }

    public static string ReadString(this Stream stream)
    {
        byte strLen = stream.ReadExactly(1)[0];
        
        byte[] data = stream.ReadExactly(strLen);
        return new string(data.Select(b => (char)b).ToArray());
    }

    public static string ReadString(this Stream stream, uint length)
    {
        byte[] data = stream.ReadExactly(length);
        return new string(data.Select(b => (char)b).ToArray());
    }
    
    public static string ReadLongString(this Stream stream)
    {
        byte[] data = stream.ReadExactly(sizeof(ushort));
        ushort strLen = BitConverter.ToUInt16(data);
        
        data = stream.ReadExactly(strLen);
        return new string(data.Select(b => (char)b).ToArray());
    }

    public static void ReadExactly(this Stream stream, byte[] buffer)
    {
        stream.Read(buffer, 0, buffer.Length);
    }
    public static float ReadFloat(this Stream stream)
    {
        return BitConverter.ToSingle(stream.ReadExactly(4));
    }
    public static double ReadDouble(this Stream stream)
    {
        return BitConverter.ToDouble(stream.ReadExactly(8));
    }
    public static UInt16 ReadUInt16(this Stream stream)
    {
        return BitConverter.ToUInt16(stream.ReadExactly(2));
    }
    public static Int16 ReadInt16(this Stream stream)
    {
        return BitConverter.ToInt16(stream.ReadExactly(2));
    }
    public static UInt32 ReadUInt32(this Stream stream)
    {
        return BitConverter.ToUInt32(stream.ReadExactly(4));
    }
    public static Int32 ReadInt32(this Stream stream)
    {
        return BitConverter.ToInt32(stream.ReadExactly(4));
    }
    public static UInt64 ReadUInt64(this Stream stream)
    {
        return BitConverter.ToUInt64(stream.ReadExactly(8));
    }
    public static Int64 ReadInt64(this Stream stream)
    {
        return BitConverter.ToInt64(stream.ReadExactly(8));
    }

    public static Vector2 ReadVec2(this Stream stream)
    {
        return new Vector2(stream.ReadFloat(), stream.ReadFloat());
    }

    public static Vector3 ReadVec3(this Stream stream)
    {
        return new Vector3(stream.ReadFloat(), stream.ReadFloat(), stream.ReadFloat());
    }

    public static void WriteVec2(this Stream stream, Vector2 vec)
    {
        stream.WriteFloat(vec.X);
        stream.WriteFloat(vec.Y);
    }

    public static void WriteVec3(this Stream stream, Vector3 vec)
    {
        stream.WriteFloat(vec.X);
        stream.WriteFloat(vec.Y);
        stream.WriteFloat(vec.Z);
    }

    public static void WriteFloat(this Stream stream, float value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteDouble(this Stream stream, double value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteUInt16(this Stream stream, UInt16 value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteInt16(this Stream stream, Int16 value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteUInt32(this Stream stream, UInt32 value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteInt32(this Stream stream, Int32 value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteUInt64(this Stream stream, UInt64 value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteInt64(this Stream stream, Int64 value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
}


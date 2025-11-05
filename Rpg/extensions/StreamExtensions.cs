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
    
    public static void WriteBoolean(this Stream stream, bool value)
    {
        stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    public static bool ReadBoolean(this Stream stream)
    {
        byte value = (byte)stream.ReadByte();
        if (value > 1)
            throw new Exception("Invalid boolean value");
        return value == 1;
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

    public static float ReadFloat(this Stream stream)
    {
        return BitConverter.ToSingle(stream.ReadExactly(4));
    }
    public static double ReadDouble(this Stream stream)
    {
        return BitConverter.ToDouble(stream.ReadExactly(8));
    }
    public static ushort ReadUInt16(this Stream stream)
    {
        return BitConverter.ToUInt16(stream.ReadExactly(2));
    }
    public static short ReadInt16(this Stream stream)
    {
        return BitConverter.ToInt16(stream.ReadExactly(2));
    }
    public static uint ReadUInt32(this Stream stream)
    {
        return BitConverter.ToUInt32(stream.ReadExactly(4));
    }
    public static int ReadInt32(this Stream stream)
    {
        return BitConverter.ToInt32(stream.ReadExactly(4));
    }
    public static ulong ReadUInt64(this Stream stream)
    {
        return BitConverter.ToUInt64(stream.ReadExactly(8));
    }
    public static long ReadInt64(this Stream stream)
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
    public static void WriteUInt16(this Stream stream, ushort value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteInt16(this Stream stream, short value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteUInt32(this Stream stream, uint value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteInt32(this Stream stream, int value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteUInt64(this Stream stream, ulong value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }
    public static void WriteInt64(this Stream stream, long value)
    {
        stream.Write(BitConverter.GetBytes(value));
    }

    public static void WritePrimitive<T>(this Stream stream, T value) where T : unmanaged
    {
        switch (value)
        {
            case byte b:
                stream.WriteByte(0);
                stream.WriteByte(b);
                break;
            case ushort us:
                stream.WriteByte(1);
                stream.WriteUInt16(us);
                break;
            case uint ui:
                stream.WriteByte(2);
                stream.WriteUInt32(ui);
                break;
            case ulong ul:
                stream.WriteByte(3);
                stream.WriteUInt64(ul);
                break;
            case short s:
                stream.WriteByte(4);
                stream.WriteInt16(s);
                break;
            case int i:
                stream.WriteByte(5);
                stream.WriteInt32(i);
                break;
            case long l:
                stream.WriteByte(6);
                stream.WriteInt64(l);
                break;
            case float f:
                stream.WriteByte(7);
                stream.WriteFloat(f);
                break;
            case double d:
                stream.WriteByte(8);
                stream.WriteDouble(d);
                break;
            case bool b:
                stream.WriteByte(9);
                stream.WriteBoolean(b);
                break;
            default:
                throw new Exception("Unsupported primitive type: " + value.GetType().Name);
        }
    }

    public static T ReadPrimitive<T>(this Stream stream) where T : unmanaged
    {
        object ret;
        byte typeId = (byte)stream.ReadByte();
        switch (typeId)
        {
            case 0:
                ret = (T)(object)stream.ReadExactly(1)[0];
                break;
            case 1:
                ret = (T)(object)stream.ReadUInt16();
                break;
            case 2:
                ret = (T)(object)stream.ReadUInt32();
                break;
            case 3:
                ret = (T)(object)stream.ReadUInt64();
                break;
            case 4:
                ret = (T)(object)stream.ReadInt16();
                break;
            case 5:
                ret = (T)(object)stream.ReadInt32();
                break;
            case 6:
                ret = (T)(object)stream.ReadInt64();
                break;
            case 7:
                ret = (T)(object)stream.ReadFloat();
                break;
            case 8:
                ret = (T)(object)stream.ReadDouble();
                break;
            case 9:
                ret = (T)(object)stream.ReadBoolean();
                break;
            default:
                throw new Exception("Unsupported primitive type ID: " + typeId);
        }
        return (T)ret;
    }
}


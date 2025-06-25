namespace AssetStudio;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class BinaryReaderExtensions
{
    public static void AlignStream(this BinaryReader reader)
        => reader.AlignStream(4);

    public static void AlignStream(this BinaryReader reader, int alignment)
    {
        var pos = reader.BaseStream.Position;
        var mod = pos % alignment;
        if (mod != 0)
        {
            reader.BaseStream.Position += alignment - mod;
        }
    }

    public static string ReadAlignedString(this BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (0 < length && length <= reader.BaseStream.Length - reader.BaseStream.Position)
        {
            var stringData = reader.ReadBytes(length);
            var result = Encoding.UTF8.GetString(stringData);
            reader.AlignStream(4);
            return result;
        }
        
        return string.Empty;
    }

    public static string ReadStringToNull(this BinaryReader reader, int maxLength = 32767, Encoding encoding = null)
    {
        if (encoding?.CodePage == 1200) //Unicode (UTF-16LE)
            return reader.ReadUnicodeStringToNull(maxLength * 2);

        Span<byte> bytes = stackalloc byte[maxLength];
        var count = 0;
        while (reader.BaseStream.Position != reader.BaseStream.Length && count < maxLength)
        {
            var b = reader.ReadByte();
            if (b == 0)
            {
                break;
            }
            bytes[count] = b;
            count++;
        }
        bytes = bytes.Slice(0, count);

        return encoding?.GetString(bytes) ?? Encoding.UTF8.GetString(bytes);
    }

    private static string ReadUnicodeStringToNull(this BinaryReader reader, int maxLength)
    {
        var bytes = new List<byte>();
        var count = 0;
        while (reader.BaseStream.Position != reader.BaseStream.Length && count < maxLength)
        {
            var b = reader.ReadBytes(2);
            if (b.Length < 2 || (b[0] == 0 && b[1] == 0))
            {
                break;
            }
            bytes.AddRange(b);
            count += 2;
        }
        return Encoding.Unicode.GetString(bytes.ToArray());
    }

    public static Quaternion ReadQuaternion(this BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    public static Vector2 ReadVector2(this BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle());

    public static Vector3 ReadVector3(this BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    public static Vector4 ReadVector4(this BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    public static Color ReadColor4(this BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    public static Matrix4x4 ReadMatrix(this BinaryReader reader)
        => new(reader.ReadSingleArray(16));
    
    public static bool[] ReadBooleanArray(this BinaryReader reader)
        => ReadArray(reader.ReadBoolean, reader.ReadInt32());

    public static byte[] ReadUInt8Array(this BinaryReader reader)
        => reader.ReadBytes(reader.ReadInt32());

    public static ushort[] ReadUInt16Array(this BinaryReader reader)
        => ReadArray(reader.ReadUInt16, reader.ReadInt32());

    public static int[] ReadInt32Array(this BinaryReader reader)
        => ReadArray(reader.ReadInt32, reader.ReadInt32());

    public static int[] ReadInt32Array(this BinaryReader reader, int length)
        => ReadArray(reader.ReadInt32, length);

    public static uint[] ReadUInt32Array(this BinaryReader reader)
        => ReadArray(reader.ReadUInt32, reader.ReadInt32());

    public static uint[][] ReadUInt32ArrayArray(this BinaryReader reader)
        => ReadArray(reader.ReadUInt32Array, reader.ReadInt32());

    public static uint[] ReadUInt32Array(this BinaryReader reader, int length)
        => ReadArray(reader.ReadUInt32, length);

    public static float[] ReadSingleArray(this BinaryReader reader)
        => ReadArray(reader.ReadSingle, reader.ReadInt32());

    public static float[] ReadSingleArray(this BinaryReader reader, int length)
        => ReadArray(reader.ReadSingle, length);

    public static string[] ReadStringArray(this BinaryReader reader)
        => ReadArray(reader.ReadAlignedString, reader.ReadInt32());

    public static Vector2[] ReadVector2Array(this BinaryReader reader)
        => ReadArray(reader.ReadVector2, reader.ReadInt32());

    public static Vector4[] ReadVector4Array(this BinaryReader reader)
        => ReadArray(reader.ReadVector4, reader.ReadInt32());

    public static Matrix4x4[] ReadMatrixArray(this BinaryReader reader)
        => ReadArray(reader.ReadMatrix, reader.ReadInt32());

    public static T[] ReadArray<T>(Func<T> readFunc, int length)
    {
        var array = new T[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = readFunc();
        }
        return array;
    }
}
namespace AssetStudio;

using System;
using System.Text;
using System.Buffers.Binary;

public static class EndianSpanReader
{
    public static uint ReadUInt32(this Span<byte> data, int start, bool isBigEndian)
    {
        return SpanToUInt32(data, start, isBigEndian);
    }

    public static uint SpanToUInt32(Span<byte> data, int start, bool isBigEndian)
    {
        return isBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(data.Slice(start))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(start));
    }

    public static long ReadUInt16(this Span<byte> data, int start, bool isBigEndian)
    {
        return SpanToUInt16(data, start, isBigEndian);
    }

    public static uint SpanToUInt16(Span<byte> data, int start, bool isBigEndian)
    {
        return isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(data.Slice(start))
            : BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(start));
    }

    public static long ReadInt64(this Span<byte> data, int start, bool isBigEndian)
    {
        return SpanToInt64(data, start, isBigEndian);
    }

    public static long SpanToInt64(Span<byte> data, int start, bool isBigEndian)
    {
        return isBigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(data.Slice(start))
            : BinaryPrimitives.ReadInt64LittleEndian(data.Slice(start));
    }

    public static float ReadSingle(this Span<byte> data, int start, bool isBigEndian)
    {
        return SpanToSingle(data, start, isBigEndian);
    }

    public static float SpanToSingle(Span<byte> data, int start, bool isBigEndian)
    {
        return isBigEndian
            ? BinaryPrimitives.ReadSingleBigEndian(data[start..])
            : BinaryPrimitives.ReadSingleLittleEndian(data[start..]);
    }

    public static string ReadStringToNull(this Span<byte> data, int maxLength = 32767)
    {
        Span<byte> bytes = stackalloc byte[maxLength];
        var count = 0;
        while (count != data.Length && count < maxLength)
        {
            var b = data[count];
            if (b == 0)
            {
                break;
            }
            bytes[count] = b;
            count++;
        }
        bytes = bytes.Slice(0, count);

        return Encoding.UTF8.GetString(bytes);
    }
}
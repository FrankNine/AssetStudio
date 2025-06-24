namespace AssetStudio;

using System;
using System.Text;
using System.Buffers.Binary;

public static class EndianSpanReader
{
    public static uint ReadUInt32(this Span<byte> data, int start, bool isBigEndian)
        => _SpanToUInt32(data, start, isBigEndian);

    private static uint _SpanToUInt32(Span<byte> data, int start, bool isBigEndian)
        => isBigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data[start..])
                       : BinaryPrimitives.ReadUInt32LittleEndian(data[start..]);

    public static long ReadUInt16(this Span<byte> data, int start, bool isBigEndian)
        => _SpanToUInt16(data, start, isBigEndian);

    private static uint _SpanToUInt16(Span<byte> data, int start, bool isBigEndian)
        => isBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data[start..])
                       : BinaryPrimitives.ReadUInt16LittleEndian(data[start..]);

    public static long ReadInt64(this Span<byte> data, int start, bool isBigEndian)
        => _SpanToInt64(data, start, isBigEndian);

    private static long _SpanToInt64(Span<byte> data, int start, bool isBigEndian)
        => isBigEndian ? BinaryPrimitives.ReadInt64BigEndian(data[start..])
                       : BinaryPrimitives.ReadInt64LittleEndian(data[start..]);

    public static float ReadSingle(this Span<byte> data, int start, bool isBigEndian)
        => _SpanToSingle(data, start, isBigEndian);

    private static float _SpanToSingle(Span<byte> data, int start, bool isBigEndian)
        => isBigEndian ? BinaryPrimitives.ReadSingleBigEndian(data[start..])
                       : BinaryPrimitives.ReadSingleLittleEndian(data[start..]);

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
        bytes = bytes[..count];

        return Encoding.UTF8.GetString(bytes);
    }
}
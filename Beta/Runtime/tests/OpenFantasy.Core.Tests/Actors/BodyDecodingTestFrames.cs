using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace OpenFantasy.Core.Tests.Actors;

/// <summary>Mutates independently retained frames without invoking the production encoder.</summary>
internal static class BodyDecodingTestFrames
{
    internal static byte[] Bytes(JsonElement value) => Convert.FromHexString(value.GetProperty("canonicalHex").GetString()!);
    internal static byte[] Text(string value) => Encoding.UTF8.GetBytes(value);
    internal static byte[] Number(ulong value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(result, value);
        return result;
    }

    internal static List<byte[]> Fields(byte[] frame)
    {
        uint count = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8));
        int cursor = 12;
        List<byte[]> result = [];
        for (uint index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(cursor)));
            cursor += 4;
            result.Add(frame.AsSpan(cursor, length).ToArray());
            cursor += length;
        }
        Assert.Equal(frame.Length, cursor);
        return result;
    }

    internal static byte[] Frame(IEnumerable<byte[]> fields)
    {
        using var stream = new MemoryStream();
        byte[][] values = fields.ToArray();
        stream.Write("OFC\0"u8);
        WriteUnsigned32(stream, 1);
        WriteUnsigned32(stream, checked((uint)values.Length));
        foreach (byte[] field in values)
        {
            WriteUnsigned32(stream, checked((uint)field.Length));
            stream.Write(field);
        }
        return stream.ToArray();
    }

    internal static byte[] Replace(byte[] frame, int fieldIndex, byte[] value)
    {
        List<byte[]> fields = Fields(frame);
        fields[fieldIndex] = value;
        return Frame(fields);
    }

    internal static byte[] ChangeNested(byte[] frame, int fieldIndex, Func<List<byte[]>, List<byte[]>> change) =>
        Replace(frame, fieldIndex, Frame(change(Fields(Fields(frame)[fieldIndex]))));

    private static void WriteUnsigned32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}

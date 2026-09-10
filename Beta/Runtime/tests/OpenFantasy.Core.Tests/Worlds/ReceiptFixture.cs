using System.Buffers.Binary;
using System.Text.Json;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

internal static class ReceiptFixture
{
    internal static JsonElement Read() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "world-receipts-v1.json"))).RootElement.Clone();
    internal static byte[] Bytes(JsonElement value) => Convert.FromHexString(value.GetProperty("canonicalHex").GetString()!);
    internal static byte[] AppliedBytes() => Bytes(Read().GetProperty("receipts")[0]);
    internal static WorldActionReceipt Applied(ulong command = 401) => WorldReceiptCodecV1.Decode(
        Replace(AppliedBytes(), 2, CommandFixture.Id(command).ToByteArray()));

    internal static byte[] Number(ulong value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(result, value);
        return result;
    }

    internal static List<byte[]> Fields(byte[] bytes)
    {
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8));
        int offset = 12;
        List<byte[]> result = [];
        for (uint index = 0; index < count; index++)
        {
            int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            result.Add(bytes.AsSpan(offset, size).ToArray());
            offset += size;
        }
        Assert.Equal(bytes.Length, offset);
        return result;
    }

    internal static byte[] Frame(IEnumerable<byte[]> values)
    {
        byte[][] fields = values.ToArray();
        byte[] result = new byte[12 + fields.Sum(field => field.Length + 4)];
        "OFC\0"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(8), (uint)fields.Length);
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(offset), (uint)field.Length);
            offset += 4;
            field.CopyTo(result, offset);
            offset += field.Length;
        }
        return result;
    }

    internal static byte[] Replace(byte[] bytes, int index, byte[] replacement)
    {
        List<byte[]> fields = Fields(bytes);
        fields[index] = replacement;
        return Frame(fields);
    }
}

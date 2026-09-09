using System.Buffers.Binary;
using System.Text;

namespace OpenFantasy.Core;

internal static class CanonicalEncodingV1
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] Text(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
        return StrictUtf8.GetBytes(value);
    }

    internal static byte[] Unsigned64(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    internal static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields)
    {
        byte[] tagBytes = Text(tag, nameof(tag));
        int length = checked(12 + 4 + tagBytes.Length);
        foreach (ReadOnlyMemory<byte> field in fields)
            length = checked(length + 4 + field.Length);

        byte[] bytes = new byte[length];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length + 1));
        int offset = 12;
        WriteField(bytes, ref offset, tagBytes);
        foreach (ReadOnlyMemory<byte> field in fields)
            WriteField(bytes, ref offset, field.Span);
        return bytes;
    }

    private static void WriteField(Span<byte> destination, ref int offset, ReadOnlySpan<byte> field)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination[offset..], checked((uint)field.Length));
        offset = checked(offset + 4);
        field.CopyTo(destination[offset..]);
        offset = checked(offset + field.Length);
    }
}

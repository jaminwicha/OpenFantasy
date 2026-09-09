using System.Buffers.Binary;
using System.Text;

namespace OpenFantasy.Core;

internal static class CanonicalEncodingV1
{
    private const uint EncodingVersion = 1;
    private const int MagicByteLength = 4;
    private const int LengthPrefixByteLength = sizeof(uint);
    private const int VersionOffset = MagicByteLength;
    private const int FieldCountOffset = VersionOffset + sizeof(uint);
    private const int HeaderByteLength = FieldCountOffset + sizeof(uint);

    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    internal static byte[] Text(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
        return _strictUtf8.GetBytes(value);
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
        int length = checked(HeaderByteLength + LengthPrefixByteLength + tagBytes.Length);
        foreach (ReadOnlyMemory<byte> field in fields)
        {
            length = checked(length + LengthPrefixByteLength + field.Length);
        }

        byte[] bytes = new byte[length];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(VersionOffset), EncodingVersion);

        // The tag is the first length-prefixed field and is included in the stored count.
        uint fieldCount = checked((uint)fields.Length + 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(FieldCountOffset), fieldCount);
        int offset = HeaderByteLength;
        WriteField(bytes, ref offset, tagBytes);
        foreach (ReadOnlyMemory<byte> field in fields)
        {
            WriteField(bytes, ref offset, field.Span);
        }

        return bytes;
    }

    private static void WriteField(Span<byte> destination, ref int offset, ReadOnlySpan<byte> field)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination[offset..], checked((uint)field.Length));
        offset = checked(offset + LengthPrefixByteLength);
        field.CopyTo(destination[offset..]);
        offset = checked(offset + field.Length);
    }
}

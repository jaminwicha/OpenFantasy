using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// Canonical framing for the cargo world's authored version hashes. It writes the same version-one layout as the
/// core canonical encoder (internal to the core assembly): the <c>OFC\0</c> magic, encoding version one, the field
/// count including the tag, then each field with a big-endian 32-bit length prefix. Numbers are explicit big-endian
/// 64-bit fields, so no culture, record formatting or compiler-generated text reaches a hash.
/// </summary>
internal static class CargoCanonicalFrame
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] Text(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return StrictUtf8.GetBytes(value);
    }

    internal static byte[] Signed64(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    internal static byte[] Unsigned64(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    internal static byte[] Frame(string tag, IReadOnlyList<byte[]> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        byte[] tagBytes = Text(tag);
        int length = checked(16 + tagBytes.Length + fields.Sum(field => checked(4 + field.Length)));
        byte[] frame = new byte[length];
        "OFC\0"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), checked((uint)fields.Count + 1));
        int offset = 12;
        foreach (byte[] field in fields.Prepend(tagBytes))
        {
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), checked((uint)field.Length));
            field.CopyTo(frame, offset + 4);
            offset += 4 + field.Length;
        }
        return frame;
    }

    internal static ContentHash Hash(string tag, IReadOnlyList<byte[]> fields) => ContentHash.Compute(Frame(tag, fields));
}

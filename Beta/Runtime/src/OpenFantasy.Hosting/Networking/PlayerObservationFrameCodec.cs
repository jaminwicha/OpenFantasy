using System.Buffers.Binary;
using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Exact big-endian full/delta frames; replacement data is the typed view codec's canonical bytes.</summary>
public static class PlayerObservationFrameCodec
{
    public const int HeaderBytes = 56;
    public const int MaximumViewBytes = ObservationViews.PlayerViewCodec.MaximumBodyBytes;
    public const ushort Version = 1;

    public static byte[] Encode(PlayerObservationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Validate(frame.Revision, frame.BaseRevision, frame.ResultLength, frame.PrefixLength, frame.SuffixLength, frame.Replacement.Length);
        byte[] bytes = new byte[HeaderBytes + frame.Replacement.Length];
        "OFVO"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), frame.IsFull ? (ushort)0 : (ushort)1);
        frame.StreamId.WriteBytes(bytes.AsSpan(8));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(24), frame.Revision);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(32), frame.BaseRevision);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(40), frame.ResultLength);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(44), frame.PrefixLength);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(48), frame.SuffixLength);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(52), frame.Replacement.Length);
        frame.Replacement.CopyTo(bytes.AsSpan(HeaderBytes));
        return bytes;
    }

    public static PlayerObservationFrame Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes || bytes.Length > HeaderBytes + MaximumViewBytes || !bytes[..4].SequenceEqual("OFVO"u8)
            || BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) != Version)
            throw new FormatException("Invalid observation framing.");
        ushort kind = BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]);
        ulong revision = BinaryPrimitives.ReadUInt64BigEndian(bytes[24..]);
        ulong basis = BinaryPrimitives.ReadUInt64BigEndian(bytes[32..]);
        int length = BinaryPrimitives.ReadInt32BigEndian(bytes[40..]);
        int prefix = BinaryPrimitives.ReadInt32BigEndian(bytes[44..]);
        int suffix = BinaryPrimitives.ReadInt32BigEndian(bytes[48..]);
        int replacement = BinaryPrimitives.ReadInt32BigEndian(bytes[52..]);
        if (kind != (basis == 0 ? 0 : 1) || replacement != bytes.Length - HeaderBytes)
            throw new FormatException("Invalid observation kind or trailing data.");
        Validate(revision, basis, length, prefix, suffix, replacement);
        return new PlayerObservationFrame(EntityId.FromBytes(bytes.Slice(8, 16)), revision, basis, length, prefix, suffix, bytes[HeaderBytes..]);
    }

    private static void Validate(ulong revision, ulong basis, int length, int prefix, int suffix, int replacement)
    {
        if (revision == 0 || length is <= 0 or > MaximumViewBytes || prefix < 0 || suffix < 0 || replacement < 0
            || (long)prefix + suffix + replacement != length
            || basis == 0 && (prefix != 0 || suffix != 0)
            || basis != 0 && (basis == ulong.MaxValue || revision != basis + 1))
            throw new FormatException("Invalid observation bounds or revision relationship.");
    }
}

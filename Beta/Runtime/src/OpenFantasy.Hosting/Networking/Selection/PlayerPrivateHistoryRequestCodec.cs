using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

/// <summary>Fixed bounded cursor payload inside selected-input version 2.</summary>
internal static class PlayerPrivateHistoryRequestCodec
{
    internal static byte[] Encode(LocalPrivateHistoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try { request.Validate(); }
        catch (ArgumentException error) { throw new FormatException("Invalid private-history request.", error); }
        if (request.Before is not { } before) return [0];
        byte[] bytes = new byte[34];
        bytes[0] = 1;
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(1), before.ThroughRevision);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(9), before.ClosedAtSequence);
        bytes[17] = (byte)before.Kind;
        before.SubjectId.WriteBytes(bytes.AsSpan(18));
        return bytes;
    }

    internal static LocalPrivateHistoryPageRequest Decode(ReadOnlySpan<byte> bytes)
    {
        LocalPrivateHistoryPageRequest result;
        if (bytes.Length == 1 && bytes[0] == 0) result = new();
        else if (bytes.Length == 34 && bytes[0] == 1)
            result = new(new(BinaryPrimitives.ReadUInt64BigEndian(bytes[1..]),
                BinaryPrimitives.ReadUInt64BigEndian(bytes[9..]), (LocalPrivateHistoryKind)bytes[17],
                EntityId.FromBytes(bytes[18..])));
        else throw new FormatException("Invalid private-history cursor payload.");
        if (!bytes.SequenceEqual(Encode(result))) throw new FormatException("Noncanonical private-history request.");
        return result;
    }
}

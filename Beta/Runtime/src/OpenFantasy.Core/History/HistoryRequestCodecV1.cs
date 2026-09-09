using System.Buffers.Binary;

namespace OpenFantasy.Core.History;

/// <summary>Canonical, bounded interchange for exact history request metadata.</summary>
public static class HistoryRequestCodecV1
{
    public const int MaximumRequestByteLength = 202;

    private const string ContractTag = "history.request.v1";
    private const uint FieldCount = 12;
    private const int InstantByteLength = sizeof(long) + sizeof(uint);

    public static byte[] Encode(HistoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        HistoryAddress address = request.Address;
        HistoryBoundary boundary = request.Boundary;
        byte[] cursorBytes = boundary.CommitCursor is ulong cursor
            ? CanonicalEncodingV1.Unsigned64(cursor)
            : [];

        return CanonicalEncodingV1.Frame(ContractTag,
            address.WorldId.ToByteArray(), address.BranchId.ToByteArray(), address.RealmId.ToByteArray(),
            Signed64(address.XSubunits), Signed64(address.YSubunits), Signed64(address.ZSubunits),
            InstantBytes(boundary.HistoricalOrigin), InstantBytes(boundary.KnownHead),
            InstantBytes(boundary.RequestedInstant),
            CanonicalEncodingV1.Unsigned64(boundary.PinnedHeadRevision), cursorBytes);
    }

    public static HistoryRequest Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new HistoryRequestReader(bytes, MaximumRequestByteLength, FieldCount);
        reader.ExpectTag("history.request.v1"u8);
        var address = new HistoryAddress(reader.ReadIdentity(), reader.ReadIdentity(), reader.ReadIdentity(),
            reader.ReadSigned64(), reader.ReadSigned64(), reader.ReadSigned64());
        WorldInstant origin = ReadInstant(ref reader);
        WorldInstant head = ReadInstant(ref reader);
        WorldInstant requested = ReadInstant(ref reader);
        ulong revision = reader.ReadUnsigned64();
        ReadOnlySpan<byte> cursorBytes = reader.ReadField(sizeof(ulong));
        ulong? cursor = cursorBytes.Length switch
        {
            0 => null,
            sizeof(ulong) => BinaryPrimitives.ReadUInt64BigEndian(cursorBytes),
            _ => throw new FormatException("The optional commit cursor must be empty or eight bytes.")
        };
        reader.Complete();

        try
        {
            return new HistoryRequest(address, new HistoryBoundary(origin, head, requested, revision, cursor));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException("The history boundary is invalid.", exception);
        }
    }

    private static byte[] Signed64(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] InstantBytes(WorldInstant instant)
    {
        byte[] bytes = new byte[InstantByteLength];
        BinaryPrimitives.WriteInt64BigEndian(bytes, instant.Days);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(sizeof(long)), (uint)instant.MillisecondsWithinDay);
        return bytes;
    }

    private static WorldInstant ReadInstant(ref HistoryRequestReader reader)
    {
        ReadOnlySpan<byte> bytes = reader.ReadExactField(InstantByteLength);
        long days = BinaryPrimitives.ReadInt64BigEndian(bytes);
        uint milliseconds = BinaryPrimitives.ReadUInt32BigEndian(bytes[sizeof(long)..]);
        if (milliseconds >= WorldInstant.MillisecondsPerDay)
        {
            throw new FormatException("The world instant contains a non-normalized millisecond value.");
        }

        return new WorldInstant(days, (int)milliseconds);
    }
}

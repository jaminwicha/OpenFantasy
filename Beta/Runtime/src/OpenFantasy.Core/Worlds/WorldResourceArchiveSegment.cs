using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Detached canonical bytes of one complete closing resource boundary. Structural validation and
/// linked decoding do not replace the host's authentication of genesis and accepted journal commands.
/// </summary>
public sealed class WorldResourceArchiveSegment
{
    private readonly byte[] _bytes;

    private WorldResourceArchiveSegment(WorldResourceArchiveReference reference,
        WorldResourceArchiveAnchor anchor, byte[] bytes)
    {
        Reference = reference;
        Anchor = anchor;
        _bytes = bytes;
    }

    public WorldResourceArchiveReference Reference { get; }
    public WorldResourceArchiveAnchor Anchor { get; }

    public static WorldResourceArchiveSegment Seal(WorldSnapshot boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (boundary.Care is null || boundary.ResourceHistory is not { } resources
            || resources.History.Batches.Count == 0 || boundary.PendingExcavation is not null)
        {
            throw new ArgumentException("Sealing requires a complete care world with nonempty resource history and no pending excavation.", nameof(boundary));
        }
        byte[] bytes = WorldSnapshotCodec.Encode(boundary);
        if (bytes.Length == 0 || bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The complete archive boundary exceeds its canonical storage envelope.", nameof(boundary));
        }
        ulong ordinal = resources.ArchiveAnchor is { } previous ? checked(previous.Reference.Ordinal + 1) : 0;
        var reference = new WorldResourceArchiveReference(boundary.WorldId, boundary.BranchId, ordinal,
            boundary.Revision, ContentHash.Compute(bytes));
        return new(reference, new WorldResourceArchiveAnchor(reference, boundary), bytes);
    }

    public byte[] Encode() => (byte[])_bytes.Clone();

    public static WorldResourceArchiveSegment Decode(WorldResourceArchiveReference expected,
        ReadOnlySpan<byte> bytes, WorldResourceArchiveAnchor? previousAnchor = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (bytes.IsEmpty || bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new FormatException("The resource archive exceeds its canonical storage envelope.");
        }
        if (ContentHash.Compute(bytes) != expected.BoundaryHash)
        {
            throw new FormatException("The resource archive does not match its requested closing-boundary hash.");
        }
        try
        {
            var restored = WorldSnapshotCodec.Decode(bytes, previousAnchor);
            var segment = Seal(restored);
            if (segment.Reference != expected || !segment._bytes.AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The resource archive does not match its exact reference and canonical closing boundary.");
            }
            return segment;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The resource archive violates its sealed-boundary contract.", error);
        }
    }
}

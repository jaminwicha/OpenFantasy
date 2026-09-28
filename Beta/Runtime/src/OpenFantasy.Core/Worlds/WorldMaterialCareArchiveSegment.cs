using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Detached canonical bytes of one complete coordinated closing root, with the bounded anchor derived
/// from those actual owners. Structural decoding does not replace the host's authentication of
/// genesis, accepted journal commands and retrieved storage.
/// </summary>
public sealed class WorldMaterialCareArchiveSegment
{
    private readonly byte[] _bytes;

    private WorldMaterialCareArchiveSegment(WorldMaterialCareArchiveReference reference,
        WorldMaterialCareArchiveAnchor anchor, WorldSnapshot continued, byte[] bytes)
    {
        Reference = reference;
        Anchor = anchor;
        Continued = continued;
        _bytes = bytes;
    }

    public WorldMaterialCareArchiveReference Reference { get; }
    public WorldMaterialCareArchiveAnchor Anchor { get; }

    /// <summary>The rebased successor of the closing root at the same revision, time, work and material.</summary>
    public WorldSnapshot Continued { get; }

    public static WorldMaterialCareArchiveSegment Seal(WorldSnapshot boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (boundary.Coordination is not { } coordination || boundary.PendingExcavation is not null)
        {
            throw new ArgumentException("Sealing requires the explicit coordinated world without a pending interval.", nameof(boundary));
        }
        byte[] bytes = WorldSnapshotCodec.Encode(boundary);
        if (bytes.Length == 0 || bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The complete archive boundary exceeds its canonical storage envelope.", nameof(boundary));
        }
        ulong ordinal = coordination.Anchor is { } previous ? checked(previous.Reference.Ordinal + 1) : 0;
        var reference = new WorldMaterialCareArchiveReference(boundary.WorldId, boundary.BranchId, ordinal,
            boundary.Revision, ContentHash.Compute(bytes));
        var (continued, anchor) = WorldMaterialCareContinuation.Rebase(boundary, reference);
        return new(reference, anchor, continued, bytes);
    }

    public byte[] Encode() => (byte[])_bytes.Clone();

    public static WorldMaterialCareArchiveSegment Decode(WorldMaterialCareArchiveReference expected,
        ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? previousAnchor = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (bytes.IsEmpty || bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new FormatException("The coordinated archive exceeds its canonical storage envelope.");
        }
        if (ContentHash.Compute(bytes) != expected.BoundaryHash)
        {
            throw new FormatException("The coordinated archive does not match its requested closing-boundary hash.");
        }
        try
        {
            var restored = WorldSnapshotCodec.DecodeCoordinated(bytes, previousAnchor);
            var segment = Seal(restored);
            if (segment.Reference != expected || !segment._bytes.AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The coordinated archive does not match its exact reference and canonical closing boundary.");
            }
            return segment;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The coordinated archive violates its sealed-boundary contract.", error);
        }
    }
}

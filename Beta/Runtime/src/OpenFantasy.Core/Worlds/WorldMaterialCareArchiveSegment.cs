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
        WorldMaterialCareArchiveAnchor anchor, WorldSnapshot continued, byte[] bytes,
        WorldProposalTransition? transition = null)
    {
        Reference = reference;
        Anchor = anchor;
        Continued = continued;
        Transition = transition;
        _bytes = bytes;
    }

    public WorldMaterialCareArchiveReference Reference { get; }
    public WorldMaterialCareArchiveAnchor Anchor { get; }

    /// <summary>The rebased successor of the closing root at the same revision, time, work and material.</summary>
    public WorldSnapshot Continued { get; }

    /// <summary>Non-null only for the one archive that closes a lived world under an explicit proposal transition.</summary>
    public WorldProposalTransition? Transition { get; }

    public static WorldMaterialCareArchiveSegment Seal(WorldSnapshot boundary) => Seal(boundary, null);

    /// <summary>
    /// Closes the exact lived coordinated head named by <paramref name="transition"/> and continues it
    /// under the saved-proposal interpretation. The closing bytes stay the ordinary profile-27 root.
    /// </summary>
    internal static WorldMaterialCareArchiveSegment SealTransition(WorldSnapshot boundary, WorldProposalTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return Seal(boundary, transition);
    }

    private static WorldMaterialCareArchiveSegment Seal(WorldSnapshot boundary, WorldProposalTransition? transition)
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
        if (transition is not null && (transition.ExpectedHeadHash != reference.BoundaryHash
            || transition.ExpectedRevision != reference.BoundarySequence || transition.TransitionOrdinal != ordinal
            || transition.WorldId != reference.WorldId || transition.BranchId != reference.BranchId))
        {
            throw new ArgumentException("A proposal transition closes only its exact named source head.", nameof(transition));
        }
        var (continued, anchor) = WorldMaterialCareContinuation.Rebase(boundary, reference, transition);
        return new(reference, anchor, continued, bytes, transition);
    }

    public byte[] Encode() => (byte[])_bytes.Clone();

    public static WorldMaterialCareArchiveSegment Decode(WorldMaterialCareArchiveReference expected,
        ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? previousAnchor = null) =>
        Decode(expected, bytes, previousAnchor, null);

    /// <summary>
    /// Decodes the one archive closed by an explicit proposal transition. The caller supplies the
    /// transition from its authenticated journal; the archive row alone cannot select it.
    /// </summary>
    public static WorldMaterialCareArchiveSegment DecodeTransition(WorldMaterialCareArchiveReference expected,
        ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? previousAnchor, WorldProposalTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return Decode(expected, bytes, previousAnchor, transition);
    }

    private static WorldMaterialCareArchiveSegment Decode(WorldMaterialCareArchiveReference expected,
        ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? previousAnchor, WorldProposalTransition? transition)
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
            if (transition is not null && (restored.ProfileVersion != WorldProposalTransition.SourceProfileVersion
                || restored.AdmissionVersion != transition.SourceAdmissionVersion
                || restored.AdvancingSteps != transition.ExpectedAdvancingSteps
                || restored.Coordination!.Anchor?.ContentHash != transition.ExpectedAnchorHash))
            {
                throw new FormatException("The transition archive does not close its exact named source head.");
            }
            var segment = Seal(restored, transition);
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

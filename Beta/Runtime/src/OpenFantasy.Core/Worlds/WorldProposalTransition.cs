using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact request to continue one lived coordinated world (profile 27) under the saved-proposal
/// interpretation (profile 30). It pins the complete source head and its latest joint anchor; it
/// carries no stock, time, consent grant or control change, and it never names another profile.
/// </summary>
public sealed class WorldProposalTransition
{
    /// <summary>The only source interpretation this request can name.</summary>
    public const ushort SourceProfileVersion = 27;

    /// <summary>The only target interpretation this request can name.</summary>
    public const ushort TargetProfileVersion = 30;

    public WorldProposalTransition(EntityId causeId, EntityId worldId, EntityId branchId, ulong expectedRevision,
        ulong expectedAdvancingSteps, ContentHash expectedHeadHash, ContentHash sourceAdmissionVersion,
        ContentHash? expectedAnchorHash, ulong transitionOrdinal)
    {
        ArgumentNullException.ThrowIfNull(expectedHeadHash);
        ArgumentNullException.ThrowIfNull(sourceAdmissionVersion);
        if (worldId == branchId || causeId == worldId || causeId == branchId
            || expectedAdvancingSteps > expectedRevision || expectedRevision == ulong.MaxValue
            || (expectedAnchorHash is null) != (transitionOrdinal == 0))
        {
            throw new ArgumentException("A proposal transition requires an exact source head, scope and archive ordinal.");
        }
        CauseId = causeId;
        WorldId = worldId;
        BranchId = branchId;
        ExpectedRevision = expectedRevision;
        ExpectedAdvancingSteps = expectedAdvancingSteps;
        ExpectedHeadHash = expectedHeadHash;
        SourceAdmissionVersion = sourceAdmissionVersion;
        ExpectedAnchorHash = expectedAnchorHash;
        TransitionOrdinal = transitionOrdinal;
    }

    public EntityId CauseId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }

    /// <summary>Revision of the source head; the transition closes it and publishes revision + 1.</summary>
    public ulong ExpectedRevision { get; }
    public ulong ExpectedAdvancingSteps { get; }
    public ContentHash ExpectedHeadHash { get; }
    public ContentHash SourceAdmissionVersion { get; }

    /// <summary>The source head's latest joint anchor, or null when the source has never cut.</summary>
    public ContentHash? ExpectedAnchorHash { get; }

    /// <summary>Ordinal of the joint archive that closes the source head.</summary>
    public ulong TransitionOrdinal { get; }

    /// <summary>Digest of the exact canonical request; the transition anchor binds it.</summary>
    public ContentHash ContentHash => ContentHash.Compute(WorldProposalTransitionCodecV1.Encode(this));

    /// <summary>Pins every field from one actual source head. The processor still checks that head.</summary>
    public static WorldProposalTransition ForHead(WorldSnapshot head, EntityId causeId)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (head.ProfileVersion != SourceProfileVersion || head.Coordination is not { } coordination)
        {
            throw new ArgumentException("Only a lived coordinated world can name this transition.", nameof(head));
        }
        WorldMaterialCareArchiveAnchor? anchor = coordination.Anchor;
        return new(causeId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.ContentHash,
            head.AdmissionVersion, anchor?.ContentHash, anchor is null ? 0 : checked(anchor.Reference.Ordinal + 1));
    }
}

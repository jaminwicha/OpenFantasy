using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Exact-head request to enable collective withdrawal without replacing lived history.</summary>
public sealed class WorldPartyWithdrawalTransition
{
    /// <summary>The only source interpretation this request can name.</summary>
    public const ushort SourceProfileVersion = 40;

    /// <summary>The only target interpretation this request can name.</summary>
    public const ushort TargetProfileVersion = 41;

    public WorldPartyWithdrawalTransition(EntityId causeId, EntityId worldId, EntityId branchId, ulong expectedRevision,
        ulong expectedAdvancingSteps, ContentHash expectedHeadHash, ContentHash sourceAdmissionVersion,
        ContentHash? expectedAnchorHash)
    {
        ArgumentNullException.ThrowIfNull(expectedHeadHash);
        ArgumentNullException.ThrowIfNull(sourceAdmissionVersion);
        if (worldId == branchId || causeId == worldId || causeId == branchId
            || expectedAdvancingSteps > expectedRevision || expectedRevision == ulong.MaxValue)
        {
            throw new ArgumentException("A withdrawal transition requires an exact source head, scope and bounded revision.");
        }
        CauseId = causeId;
        WorldId = worldId;
        BranchId = branchId;
        ExpectedRevision = expectedRevision;
        ExpectedAdvancingSteps = expectedAdvancingSteps;
        ExpectedHeadHash = expectedHeadHash;
        SourceAdmissionVersion = sourceAdmissionVersion;
        ExpectedAnchorHash = expectedAnchorHash;
    }

    public EntityId CauseId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }

    /// <summary>Revision of the source head; the transition publishes revision + 1 without advancing time.</summary>
    public ulong ExpectedRevision { get; }
    public ulong ExpectedAdvancingSteps { get; }
    public ContentHash ExpectedHeadHash { get; }
    public ContentHash SourceAdmissionVersion { get; }

    /// <summary>The source head's latest joint anchor, or null when the source has never cut.</summary>
    public ContentHash? ExpectedAnchorHash { get; }

    /// <summary>Digest of the exact canonical request; the journal binds it.</summary>
    public ContentHash ContentHash => ContentHash.Compute(WorldPartyWithdrawalTransitionCodecV1.Encode(this));

    /// <summary>Pins every field from one actual source head. The processor still checks that head.</summary>
    public static WorldPartyWithdrawalTransition ForHead(WorldSnapshot head, EntityId causeId)
    {
        ArgumentNullException.ThrowIfNull(head);
        if (head.ProfileVersion != SourceProfileVersion || head.Coordination is not { } coordination)
        {
            throw new ArgumentException("Only an effective-control world can name this transition.", nameof(head));
        }
        WorldMaterialCareArchiveAnchor? anchor = coordination.Anchor;
        return new(causeId, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, head.ContentHash,
            head.AdmissionVersion, anchor?.ContentHash);
    }
}

using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>An exact profile-three head upgrade request. It does not contain or replace the retained inventory.</summary>
public sealed class WorldQuestMigration
{
    public WorldQuestMigration(EntityId causeId, EntityId worldId, EntityId branchId, ulong expectedRevision,
        ulong expectedAdvancingSteps, ContentHash expectedHeadHash, ContentHash sourceAdmissionVersion,
        ContentHash targetAdmissionVersion, WorldQuestAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(expectedHeadHash);
        ArgumentNullException.ThrowIfNull(sourceAdmissionVersion);
        ArgumentNullException.ThrowIfNull(targetAdmissionVersion);
        ArgumentNullException.ThrowIfNull(admission);
        if (worldId == branchId || causeId == worldId || causeId == branchId
            || admission.WorldId != worldId || admission.BranchId != branchId
            || expectedAdvancingSteps > expectedRevision
            || targetAdmissionVersion != ComputeTargetAdmissionVersion(sourceAdmissionVersion, admission))
        {
            throw new ArgumentException("Quest migration requires exact source and target pins in the retained world scope.");
        }
        CauseId = causeId;
        WorldId = worldId;
        BranchId = branchId;
        ExpectedRevision = expectedRevision;
        ExpectedAdvancingSteps = expectedAdvancingSteps;
        ExpectedHeadHash = expectedHeadHash;
        SourceAdmissionVersion = sourceAdmissionVersion;
        TargetAdmissionVersion = targetAdmissionVersion;
        Admission = admission;
    }

    public EntityId CauseId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong ExpectedRevision { get; }
    public ulong ExpectedAdvancingSteps { get; }
    public ContentHash ExpectedHeadHash { get; }
    public ContentHash SourceAdmissionVersion { get; }
    public ContentHash TargetAdmissionVersion { get; }
    public WorldQuestAdmission Admission { get; }

    public static ContentHash ComputeTargetAdmissionVersion(ContentHash sourceAdmissionVersion, WorldQuestAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(sourceAdmissionVersion);
        ArgumentNullException.ThrowIfNull(admission);
        return ContentHash.Compute(CanonicalEncodingV1.Frame("world.quest-admission-version", CanonicalEncodingV1.Unsigned64(1),
            sourceAdmissionVersion.ToByteArray(), WorldQuestMigrationCodecV1.EncodeAdmission(admission)));
    }
}

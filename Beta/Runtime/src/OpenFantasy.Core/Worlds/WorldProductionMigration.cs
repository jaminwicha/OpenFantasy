using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>A pinned profile upgrade, separate from actor actions and advancing simulation time.</summary>
public sealed class WorldProductionMigration
{
    public WorldProductionMigration(EntityId causeId, EntityId worldId, EntityId branchId, ulong expectedRevision,
        ulong expectedAdvancingSteps, ContentHash expectedHeadHash, ContentHash sourceAdmissionVersion,
        ContentHash targetAdmissionVersion, ProductionSnapshot production)
    {
        ArgumentNullException.ThrowIfNull(expectedHeadHash);
        ArgumentNullException.ThrowIfNull(sourceAdmissionVersion);
        ArgumentNullException.ThrowIfNull(targetAdmissionVersion);
        ArgumentNullException.ThrowIfNull(production);
        if (worldId == branchId || causeId == worldId || causeId == branchId
            || production.WorldId != worldId || production.BranchId != branchId || production.Revision != 0
            || production.Batches.Count != 0 || production.Lots.Count != 0 || production.Recipes.Count == 0
            || expectedAdvancingSteps > expectedRevision
            || targetAdmissionVersion != ComputeTargetAdmissionVersion(sourceAdmissionVersion, production))
        {
            throw new ArgumentException("Production migration requires a pinned empty ledger in the retained world scope.");
        }
        CauseId = causeId;
        WorldId = worldId;
        BranchId = branchId;
        ExpectedRevision = expectedRevision;
        ExpectedAdvancingSteps = expectedAdvancingSteps;
        ExpectedHeadHash = expectedHeadHash;
        SourceAdmissionVersion = sourceAdmissionVersion;
        TargetAdmissionVersion = targetAdmissionVersion;
        Production = production;
    }

    public EntityId CauseId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong ExpectedRevision { get; }
    public ulong ExpectedAdvancingSteps { get; }
    public ContentHash ExpectedHeadHash { get; }
    public ContentHash SourceAdmissionVersion { get; }
    public ContentHash TargetAdmissionVersion { get; }
    public ProductionSnapshot Production { get; }

    public static ContentHash ComputeTargetAdmissionVersion(ContentHash sourceAdmissionVersion, ProductionSnapshot production)
    {
        ArgumentNullException.ThrowIfNull(sourceAdmissionVersion);
        ArgumentNullException.ThrowIfNull(production);
        return ContentHash.Compute(CanonicalEncodingV1.Frame("world.production-admission", CanonicalEncodingV1.Unsigned64(1),
            sourceAdmissionVersion.ToByteArray(), ProductionCodecV1.Encode(production)));
    }
}

using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>
/// One complete material candidate or the untouched originals, never a partial state. Only a
/// durable caller may commit the candidate and publish its result.
/// </summary>
public sealed class ConsentedMaterialTransferPlan
{
    internal ConsentedMaterialTransferPlan(CountedAuthorityState originalInventory, ProductionSnapshot originalProduction,
        CountedAuthorityState candidateInventory, ProductionSnapshot candidateProduction,
        ConsentedMaterialApproval approval, WorldActionRejection rejection, EntityId? movedLotId)
    {
        if ((rejection == WorldActionRejection.None) != movedLotId.HasValue)
        {
            throw new ArgumentException("Exactly the applied plans identify the lot that changed custody.",
                nameof(movedLotId));
        }

        BaseInventoryHash = originalInventory.ContentHash;
        BaseProductionHash = originalProduction.ContentHash;
        CandidateInventory = candidateInventory;
        CandidateProduction = candidateProduction;
        Approval = approval;
        Rejection = rejection;
        MovedLotId = movedLotId;
    }

    public ContentHash BaseInventoryHash { get; }
    public ContentHash BaseProductionHash { get; }
    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public ConsentedMaterialApproval Approval { get; }
    public WorldActionRejection Rejection { get; }
    public bool IsApplied => Rejection == WorldActionRejection.None;

    /// <summary>The whole original lot, or the child spawned for a partial amount; absent when rejected.</summary>
    public EntityId? MovedLotId { get; }

    /// <summary>
    /// Returns both candidates only for the exact complete owners that were decided, including
    /// controls, access policies, capacities, recipes and ordered history. Any other current
    /// inventory or production makes this plan stale.
    /// </summary>
    public (CountedAuthorityState Inventory, ProductionSnapshot Production) ApplyTo(
        CountedAuthorityState currentInventory, ProductionSnapshot currentProduction)
    {
        ArgumentNullException.ThrowIfNull(currentInventory);
        ArgumentNullException.ThrowIfNull(currentProduction);
        if (!IsApplied)
        {
            throw new InvalidOperationException("A rejected plan has no candidate to apply.");
        }

        if (currentInventory.ContentHash != BaseInventoryHash || currentProduction.ContentHash != BaseProductionHash)
        {
            throw new CustodyConflictException(CustodyConflict.StalePlan);
        }

        return (CandidateInventory, CandidateProduction);
    }
}

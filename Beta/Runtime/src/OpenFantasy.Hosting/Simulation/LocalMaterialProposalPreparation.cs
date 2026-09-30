using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>One exact available lot in the controlled actor's own storage, not a grouped item total.</summary>
public sealed record LocalMaterialProposalLot(ConsentedMaterialLotKind Kind, EntityId LotId,
    CountedItemDefinition Definition, ulong Revision, ulong Quantity);

/// <summary>
/// An immutable, bounded creation snapshot. Captures exact terms without retaining the world root,
/// selecting a replacement lot, reserving stock, granting consent or publishing a step.
/// </summary>
public sealed class LocalMaterialProposalPreparation
{
    public const ulong OfferLifetimeSteps = 1200;
    private readonly EntityId _worldId;
    private readonly EntityId _branchId;
    private readonly EntityId _senderActorId;
    private readonly EntityId _sourceCustodianId;
    private readonly IReadOnlyDictionary<EntityId, EntityId> _destinations;

    private LocalMaterialProposalPreparation(WorldSnapshot state, EntityId senderActorId)
    {
        _worldId = state.WorldId;
        _branchId = state.BranchId;
        _senderActorId = senderActorId;
        _sourceCustodianId = state.GetResident(senderActorId).InventoryCustodianId;
        // One nominal minute at the native 20 cutoffs/second leaves time for deliberate inspection.
        // This is advancing time, not wall time: holds do not spend the captured lifetime.
        ExpiresAtAdvancingStep = checked(state.AdvancingSteps + 1 + OfferLifetimeSteps);
        _destinations = state.Residents.Where(actor => actor.ActorId != senderActorId
                && actor.InventoryCustodianId != _sourceCustodianId
                && state.Inventory.Policies.Any(policy => policy.OwnerActorId == actor.ActorId
                    && policy.CustodianId == actor.InventoryCustodianId))
            .ToDictionary(actor => actor.ActorId, actor => actor.InventoryCustodianId);
        RecipientActorIds = Array.AsReadOnly(_destinations.Keys.Order().ToArray());
        Lots = Array.AsReadOnly(state.Inventory.Custody.Lots
            .Where(lot => lot.CustodianId == _sourceCustodianId
                && lot.Protection == LotProtection.None && lot.ReservedBy is null)
            .Select(lot => new LocalMaterialProposalLot(ConsentedMaterialLotKind.CountedSource,
                lot.LotId, lot.Definition, lot.Revision, lot.Quantity))
            .Concat(state.Production!.Lots.Where(lot => lot.CustodianId == _sourceCustodianId
                && lot.Protection == LotProtection.None && lot.ReservedBy is null)
                .Select(lot => new LocalMaterialProposalLot(ConsentedMaterialLotKind.Produced,
                    lot.LotId, lot.Definition, lot.Revision, lot.Quantity)))
            .OrderBy(lot => lot.Kind).ThenBy(lot => lot.LotId).ToArray());
    }

    public ReadOnlyCollection<LocalMaterialProposalLot> Lots { get; }
    public ReadOnlyCollection<EntityId> RecipientActorIds { get; }
    public ulong ExpiresAtAdvancingStep { get; }

    /// <summary>
    /// Prepares only the explicit lot, recipient and positive quantity from this captured view.
    /// Missing or incompatible selections return nothing; authoritative submission rechecks the pin.
    /// The client keeps this intent while confirming it, without refreshing its expiry or terms.
    /// </summary>
    public ProposeMaterialIntent? Prepare(ConsentedMaterialLotKind kind, EntityId lotId,
        EntityId recipientActorId, ulong quantity)
    {
        LocalMaterialProposalLot? lot = Lots.FirstOrDefault(value => value.Kind == kind && value.LotId == lotId);
        if (lot is null || quantity == 0 || quantity > lot.Quantity
            || !_destinations.TryGetValue(recipientActorId, out EntityId destination))
        {
            return null;
        }
        return new ProposeMaterialIntent(new ConsentedMaterialTerms(_worldId, _branchId, _senderActorId,
            recipientActorId, kind, lot.LotId, lot.Definition, _sourceCustodianId, destination, quantity,
            lot.Revision), ExpiresAtAdvancingStep);
    }

    /// <summary>
    /// Checks a previously captured creation against this current public preparation without renewing
    /// its expiry. The published expiry minus the fixed lifetime identifies the next creation step;
    /// an offer must survive that step. This is presentation advice, not authority admission.
    /// </summary>
    public bool CanStillPropose(ProposeMaterialIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ConsentedMaterialTerms terms = intent.Terms;
        ProposeMaterialIntent? current = Prepare(terms.LotKind, terms.LotId, terms.RecipientActorId, terms.Quantity);
        return current is not null && current.Terms == terms
            && intent.ExpiresAtAdvancingStep > ExpiresAtAdvancingStep - OfferLifetimeSteps
            && intent.ExpiresAtAdvancingStep <= ExpiresAtAdvancingStep;
    }

    internal static LocalMaterialProposalPreparation? Project(WorldSnapshot state, EntityId playerActorId)
    {
        if (state.ProfileVersion != 30 || state.HasReadingHold || state.PendingExcavation is not null
            || state.Care?.Pending is not null || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration
            || state.Coordination!.Proposals!.CanCreate != true
            || state.AdvancingSteps > ulong.MaxValue - 1 - OfferLifetimeSteps
            || !state.Inventory.Policies.Any(policy => policy.OwnerActorId == playerActorId
                && policy.CustodianId == state.GetResident(playerActorId).InventoryCustodianId))
        {
            return null;
        }
        return new(state, playerActorId);
    }
}

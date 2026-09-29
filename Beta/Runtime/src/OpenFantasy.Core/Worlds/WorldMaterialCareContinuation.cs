using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression.ResourceHistory;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Reserves the whole next batch and one future remedy across the material, care and resource
/// windows, then closes the complete root before any command of that batch is interpreted.
/// </summary>
internal static class WorldMaterialCareContinuation
{
    // One further use, treatment or craft stays selectable after every batch, so a presented offer
    // never waits for an intent that a full window would prevent selecting.
    private const int ReservedOperations = 1;
    private const int ReservedResourceEffects = 2;

    internal static (WorldSnapshot State, WorldMaterialCareArchiveSegment? Sealed) Prepare(WorldSnapshot world, WorldStepBatch batch)
    {
        if (world.Coordination is null || !RequiresCut(world, batch))
        {
            return (world, null);
        }
        var segment = WorldMaterialCareArchiveSegment.Seal(world);
        return (segment.Continued, segment);
    }

    /// <summary>Changes representation only: no time, work, material, custody or resource effect.</summary>
    internal static (WorldSnapshot Continued, WorldMaterialCareArchiveAnchor Anchor) Rebase(WorldSnapshot closing,
        WorldMaterialCareArchiveReference reference)
    {
        if (closing.Coordination is not { } coordination || closing.Production is not { } closingProduction
            || closing.Care is not { } closingCare || closing.Reservoirs is not { } closingReservoirs
            || closing.LiquidCells is not { } closingLiquids || closing.Construction is not { } construction)
        {
            throw new ArgumentException("A coordinated cut requires the complete material, care and liquid world.", nameof(closing));
        }
        ProductionSnapshot production = closingProduction.ContinueWorld(closing.Inventory, construction,
            coordination.ConsentedMaterial is not null);
        var capacity = closingReservoirs.Capacity.PlanContinuation(production, CancellationToken.None);
        LiquidReservoirState reservoirs = closingReservoirs.PlanApply(capacity).ApplyTo(closingReservoirs);
        LiquidCellState liquids = closingLiquids.Rebind(reservoirs);
        CareSnapshot care = closingCare;
        if (CareContinuationContext.CanCapture(closingCare))
        {
            CareContinuationContext context = CareContinuationContext.Capture(closingCare);
            care = context.Continue();
            context.ValidateClosure(closingCare, care);
        }
        var anchor = new WorldMaterialCareArchiveAnchor(reference, closing, production.Continuation!,
            care.Continuation!, GeographyProductionDependencies.ForCompleteWorld(reservoirs));
        var resources = new WorldResourceHistory(closing.AdmissionVersion,
            new ActorResourceHistory(closing.Progression!, closing.Revision, []), [], coordinatedAnchor: anchor);
        return (closing.WithCoordinatedCut(production, reservoirs, liquids, care, resources,
            coordination.WithAnchor(anchor)), anchor);
    }

    private static bool RequiresCut(WorldSnapshot world, WorldStepBatch batch)
    {
        ProductionSnapshot production = world.Production!;
        CareSnapshot care = world.Care!;
        ActorResourceHistory history = world.ResourceHistory!.History;
        int uses = batch.Actions.Count(command => command.Intent is UseRestorativeItemIntent);
        int treatments = batch.Actions.Count(command => command.Intent is AdministerRemedyIntent);
        int acknowledgments = batch.Actions.Count(command => command.Intent is AcknowledgeCareIntent);
        int crafts = batch.Actions.Count(command => command.Intent is CraftIntent or WorkConstructionIntent);
        (int batches, int effects) = WorldResourceContinuation.RequiredCapacity(batch);
        int retainedUses = care.Ordinary?.Uses.Count ?? 0;
        int retainedTransfers = world.Coordination!.ConsentedMaterial?.Transfers.Count ?? 0;
        int transfers = batch.Actions.Count(command => command.Intent is OfferMaterialIntent);
        int completedAcknowledgments = care.Acknowledgments.Count - (care.Pending is null ? 0 : 1);
        bool progressed = production.Batches.Count + production.ConsumedItems.Count + history.Batches.Count
            + care.Treatments.Count + completedAcknowledgments + retainedUses + retainedTransfers != 0;
        return progressed && (
            retainedTransfers + transfers + ReservedOperations > WorldConsentedMaterialHistory.MaximumTransfers
            || history.Batches.Count + batches + ReservedOperations > ActorResourceHistory.MaximumBatches
            || history.ChangeCount + effects + ReservedResourceEffects > ActorResourceHistory.MaximumChanges
            || production.Batches.Count + crafts + ReservedOperations > ProductionSnapshot.MaximumBatches
            || production.ConsumedItems.Count + uses + treatments + ReservedOperations > ProductionSnapshot.MaximumConsumedItems
            || care.Treatments.Count + treatments + ReservedOperations > CareSnapshot.MaximumTreatments
            || care.Acknowledgments.Count + acknowledgments + ReservedOperations > CareSnapshot.MaximumAcknowledgments
            || retainedUses + uses + ReservedOperations > OrdinaryCareSnapshot.MaximumUses);
    }
}

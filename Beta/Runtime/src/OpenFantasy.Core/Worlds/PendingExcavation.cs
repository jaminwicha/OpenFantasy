using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// One accepted excavation reserves its advancing interval while exact geometric work is unfinished.
/// The authoritative world remains stable; only terminal publication spends labor, material and time.
/// </summary>
public sealed class PendingExcavation
{
    private readonly PendingExcavationReservations _reservations;

    internal PendingExcavation(WorldActionCommand command, EntityId triggerCauseId, ulong basisRevision,
        ContentHash basisHash, ulong continuationCount, ConstructionSettlementResolution resolution,
        ConsequenceExecutionPolicy policy)
    {
        Command = command;
        TriggerCauseId = triggerCauseId;
        BasisRevision = basisRevision;
        BasisHash = basisHash;
        ContinuationCount = continuationCount;
        Resolution = resolution;
        Policy = policy;
        _reservations = new PendingExcavationReservations(command, triggerCauseId, basisRevision,
            MaximumContinuations(policy, resolution.MaximumTotalWorkUnits));
    }

    private PendingExcavation(PendingExcavation previous, ConstructionSettlementResolution resolution)
    {
        if (resolution.MaximumTotalWorkUnits != previous.Resolution.MaximumTotalWorkUnits)
        {
            throw new InvalidOperationException("Forward excavation work cannot change its reserved completion interval.");
        }
        Command = previous.Command;
        TriggerCauseId = previous.TriggerCauseId;
        BasisRevision = previous.BasisRevision;
        BasisHash = previous.BasisHash;
        ContinuationCount = checked(previous.ContinuationCount + 1);
        Resolution = resolution;
        Policy = previous.Policy;
        // The private forward path fixes every reservation input. Retain neither the previous
        // cursor nor its world; only the immutable publication set is shared.
        _reservations = previous._reservations;
    }

    public WorldActionCommand Command { get; }
    public EntityId TriggerCauseId { get; }
    public ulong BasisRevision { get; }
    public ContentHash BasisHash { get; }
    public ulong ContinuationCount { get; }
    public ConstructionSettlementResolution Resolution { get; }
    public EntityId ReservedSourceId => _reservations.SourceId;
    public ReadOnlyCollection<EntityId> ResultReservations => _reservations.Results;
    public ulong MaximumContinuationCount => MaximumContinuations(Policy, Resolution.MaximumTotalWorkUnits);
    public int MaximumTerminalOutcomesByteLength => Math.Max(
        WorldReceiptCodecV1.EncodeBatch([WorldActionReceipt.Create(Command,
            checked(BasisRevision + 1 + MaximumContinuationCount), WorldActionRejection.None)]).Length,
        WorldReceiptCodecV1.EncodeBatch([WorldActionReceipt.Create(Command,
            checked(BasisRevision + 1 + MaximumContinuationCount), WorldActionRejection.Unavailable)]).Length);
    internal ConsequenceExecutionPolicy Policy { get; }

    internal static ulong MaximumContinuations(ConsequenceExecutionPolicy policy,
        int maximumWorkUnits = ConstructionSettlementResolution.MaximumWorkUnits) => checked(
        1UL + ((ulong)maximumWorkUnits + (ulong)policy.WorkUnitsPerContinuation - 1)
            / (ulong)policy.WorkUnitsPerContinuation);

    internal static PendingExcavation? Begin(WorldSnapshot basis, WorldActionCommand command, EntityId cause)
    {
        var site = basis.Excavation!.Sites.Single(s => s.SiteId == ((ExcavateIntent)command.Intent).SiteId);
        ConstructionSettlementResolution resolution;
        try
        {
            var regions = ExcavationPlanner.CandidateRegions(basis.Space, site);
            resolution = basis.DescentMode == ConstructionDescentMode.EventOrdered
                ? ConstructionSettlementResolution.BeginEventOrdered(basis, regions)
                : basis.CoupledSettlementPolicy is not null
                ? ConstructionSettlementResolution.BeginCoupled(basis, regions)
                : ConstructionSettlementResolution.Begin(basis.Space, regions, basis.ConstructionPhysical!, basis.StructuralSettlementPolicy!);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            // Preserve the ordinary planner's terminal refusal for unrepresentable candidate geometry.
            // Publication-identity errors below are dispatch failures and must not take this fallback.
            return null;
        }
        return new(command, cause, basis.Revision, basis.ContentHash, 0, resolution, basis.ConsequencePolicy!);
    }

    internal PendingExcavation Advance() => new(this, Resolution.Advance(Policy.WorkUnitsPerContinuation));

    internal void ValidateForWorld(WorldSnapshot world)
    {
        if (world.ProfileVersion is not 16 and not 17 and not 18 || world.HasReadingHold || world.ConsequencePolicy!.ContentHash != Policy.ContentHash
            || Resolution.IsCoupled != (world.CoupledSettlementPolicy is not null)
            || Resolution.IsEventOrdered != (world.DescentMode == ConstructionDescentMode.EventOrdered)
            || !Resolution.IsEventOrdered && Resolution.MaximumTotalWorkUnits != (world.CoupledSettlementPolicy?.MaximumWorkUnits
                ?? ConstructionSettlementResolution.MaximumWorkUnits)
            || world.Revision != checked(BasisRevision + 1 + ContinuationCount)
            || ContinuationCount >= MaximumContinuationCount
            || ContinuationCount != 0 && Resolution.IsComplete
            || Resolution.CompletedWorkUnits != checked(ContinuationCount * (ulong)Policy.WorkUnitsPerContinuation))
        {
            throw new ArgumentException("The pending cursor does not match its reserved interval and exact commit boundary.");
        }
        var basis = world.WithPending(null, BasisRevision);
        if (basis.ContentHash != BasisHash || new ExcavationPlanner().ValidateRequest(basis, basis.Inventory,
                basis.Space, basis.Gathering!, basis.Excavation!, Command, true, false) != WorldActionRejection.None)
        {
            throw new ArgumentException("Pending excavation dependencies or accepted command were changed.");
        }
    }
}

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
        ulong continuationLimit = MaximumContinuations(policy);
        var results = new List<EntityId>();
        // Reserve both admissible outcomes at every possible terminal commit, without issuing them.
        for (ulong offset = 2; offset <= checked(continuationLimit + 1); offset++)
        {
            ulong sequence = checked(basisRevision + offset);
            results.Add(WorldActionReceipt.Create(command, sequence, WorldActionRejection.None).ResultId);
            results.Add(WorldActionReceipt.Create(command, sequence, WorldActionRejection.Unavailable).ResultId);
        }
        ResultReservations = results.AsReadOnly();
        EntityId[] identities = results.Append(ReservedSourceId).Append(command.CommandId).Append(triggerCauseId).ToArray();
        if (command.Intent is not ExcavateIntent || identities.Distinct().Count() != identities.Length)
        {
            throw new ArgumentException("A pending excavation needs unique command, cause and publication identities.");
        }
    }

    public WorldActionCommand Command { get; }
    public EntityId TriggerCauseId { get; }
    public ulong BasisRevision { get; }
    public ContentHash BasisHash { get; }
    public ulong ContinuationCount { get; }
    public ConstructionSettlementResolution Resolution { get; }
    public EntityId ReservedSourceId => EntityId.DeriveSpawned(Command.CommandId, 0);
    public ReadOnlyCollection<EntityId> ResultReservations { get; }
    public ulong MaximumContinuationCount => MaximumContinuations(Policy);
    public int MaximumTerminalOutcomesByteLength => Math.Max(
        WorldReceiptCodecV1.EncodeBatch([WorldActionReceipt.Create(Command,
            checked(BasisRevision + 1 + MaximumContinuationCount), WorldActionRejection.None)]).Length,
        WorldReceiptCodecV1.EncodeBatch([WorldActionReceipt.Create(Command,
            checked(BasisRevision + 1 + MaximumContinuationCount), WorldActionRejection.Unavailable)]).Length);
    internal ConsequenceExecutionPolicy Policy { get; }

    internal static ulong MaximumContinuations(ConsequenceExecutionPolicy policy) => checked(
        1UL + ((ulong)ConstructionSettlementResolution.MaximumWorkUnits + (ulong)policy.WorkUnitsPerContinuation - 1)
            / (ulong)policy.WorkUnitsPerContinuation);

    internal static PendingExcavation? Begin(WorldSnapshot basis, WorldActionCommand command, EntityId cause)
    {
        var site = basis.Excavation!.Sites.Single(s => s.SiteId == ((ExcavateIntent)command.Intent).SiteId);
        ConstructionSettlementResolution resolution;
        try
        {
            resolution = ConstructionSettlementResolution.Begin(basis.Space,
                ExcavationPlanner.CandidateRegions(basis.Space, site), basis.ConstructionPhysical!, basis.StructuralSettlementPolicy!);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            // Preserve the ordinary planner's terminal refusal for unrepresentable candidate geometry.
            // Publication-identity errors below are dispatch failures and must not take this fallback.
            return null;
        }
        return new(command, cause, basis.Revision, basis.ContentHash, 0, resolution, basis.ConsequencePolicy!);
    }

    internal PendingExcavation Advance() => new(Command, TriggerCauseId, BasisRevision, BasisHash,
        checked(ContinuationCount + 1), Resolution.Advance(Policy.WorkUnitsPerContinuation), Policy);

    internal void ValidateForWorld(WorldSnapshot world)
    {
        if (world.ProfileVersion != 16 || world.HasReadingHold || world.ConsequencePolicy!.ContentHash != Policy.ContentHash
            || world.Revision != checked(BasisRevision + 1 + ContinuationCount)
            || ContinuationCount >= MaximumContinuations(Policy)
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

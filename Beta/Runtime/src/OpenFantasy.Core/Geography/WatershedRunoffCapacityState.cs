using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Owns pending receiving obligations and gates every admitted custody/production successor.</summary>
public sealed class WatershedRunoffCapacityState
{
    public const int MaximumPending = 16;
    public const int MaximumClosed = 4096;
    private readonly Lazy<ContentHash> _contentHash;

    internal WatershedRunoffCapacityState(WatershedRunoffDomain domain, CountedAuthorityState inventory,
        ProductionSnapshot? production, ulong revision, IEnumerable<PreparedWatershedRunoff> pending,
        IEnumerable<EntityId> closed, RunoffMaterialContinuation? continuation = null)
    {
        Domain = domain;
        Inventory = inventory;
        Production = production;
        Revision = revision;
        ContinuationQualification = continuation;
        domain.ValidateAuthority(inventory, production, continuation);
        domain.ValidateReceivers(inventory, production);
        Pending = pending.Take(MaximumPending + 1).OrderBy(p => p.Request.ReservationId).ToList().AsReadOnly();
        Closed = closed.Take(MaximumClosed + 1).Order().ToList().AsReadOnly();
        if (Pending.Count > MaximumPending || Closed.Count > MaximumClosed
            || Pending.Select(p => p.Request.ReservationId).Distinct().Count() != Pending.Count
            || Closed.Distinct().Count() != Closed.Count || Closed.Any(id => !inventory.ReservationIds.Contains(id))
            || Pending.Any(p => Closed.Contains(p.Request.ReservationId)))
        {
            throw new ArgumentException("Runoff commitment identities must be bounded, admitted and exclusive.");
        }
        foreach (var runoff in Pending)
        {
            ValidateBasis(runoff);
        }
        Receivers = AggregateReceivers().ToList().AsReadOnly();
        _contentHash = new(() => ContentHash.Compute(Production?.Continuation?.SupportsReservedTransport == true ? WatershedRunoffCapacityCodecV4.Encode(this)
            : Production?.IsContinued == true ? WatershedRunoffCapacityCodecV3.Encode(this)
            : Production?.SupportsConsumption == true
            ? WatershedRunoffCapacityCodecV2.Encode(this) : WatershedRunoffCapacityCodecV1.Encode(this)));
    }

    public WatershedRunoffDomain Domain { get; }
    public CountedAuthorityState Inventory { get; }
    public ProductionSnapshot? Production { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<PreparedWatershedRunoff> Pending { get; }
    public ReadOnlyCollection<EntityId> Closed { get; }
    public ReadOnlyCollection<WatershedRunoffReceiverTotal> Receivers { get; }
    public ContentHash ContentHash => _contentHash.Value;
    internal RunoffMaterialContinuation? ContinuationQualification { get; }

    public static WatershedRunoffCapacityState Create(PreparedWatershedRunoff first)
    {
        ArgumentNullException.ThrowIfNull(first);
        return new(WatershedRunoffDomain.From(first.Request), first.Request.Inventory, first.Request.Production, 0, [first], [],
            first.Request.ContinuationQualification);
    }

    /// <summary>Creates another forcing request within this exact admitted domain and material context.</summary>
    public WatershedRunoffRequest CreateRequest(EntityId reservationId, WorldInstant start, WorldInstant end,
        IEnumerable<WatershedRunoffInput> inputs) =>
        new(Domain.Network, Domain.Mapping, Inventory, Production, reservationId, Domain.OwnerActorId,
            start, end, Domain.Material, inputs, Domain.Terminals, ContinuationQualification);

    public WatershedRunoffCapacityPlan PlanAdmit(PreparedWatershedRunoff runoff)
    {
        ArgumentNullException.ThrowIfNull(runoff);
        return new(this, new(Domain, Inventory, Production, checked(Revision + 1), Pending.Append(runoff), Closed, ContinuationQualification));
    }

    /// <summary>Releases capacity only. Held source stock retains its existing reservation metadata.</summary>
    public WatershedRunoffCapacityPlan PlanCancel(EntityId reservationId)
    {
        if (!Pending.Any(p => p.Request.ReservationId == reservationId))
        {
            throw new ArgumentException("Only an active forcing commitment can be canceled.");
        }
        return new(this, new(Domain, Inventory, Production, checked(Revision + 1),
            Pending.Where(p => p.Request.ReservationId != reservationId), Closed.Append(reservationId), ContinuationQualification));
    }

    /// <summary>Captures this exact closing material owner without advancing capacity or changing any pending obligation.</summary>
    public WatershedRunoffCapacityPlan PlanContinue(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PlanContinuation(RequireProduction().Continue(Inventory), cancellationToken);
    }

    /// <summary>Explicitly selects ordered material continuation from this owner's actual closing inventory and production.</summary>
    public WatershedRunoffCapacityPlan PlanContinueMaterial(GatheringSnapshot? gathering = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PlanContinuation(RequireProduction().ContinueMaterial(Inventory, gathering), cancellationToken);
    }

    /// <summary>Explicitly selects construction-capable continuation using its exact admitted material boundary.</summary>
    public WatershedRunoffCapacityPlan PlanContinueConstruction(MaterialConstructionState state,
        GatheringSnapshot? gathering = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PlanContinuation(RequireProduction().ContinueConstruction(Inventory, state, gathering), cancellationToken);
    }

    public WatershedRunoffCapacityPlan PlanReconcile(CountedAuthorityState inventory, ProductionSnapshot? production,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        cancellationToken.ThrowIfCancellationRequested();
        if (inventory.ContentHash == Inventory.ContentHash && production?.ContentHash == Production?.ContentHash)
        {
            return new(this, this);
        }
        if (inventory.Custody.Revision <= Inventory.Custody.Revision
            || Inventory.WithCustody(inventory.Custody).ContentHash != inventory.ContentHash)
        {
            throw new ArgumentException("Reconciliation requires advancing custody with unchanged scope and admission.");
        }
        ValidateProductionSuccessor(production);
        var refreshed = Reprepare(inventory, production, ContinuationQualification, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(this, new(Domain, inventory, production, checked(Revision + 1), refreshed, Closed, ContinuationQualification));
    }

    private ProductionSnapshot RequireProduction() => Production
        ?? throw new ArgumentException("Material continuation requires an explicit production owner.");

    internal WatershedRunoffCapacityPlan PlanContinuation(ProductionSnapshot production, CancellationToken cancellation)
    {
        var continuation = RunoffMaterialContinuation.Capture(this, production);
        var refreshed = Reprepare(Inventory, production, continuation, cancellation);
        var next = new WatershedRunoffCapacityState(Domain, Inventory, production, Revision, refreshed, Closed, continuation);
        if (next.Inventory.ContentHash != Inventory.ContentHash || next.Domain.ContentHash != Domain.ContentHash
            || next.Revision != Revision || production.Revision != Production!.Revision
            || production.SupportsConsumption != Production.SupportsConsumption
            || !next.Closed.SequenceEqual(Closed) || !next.Receivers.SequenceEqual(Receivers)
            || next.Pending.Count != Pending.Count)
        {
            throw new ArgumentException("A material cut must preserve exact capacity ownership and receiving projections.");
        }
        for (int index = 0; index < Pending.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var prior = Pending[index];
            var continued = next.Pending[index];
            if (prior.Request.ReservationId != continued.Request.ReservationId
                || prior.Request.Start != continued.Request.Start || prior.Request.End != continued.Request.End
                || !prior.Request.Inputs.SequenceEqual(continued.Request.Inputs)
                || !prior.Request.Terminals.SequenceEqual(continued.Request.Terminals)
                || prior.TotalInputUnits != continued.TotalInputUnits || prior.TotalInputVolume != continued.TotalInputVolume
                || !prior.Terminals.SequenceEqual(continued.Terminals) || !prior.Receivers.SequenceEqual(continued.Receivers))
            {
                throw new ArgumentException("A material cut cannot alter a pending forcing obligation.");
            }
            foreach (var basin in Domain.Network.Basins)
            {
                cancellation.ThrowIfCancellationRequested();
                foreach (var column in basin.Columns)
                {
                    if (prior.ContributingUnits(basin.BasinId, column.Index) != continued.ContributingUnits(basin.BasinId, column.Index))
                    {
                        throw new ArgumentException("A material cut cannot alter retained runoff contributions.");
                    }
                }
            }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(this, next);
    }

    private List<PreparedWatershedRunoff> Reprepare(CountedAuthorityState inventory, ProductionSnapshot? production,
        RunoffMaterialContinuation? continuation, CancellationToken cancellation)
    {
        var planner = new WatershedRunoffPlanner();
        var refreshed = new List<PreparedWatershedRunoff>();
        foreach (var pending in Pending)
        {
            cancellation.ThrowIfCancellationRequested();
            var request = pending.Request;
            refreshed.Add(planner.Prepare(new(Domain.Network, Domain.Mapping, inventory, production, request.ReservationId,
                Domain.OwnerActorId, request.Start, request.End, Domain.Material, request.Inputs, Domain.Terminals, continuation), cancellation));
        }
        return refreshed;
    }

    /// <summary>Internal completion publication after the transport planner moves the entire exact funding bill.</summary>
    internal WatershedRunoffCapacityPlan PlanComplete(EntityId reservationId, CountedAuthorityState inventory, ProductionSnapshot? production = null)
    {
        production ??= Production;
        if (Production?.IsContinued == true)
        {
            if (Production.Continuation?.SupportsReservedTransport != true || production?.MaterialHistory?.Operations.LastOrDefault()
                is not MaterialReservedTransportOperation delivery || delivery.Evidence.ReservationId != reservationId
                || delivery.Evidence.BeforeInventoryHash != Inventory.ContentHash
                || delivery.Evidence.BeforeProductionHash != Production.ContentHash
                || delivery.Evidence.AfterInventoryHash != inventory.ContentHash)
                throw new ArgumentException("Reserved completion requires its actual joined V7 delivery operation.");
            ValidateProductionSuccessor(production);
        }
        else if (production?.ContentHash != Production?.ContentHash)
            throw new ArgumentException("Inline transport cannot replace its complete production owner.");
        production?.ValidateForAuthority(inventory);
        if (!Pending.Any(p => p.Request.ReservationId == reservationId) || inventory.Custody.Revision <= Inventory.Custody.Revision
            || Inventory.WithCustody(inventory.Custody).ContentHash != inventory.ContentHash
            || inventory.Custody.Lots.Any(l => l.ReservedBy == reservationId))
        {
            throw new ArgumentException("Completion requires one active budget, advancing exact custody and no remaining source hold.");
        }
        var planner = new WatershedRunoffPlanner();
        var remaining = Pending.Where(p => p.Request.ReservationId != reservationId).Select(p => planner.Prepare(
            new(Domain.Network, Domain.Mapping, inventory, production, p.Request.ReservationId, Domain.OwnerActorId,
                p.Request.Start, p.Request.End, Domain.Material, p.Request.Inputs, Domain.Terminals, ContinuationQualification))).ToArray();
        return new(this, new(Domain, inventory, production, checked(Revision + 1), remaining, Closed.Append(reservationId), ContinuationQualification));
    }

    private void ValidateBasis(PreparedWatershedRunoff runoff)
    {
        var request = runoff.Request;
        if (request.Inventory.ContentHash != Inventory.ContentHash || request.Production?.ContentHash != Production?.ContentHash
            || request.Network.ComputeContentHash() != Domain.Network.ComputeContentHash() || request.Mapping.Version != Domain.Mapping.Version
            || request.OwnerActorId != Domain.OwnerActorId || request.Material.Item != Domain.Material.Item
            || request.Material.Physical.DefinitionId != Domain.Material.Physical.DefinitionId
            || request.Material.Physical.Version != Domain.Material.Physical.Version
            || !request.Terminals.SequenceEqual(Domain.Terminals))
        {
            throw new ArgumentException("Each commitment requires the owner's exact complete authority and fixed runoff domain.");
        }
    }

    private IEnumerable<WatershedRunoffReceiverTotal> AggregateReceivers()
    {
        foreach (var receiver in Domain.Terminals.GroupBy(t => t.CustodianId).OrderBy(g => g.Key))
        {
            ulong existing = Production?.GetCombinedStoredQuantity(Inventory, receiver.Key)
                ?? Inventory.Custody.GetStoredQuantity(receiver.Key);
            ulong held = Pending.SelectMany(p => p.Receivers).Where(r => r.CustodianId == receiver.Key)
                .Aggregate(0UL, (sum, r) => checked(sum + r.IncomingUnits));
            ulong projected = checked(existing + held);
            var volume = Domain.Material.Physical.VolumePerUnit.Multiply(projected);
            var capacity = receiver.First().Capacity;
            if (projected > Inventory.Custody.GetStorage(receiver.Key).UnitCapacity || volume.Value > capacity.Value)
            {
                throw new ArgumentException("Actual occupancy and all outstanding inflows exceed receiving capacity.");
            }
            yield return new(receiver.Key, existing, held, projected, volume, capacity);
        }
    }

    private void ValidateProductionSuccessor(ProductionSnapshot? next)
    {
        if (Production?.ContentHash == next?.ContentHash)
        {
            return;
        }
        if (Production is null || next is null)
        {
            throw new ArgumentException("Production must remain explicit, advance and preserve its admitted recipes.");
        }
        ProductionRetirementHistory.ValidateSuccessor(Production, next);
    }
}

using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
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
        IEnumerable<EntityId> closed)
    {
        Domain = domain;
        Inventory = inventory;
        Production = production;
        Revision = revision;
        domain.ValidateAuthority(inventory, production);
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
        _contentHash = new(() => ContentHash.Compute(WatershedRunoffCapacityCodecV1.Encode(this)));
    }

    public WatershedRunoffDomain Domain { get; }
    public CountedAuthorityState Inventory { get; }
    public ProductionSnapshot? Production { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<PreparedWatershedRunoff> Pending { get; }
    public ReadOnlyCollection<EntityId> Closed { get; }
    public ReadOnlyCollection<WatershedRunoffReceiverTotal> Receivers { get; }
    public ContentHash ContentHash => _contentHash.Value;

    public static WatershedRunoffCapacityState Create(PreparedWatershedRunoff first)
    {
        ArgumentNullException.ThrowIfNull(first);
        return new(WatershedRunoffDomain.From(first.Request), first.Request.Inventory, first.Request.Production, 0, [first], []);
    }

    public WatershedRunoffCapacityPlan PlanAdmit(PreparedWatershedRunoff runoff)
    {
        ArgumentNullException.ThrowIfNull(runoff);
        return new(this, new(Domain, Inventory, Production, checked(Revision + 1), Pending.Append(runoff), Closed));
    }

    /// <summary>Releases capacity only. Held source stock retains its existing reservation metadata.</summary>
    public WatershedRunoffCapacityPlan PlanCancel(EntityId reservationId)
    {
        if (!Pending.Any(p => p.Request.ReservationId == reservationId))
        {
            throw new ArgumentException("Only an active forcing commitment can be canceled.");
        }
        return new(this, new(Domain, Inventory, Production, checked(Revision + 1),
            Pending.Where(p => p.Request.ReservationId != reservationId), Closed.Append(reservationId)));
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
        var planner = new WatershedRunoffPlanner();
        var refreshed = new List<PreparedWatershedRunoff>();
        foreach (var pending in Pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = pending.Request;
            refreshed.Add(planner.Prepare(new(Domain.Network, Domain.Mapping, inventory, production, request.ReservationId,
                Domain.OwnerActorId, request.Start, request.End, Domain.Material, request.Inputs, Domain.Terminals), cancellationToken));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(this, new(Domain, inventory, production, checked(Revision + 1), refreshed, Closed));
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
        if (Production is null || next is null || next.Revision <= Production.Revision
            || next.WorldId != Production.WorldId || next.BranchId != Production.BranchId
            || next.Recipes.Count != Production.Recipes.Count
            || Production.Recipes.Any(prior => !next.Recipes.Any(r => r.DefinitionId == prior.DefinitionId && r.Version == prior.Version)))
        {
            throw new ArgumentException("Production must remain explicit, advance and preserve its admitted recipes.");
        }
        foreach (var prior in Production.Batches)
        {
            var retained = next.Batches.SingleOrDefault(b => b.BatchId == prior.BatchId);
            if (retained is null || !ProductionCodecV1.EncodeBatch(prior).AsSpan().SequenceEqual(ProductionCodecV1.EncodeBatch(retained)))
            {
                throw new ArgumentException("Capacity reconciliation cannot discard or rewrite production history.");
            }
        }
    }
}

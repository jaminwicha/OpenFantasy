using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Executes finite due batches in canonical order, with shared step throughput and physical backpressure.</summary>
internal sealed class ReservoirTransportPlanner
{
    internal ReservoirTransportPlan Advance(ReservoirTransportState basis, WorldInstant boundary, bool readingHold,
        Func<EntityId, bool>? historicallyOccupied)
    {
        bool continued = basis.Reservoirs.Capacity.Production?.Continuation?.SupportsReservedTransport == true;
        if (continued) ArgumentNullException.ThrowIfNull(historicallyOccupied);
        if (boundary == basis.Boundary)
        {
            return new(basis, basis);
        }
        if (boundary != basis.Boundary.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep))
        {
            throw new ArgumentException("Transport advances exactly one selected calendar step; skipped or partial steps grant no throughput.");
        }
        var physical = basis.Reservoirs;
        var journal = basis.Stages.ToDictionary(s => s.StageId);
        var used = basis.Connections.ToDictionary(c => c.ConnectionId, _ => 0UL);
        List<EntityId> created = [];
        if (!readingHold)
        {
            foreach (var stage in basis.Stages.Where(s => s.IsPending && s.DueAt <= boundary).OrderBy(s => s.DueAt).ThenBy(s => s.StageId))
            {
                var current = new ReservoirTransportState(physical, basis.Connections, journal.Values, basis.Revision, boundary, basis.ContinuationQualification);
                var runoff = physical.Capacity.Pending.Single(p => p.Request.ReservationId == stage.ReservationId);
                var routes = RunoffTransportRouting.Resolve(current, runoff, stage.Routes);
                var amounts = runoff.Request.Inputs.Where(i => i.Source is not null).GroupBy(i => routes[i.BasinId].ConnectionId)
                    .ToDictionary(g => g.Key, g => g.Aggregate(0UL, (sum, i) => checked(sum + i.Source!.Quantity)));
                if (amounts.Any(pair => pair.Value > basis.Connections.Single(c => c.ConnectionId == pair.Key).MaximumUnitsPerStep - used[pair.Key]))
                {
                    journal[stage.StageId] = stage.WithStatus(RunoffTransportStatus.BlockedByThroughput);
                    continue;
                }
                try
                {
                    ProductionSnapshot? production = physical.Capacity.Production;
                    EntityId cause = ReservoirTransportEvidence.DeliveryCause(stage.StageId);
                    if (continued && (production!.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations
                        || current.ContainsIdentity(cause) || created.Contains(cause) || historicallyOccupied!(cause)))
                        throw new ArgumentException("Reserved delivery requires material history room and a fresh permanent cause.");
                    var moved = RunoffTransportCustody.Move(current, runoff, stage, routes,
                        id => continued && id == cause || created.Contains(id) || historicallyOccupied?.Invoke(id) == true);
                    if (continued)
                    {
                        // This tentative proof and all material changes remain private until every physical gate accepts the whole stage.
                        var evidence = new ReservoirTransportEvidence(current, runoff, stage, boundary,
                            RunoffTransportCustody.Intervals(current, runoff, routes), moved.Inventory, moved.CreatedIds);
                        production = MaterialContinuationCommit.Prepare(moved.Inventory, production!,
                            new MaterialReservedTransportOperation(checked(production!.Revision + 1), evidence)).CandidateProduction;
                    }
                    var completion = physical.Capacity.PlanComplete(stage.ReservationId, moved.Inventory, production);
                    var candidate = physical.PlanApply(completion).ApplyTo(physical);
                    if (routes.Values.DistinctBy(c => c.ConnectionId).Any(c => !c.AdmitsHead(candidate)))
                    {
                        journal[stage.StageId] = stage.WithStatus(RunoffTransportStatus.BlockedByHead);
                        continue;
                    }
                    physical = candidate;
                    journal[stage.StageId] = stage.WithStatus(RunoffTransportStatus.Completed, boundary);
                    created.AddRange(moved.CreatedIds);
                    if (continued) created.Add(cause);
                    foreach (var pair in amounts)
                    {
                        used[pair.Key] = checked(used[pair.Key] + pair.Value);
                    }
                }
                catch (Exception error) when (error is ArgumentException or OverflowException or CustodyConflictException)
                {
                    // A bounded stage may wait for representable material storage; no debit or hold is released.
                    journal[stage.StageId] = stage.WithStatus(RunoffTransportStatus.BlockedByMaterial);
                }
            }
        }
        var final = new ReservoirTransportState(physical, basis.Connections, journal.Values, checked(basis.Revision + 1), boundary, basis.ContinuationQualification);
        return new(basis, final, created);
    }
}

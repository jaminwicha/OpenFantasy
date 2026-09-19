using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Geography;

/// <summary>Executes finite due batches in canonical order, with shared step throughput and physical backpressure.</summary>
internal sealed class ReservoirTransportPlanner
{
    internal ReservoirTransportPlan Advance(ReservoirTransportState basis, WorldInstant boundary, bool readingHold,
        Func<EntityId, bool>? historicallyOccupied)
    {
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
                var current = new ReservoirTransportState(physical, basis.Connections, journal.Values, basis.Revision, boundary);
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
                    var moved = RunoffTransportCustody.Move(current, runoff, stage, routes,
                        id => created.Contains(id) || historicallyOccupied?.Invoke(id) == true);
                    var completion = physical.Capacity.PlanComplete(stage.ReservationId, moved.Inventory);
                    var candidate = physical.PlanApply(completion).ApplyTo(physical);
                    if (routes.Values.DistinctBy(c => c.ConnectionId).Any(c => !c.AdmitsHead(candidate)))
                    {
                        journal[stage.StageId] = stage.WithStatus(RunoffTransportStatus.BlockedByHead);
                        continue;
                    }
                    physical = candidate;
                    journal[stage.StageId] = stage.WithStatus(RunoffTransportStatus.Completed, boundary);
                    created.AddRange(moved.CreatedIds);
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
        var final = new ReservoirTransportState(physical, basis.Connections, journal.Values, checked(basis.Revision + 1), boundary);
        return new(basis, final, created);
    }
}

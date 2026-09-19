using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Geography;

/// <summary>Moves each held source interval to its own drainage receiver in one immutable custody transition.</summary>
internal static class RunoffTransportCustody
{
    internal static (CountedAuthorityState Inventory, IReadOnlyList<EntityId> CreatedIds) Move(
        ReservoirTransportState state, PreparedWatershedRunoff runoff, RunoffTransportStage stage,
        IReadOnlyDictionary<EntityId, ReservoirConnection> routes, Func<EntityId, bool>? historicallyOccupied)
    {
        var inventory = state.Reservoirs.Capacity.Inventory;
        var inputs = runoff.Request.Inputs.Where(i => i.Source is not null).ToArray();
        List<CountedSourceLot> lots = [];
        List<EntityId> created = [];
        HashSet<EntityId> touched = [];
        ulong movedUnits = 0;
        foreach (var lot in inventory.Custody.Lots)
        {
            if (lot.ReservedBy != stage.ReservationId)
            {
                lots.Add(lot);
                continue;
            }
            ulong cursor = lot.Source.Start;
            int fragment = 0;
            foreach (var input in inputs.Where(i => i.Source!.SourceId == lot.Source.SourceId && i.CustodianId == lot.CustodianId)
                .OrderBy(i => i.Source!.Start))
            {
                ulong start = Math.Max(lot.Source.Start, input.Source!.Start), end = Math.Min(lot.Source.End, input.Source.End);
                if (start >= end)
                {
                    continue;
                }
                if (start != cursor || lot.Definition != runoff.Request.Material.Item || lot.Protection != LotProtection.None)
                {
                    throw new ArgumentException("Delivery cannot change or leave holes in its exact eligible source funding.");
                }
                EntityId identity = lot.LotId;
                ulong revision = checked(lot.Revision + 1);
                if (fragment++ != 0)
                {
                    identity = EntityId.DeriveSpawned(stage.StageId, (ulong)created.Count);
                    if (state.ContainsIdentity(identity) || created.Contains(identity) || historicallyOccupied?.Invoke(identity) == true)
                    {
                        throw new ArgumentException("A transport fragment collides with an existing or historical identity.");
                    }
                    created.Add(identity);
                    revision = 0;
                }
                var connection = routes[input.BasinId];
                EntityId receiver = state.Reservoirs.Geometries.Single(g => g.ReservoirId == connection.ReceiverReservoirId).CustodianId;
                lots.Add(new(identity, receiver, lot.Definition, new(lot.Source.SourceId, start, end), revision));
                touched.Add(lot.CustodianId);
                touched.Add(receiver);
                movedUnits = checked(movedUnits + end - start);
                cursor = end;
            }
            if (cursor != lot.Source.End)
            {
                throw new ArgumentException("Every held source unit must move exactly once in its indivisible stage.");
            }
        }
        if (movedUnits != stage.Units)
        {
            throw new ArgumentException("Transferred intervals do not equal the admitted stage's funded quantity.");
        }
        var storages = inventory.Custody.Storages.Select(s => touched.Contains(s.CustodianId)
            ? s with { Revision = checked(s.Revision + 1) } : s);
        var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, checked(inventory.Custody.Revision + 1), storages, lots);
        var candidate = inventory.WithCustody(custody);
        state.Reservoirs.Capacity.Production?.ValidateForAuthority(candidate);
        return (candidate, created);
    }
}

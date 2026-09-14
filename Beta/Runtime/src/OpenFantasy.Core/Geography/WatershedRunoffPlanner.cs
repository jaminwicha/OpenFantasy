namespace OpenFantasy.Core.Geography;

/// <summary>Prepares exact aggregate runoff obligations along an already resolved finite drainage DAG.</summary>
public sealed class WatershedRunoffPlanner
{
    public PreparedWatershedRunoff Prepare(WatershedRunoffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var funding = new WatershedRunoffFunding(request, cancellationToken);
        var basins = request.Network.Basins.ToDictionary(b => b.BasinId);
        var closures = request.Network.Closures.ToDictionary(c => c.BasinId);
        var incoming = basins.Keys.ToDictionary(id => id, _ => 0);
        var contributions = basins.Values.ToDictionary(b => b.BasinId,
            b => Enumerable.Repeat(funding.PerColumn[b.BasinId], b.Columns.Count).ToArray());
        foreach (WatershedOutletClosure closure in closures.Values)
        {
            if (closure.ReceivingBasinId is { } target)
            {
                incoming[target]++;
            }
        }
        var ready = new SortedSet<EntityId>(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        List<WatershedRunoffTerminalTotal> terminals = [];
        int visited = 0;
        while (ready.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EntityId identity = ready.Min;
            ready.Remove(identity);
            PreparedWatershed basin = basins[identity];
            ulong[] amounts = contributions[identity];
            foreach (WatershedColumn column in basin.Columns.OrderByDescending(c => c.DrainageRank))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (column.DownstreamIndex is { } downstream)
                {
                    amounts[downstream] = checked(amounts[downstream] + amounts[column.Index]);
                }
            }
            ulong outgoing = amounts[basin.Request.OutletIndex];
            WatershedOutletClosure closure = closures[identity];
            if (closure.ReceivingBasinId is { } target)
            {
                int index = closure.ReceivingColumnIndex!.Value;
                contributions[target][index] = checked(contributions[target][index] + outgoing);
                if (--incoming[target] == 0)
                {
                    ready.Add(target);
                }
            }
            else
            {
                var storage = request.Terminals.Single(t => t.BasinId == identity);
                terminals.Add(new(identity, storage.CustodianId, outgoing));
            }
            visited++;
        }
        if (visited != basins.Count || terminals.Aggregate(0UL, (sum, t) => checked(sum + t.IncomingUnits)) != funding.TotalUnits)
        {
            throw new ArgumentException("Runoff must terminate with exactly its funded input quantity.");
        }
        var receivers = ValidateReceivers(request, terminals);
        cancellationToken.ThrowIfCancellationRequested();
        return new(request, contributions, funding.TotalUnits, terminals, receivers);
    }

    private static IEnumerable<WatershedRunoffReceiverTotal> ValidateReceivers(WatershedRunoffRequest request,
        IEnumerable<WatershedRunoffTerminalTotal> terminals)
    {
        foreach (var group in terminals.GroupBy(t => t.CustodianId).OrderBy(g => g.Key))
        {
            var bindings = request.Terminals.Where(t => t.CustodianId == group.Key).ToArray();
            var capacity = bindings[0].Capacity;
            if (bindings.Any(b => b.Capacity != capacity))
            {
                throw new ArgumentException("One physical receiver cannot have conflicting volume capacity witnesses.");
            }
            ulong existing = request.Production?.GetCombinedStoredQuantity(request.Inventory, group.Key)
                ?? request.Inventory.Custody.GetStoredQuantity(group.Key);
            ulong incoming = group.Aggregate(0UL, (sum, t) => checked(sum + t.IncomingUnits));
            ulong projected = checked(existing + incoming);
            var volume = request.Material.Physical.VolumePerUnit.Multiply(projected);
            if (projected > request.Inventory.Custody.GetStorage(group.Key).UnitCapacity || volume.Value > capacity.Value)
            {
                throw new ArgumentException("Combined incoming runoff and actual occupancy exceed counted or physical receiving headroom.");
            }
            yield return new(group.Key, existing, incoming, projected, volume, capacity);
        }
    }
}

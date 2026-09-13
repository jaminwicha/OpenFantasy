namespace OpenFantasy.Core.Geography;

/// <summary>Composes a finite drainage DAG and propagates each incoming contribution through its exact receiving column.</summary>
public sealed class WatershedNetworkPlanner
{
    public PreparedWatershedNetwork Prepare(EntityId provinceId, IEnumerable<PreparedWatershed> basins,
        IEnumerable<WatershedOutletClosure> closures, CancellationToken cancellation = default)
    {
        var inputs = new WatershedNetworkInputs(provinceId, basins, closures, cancellation);
        Dictionary<EntityId, int> incoming = inputs.Basins.ToDictionary(basin => basin.BasinId, _ => 0);
        Dictionary<EntityId, int[]> counts = inputs.Basins.ToDictionary(basin => basin.BasinId,
            basin => Enumerable.Repeat(1, basin.Columns.Count).ToArray());
        foreach (WatershedOutletClosure closure in inputs.Closures)
        {
            if (closure.ReceivingBasinId is { } target)
            {
                incoming[target] = checked(incoming[target] + 1);
            }
        }
        var ready = new SortedSet<EntityId>(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var terminals = new List<WatershedTerminalContribution>();
        int visited = 0;
        while (ready.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            EntityId identity = ready.Min;
            ready.Remove(identity);
            PreparedWatershed basin = inputs.ByBasin[identity];
            int[] local = counts[identity];
            foreach (WatershedColumn column in basin.Columns.OrderByDescending(column => column.DrainageRank))
            {
                cancellation.ThrowIfCancellationRequested();
                if (column.DownstreamIndex is { } downstream)
                {
                    local[downstream] = checked(local[downstream] + local[column.Index]);
                }
            }
            int outletCount = local[basin.Request.OutletIndex];
            WatershedOutletClosure closure = inputs.ByClosure[identity];
            if (closure.ReceivingBasinId is { } target)
            {
                int targetIndex = closure.ReceivingColumnIndex!.Value;
                counts[target][targetIndex] = checked(counts[target][targetIndex] + outletCount);
                if (--incoming[target] == 0)
                {
                    ready.Add(target);
                }
            }
            else
            {
                int capacity = closure.TerminalColumnCapacity!.Value;
                if (outletCount > capacity)
                {
                    throw new ArgumentException("Terminal coverage exceeds its finite contributing-column capacity.");
                }
                terminals.Add(new(identity, basin.Request.ExternalBoundaryId, outletCount, capacity));
            }
            visited++;
        }
        if (visited != inputs.Basins.Length || terminals.Sum(terminal => terminal.ContributingColumns) != inputs.Columns.Count)
        {
            throw new ArgumentException("The network contains a cycle or fails complete terminal coverage.");
        }
        WatershedNetworkFace[] faces = new WatershedNetworkFaces(inputs, provinceId).Prepare(cancellation);
        cancellation.ThrowIfCancellationRequested();
        return new PreparedWatershedNetwork(provinceId, inputs.Basins, inputs.Closures, faces, counts,
            terminals.OrderBy(terminal => terminal.BasinId).ToArray());
    }
}

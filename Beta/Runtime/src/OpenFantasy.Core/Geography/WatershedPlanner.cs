namespace OpenFantasy.Core.Geography;

/// <summary>
/// Finite priority-flood preparation from an explicit outlet. Heap ties use canonical column indices;
/// equal spill elevations use an acyclic drainage rank, never worker completion order or float perturbations.
/// </summary>
public sealed class WatershedPlanner
{
    public PreparedWatershed Prepare(WatershedRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellation.ThrowIfCancellationRequested();
        var derivation = new WatershedDerivation(request);
        int count = request.Bounds.ColumnCount;
        long[] natural = new long[count];
        long[] spill = new long[count];
        int[] parent = Enumerable.Repeat(-2, count).ToArray();
        int[] order = new int[count];
        int[] rank = new int[count];
        int[] contribution = Enumerable.Repeat(1, count).ToArray();
        for (int index = 0; index < count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            natural[index] = index == request.OutletIndex ? request.Rules.OutletElevation : derivation.NaturalElevation(index);
        }

        var frontier = new PriorityQueue<int, (long Elevation, int Index)>();
        int outlet = request.OutletIndex;
        parent[outlet] = -1;
        spill[outlet] = natural[outlet];
        frontier.Enqueue(outlet, (spill[outlet], outlet));
        int visited = 0;
        while (frontier.TryDequeue(out int current, out _))
        {
            cancellation.ThrowIfCancellationRequested();
            rank[current] = visited;
            order[visited++] = current;
            foreach (int neighbor in request.Bounds.Neighbors(current))
            {
                if (parent[neighbor] != -2)
                {
                    continue;
                }
                parent[neighbor] = current;
                spill[neighbor] = Math.Max(natural[neighbor], spill[current]);
                frontier.Enqueue(neighbor, (spill[neighbor], neighbor));
            }
        }
        if (visited != count)
        {
            throw new InvalidOperationException("Watershed preparation did not reach every declared column.");
        }
        for (int index = count - 1; index > 0; index--)
        {
            int current = order[index];
            contribution[parent[current]] = checked(contribution[parent[current]] + contribution[current]);
        }

        var identities = new HashSet<EntityId>
        {
            request.Scope.WorldId, request.Scope.RealmId, request.Scope.BranchId, request.ExternalBoundaryId
        };
        EntityId Register(string role, ulong ordinal)
        {
            EntityId id = derivation.Identity(role, ordinal);
            if (!identities.Add(id))
            {
                throw new InvalidOperationException("Generated watershed identities collide.");
            }
            return id;
        }
        EntityId basinId = Register("basin", 0);
        EntityId formationId = Register("formation", 0);
        WatershedColumn[] columns = new WatershedColumn[count];
        var boundaries = new Dictionary<(int, int), WatershedBoundary>();
        for (int index = 0; index < count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            columns[index] = new WatershedColumn(index, natural[index], spill[index], parent[index] < 0 ? null : parent[index],
                rank[index], contribution[index], Register("substrate-parcel", (ulong)index));
            foreach (int neighbor in request.Bounds.Neighbors(index).Where(value => value > index))
            {
                ulong ordinal = checked((ulong)index * WatershedBounds.MaximumColumns + (ulong)neighbor);
                int? from = parent[index] == neighbor ? index : parent[neighbor] == index ? neighbor : null;
                boundaries.Add((index, neighbor), new WatershedBoundary(Register("boundary", ordinal), index, neighbor,
                    natural[index], natural[neighbor], from));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        return new PreparedWatershed(request, basinId, formationId, columns, boundaries);
    }
}

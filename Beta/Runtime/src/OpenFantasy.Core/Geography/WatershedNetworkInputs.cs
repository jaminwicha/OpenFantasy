namespace OpenFantasy.Core.Geography;

/// <summary>Bounded, complete input validation and identity ownership for one immutable network candidate.</summary>
internal sealed class WatershedNetworkInputs
{
    private readonly HashSet<EntityId> _identities;

    internal WatershedNetworkInputs(EntityId provinceId, IEnumerable<PreparedWatershed> basins,
        IEnumerable<WatershedOutletClosure> closures, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(basins);
        ArgumentNullException.ThrowIfNull(closures);
        cancellation.ThrowIfCancellationRequested();
        PreparedWatershed[] supplied = basins.Take(PreparedWatershedNetwork.MaximumBasins + 1).ToArray();
        if (supplied.Length is < 1 or > PreparedWatershedNetwork.MaximumBasins || supplied.Any(basin => basin is null))
        {
            throw new ArgumentException("A network requires one through sixteen prepared basins.", nameof(basins));
        }
        Basins = supplied.OrderBy(basin => basin.BasinId).ToArray();
        var scope = Basins[0].Request.Scope;
        _identities = [];
        Register(scope.WorldId);
        Register(scope.BranchId);
        Register(scope.RealmId);
        Register(provinceId);
        foreach (PreparedWatershed basin in Basins)
        {
            cancellation.ThrowIfCancellationRequested();
            if (basin.Request.Scope != scope || basin.Request.Seed != Basins[0].Request.Seed)
            {
                throw new ArgumentException("Network basins must have the same world, branch, realm and seed.");
            }
            Register(basin.BasinId);
            Register(basin.FormationId);
            Register(basin.Request.ExternalBoundaryId);
            ByBasin.Add(basin.BasinId, basin);
            foreach (WatershedColumn column in basin.Columns)
            {
                cancellation.ThrowIfCancellationRequested();
                Register(column.SubstrateParcelId);
                if (!Columns.TryAdd(AddressOf(basin, column.Index), new(basin.BasinId, column.Index)))
                {
                    throw new ArgumentException("Network basin columns overlap.");
                }
            }
            foreach (WatershedBoundary boundary in basin.Boundaries)
            {
                Register(boundary.BoundaryId);
            }
        }
        WatershedOutletClosure[] suppliedClosures = closures.Take(PreparedWatershedNetwork.MaximumBasins + 1).ToArray();
        if (suppliedClosures.Length != Basins.Length || suppliedClosures.Any(closure => closure is null))
        {
            throw new ArgumentException("Every basin requires exactly one explicit outlet closure.", nameof(closures));
        }
        Closures = suppliedClosures.OrderBy(closure => closure.BasinId).ToArray();
        foreach (WatershedOutletClosure closure in Closures)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!ByBasin.TryGetValue(closure.BasinId, out PreparedWatershed? source)
                || !ByClosure.TryAdd(closure.BasinId, closure))
            {
                throw new ArgumentException("An outlet closure is duplicated or names an unknown basin.");
            }
            if (closure.Kind == WatershedOutletKind.Basin)
            {
                ValidateLink(source, closure);
            }
        }
    }

    internal PreparedWatershed[] Basins { get; }
    internal WatershedOutletClosure[] Closures { get; }
    internal Dictionary<EntityId, PreparedWatershed> ByBasin { get; } = [];
    internal Dictionary<EntityId, WatershedOutletClosure> ByClosure { get; } = [];
    internal Dictionary<WatershedColumnAddress, WatershedColumnReference> Columns { get; } = [];

    internal void Register(EntityId identity)
    {
        if (!_identities.Add(identity))
        {
            throw new ArgumentException("Network identity roles collide across the admitted candidate.");
        }
    }

    internal static WatershedColumnAddress AddressOf(PreparedWatershed basin, int index)
    {
        (long x, long y) = basin.Request.Bounds.CoordinateAt(index);
        return new WatershedColumnAddress(x, y);
    }

    private void ValidateLink(PreparedWatershed source, WatershedOutletClosure closure)
    {
        EntityId targetId = closure.ReceivingBasinId!.Value;
        int targetIndex = closure.ReceivingColumnIndex!.Value;
        if (!ByBasin.TryGetValue(targetId, out PreparedWatershed? target)
            || !target.Request.Bounds.IsBoundary(targetIndex)
            || !AddressOf(source, source.Request.OutletIndex).IsAdjacentTo(AddressOf(target, targetIndex)))
        {
            throw new ArgumentException("An outlet link must enter a cardinally adjacent receiving basin boundary column.");
        }
        if (target.Columns[targetIndex].SpillElevation > source.Columns[source.Request.OutletIndex].NaturalElevation)
        {
            throw new ArgumentException("The receiving spill route exceeds the sending outlet elevation.");
        }
    }
}

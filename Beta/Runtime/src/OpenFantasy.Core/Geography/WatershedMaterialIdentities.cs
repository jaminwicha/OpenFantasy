namespace OpenFantasy.Core.Geography;

/// <summary>Shared immutable macro role lookup for material bindings to one exact prepared network.</summary>
internal sealed class WatershedMaterialIdentities
{
    private readonly HashSet<EntityId> _identities = [];

    internal WatershedMaterialIdentities(PreparedWatershedNetwork network)
    {
        _identities.Add(network.Scope.RealmId);
        _identities.Add(network.ProvinceId);
        foreach (PreparedWatershed basin in network.Basins)
        {
            _identities.Add(basin.BasinId);
            _identities.Add(basin.FormationId);
            _identities.Add(basin.Request.ExternalBoundaryId);
            foreach (WatershedColumn column in basin.Columns)
            {
                _identities.Add(column.SubstrateParcelId);
            }
            foreach (WatershedBoundary boundary in basin.Boundaries)
            {
                _identities.Add(boundary.BoundaryId);
            }
        }
        foreach (WatershedNetworkFace face in network.SharedFaces)
        {
            _identities.Add(face.BoundaryId);
        }
    }

    internal bool Contains(EntityId identity) => _identities.Contains(identity);
}

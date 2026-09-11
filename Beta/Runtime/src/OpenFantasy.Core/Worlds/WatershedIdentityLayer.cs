using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Immutable reserved macro identities. These confer no terrain extraction or liquid custody.</summary>
internal sealed class WatershedIdentityLayer
{
    private readonly PreparedWatershed? _watershed;
    private readonly Dictionary<EntityId, WorldIdentityRole> _roles = [];
    private readonly TerrainIdentityLayer _terrain;

    internal WatershedIdentityLayer(PreparedWatershed? watershed, TerrainIdentityLayer terrain)
    {
        _watershed = watershed;
        _terrain = terrain;
        if (watershed is null)
        {
            return;
        }
        Add(watershed.BasinId, WorldIdentityRole.GeographyBasin);
        Add(watershed.FormationId, WorldIdentityRole.GeographyFormation);
        Add(watershed.Request.ExternalBoundaryId, WorldIdentityRole.GeographyTerminal);
        foreach (WatershedColumn column in watershed.Columns)
        {
            Add(column.SubstrateParcelId, WorldIdentityRole.GeographySubstrate);
        }
        foreach (WatershedBoundary boundary in watershed.Boundaries)
        {
            Add(boundary.BoundaryId, WorldIdentityRole.GeographyBoundary);
        }
        ValidateTerrain();
    }

    internal bool Matches(PreparedWatershed? watershed, TerrainIdentityLayer terrain) =>
        ReferenceEquals(_watershed, watershed) && ReferenceEquals(_terrain, terrain);
    internal bool Contains(EntityId identity) => _roles.ContainsKey(identity);

    internal void ValidateScope(TerrainScope scope)
    {
        if (_watershed is not null && _watershed.Request.Scope != scope)
        {
            throw new ArgumentException("Macro geography must belong to the same world, branch and realm as local space.");
        }
    }

    private void ValidateTerrain()
    {
        foreach (EntityId identity in _roles.Keys)
        {
            if (_terrain.Contains(identity))
            {
                throw new ArgumentException("Macro geography cannot alias local terrain or material identities.");
            }
        }
    }

    private void Add(EntityId identity, WorldIdentityRole role)
    {
        if (!_roles.TryAdd(identity, role))
        {
            throw new ArgumentException("Macro geography contains duplicate reserved identities.");
        }
    }
}

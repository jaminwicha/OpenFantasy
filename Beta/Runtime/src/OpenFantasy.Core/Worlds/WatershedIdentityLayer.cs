using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Immutable reserved macro identities. These confer no terrain extraction or liquid custody.</summary>
internal sealed class WatershedIdentityLayer
{
    private readonly PreparedWatershed? _watershed;
    private readonly PreparedWatershedNetwork? _network;
    private readonly Dictionary<EntityId, WorldIdentityRole> _roles = [];
    private readonly TerrainIdentityLayer _terrain;
    private readonly Lazy<HashSet<ulong>> _identityPrefixes;

    internal WatershedIdentityLayer(PreparedWatershed? watershed, PreparedWatershedNetwork? network, TerrainIdentityLayer terrain)
    {
        _watershed = watershed;
        _network = network;
        _terrain = terrain;
        if (watershed is not null)
        {
            AddBasin(watershed);
        }
        if (network is not null)
        {
            Add(network.ProvinceId, WorldIdentityRole.GeographyProvince);
            foreach (PreparedWatershed basin in network.Basins)
            {
                AddBasin(basin);
            }
            foreach (WatershedNetworkFace face in network.SharedFaces)
            {
                Add(face.BoundaryId, WorldIdentityRole.GeographyBoundary);
            }
        }
        ValidateTerrain();
        _identityPrefixes = new(() => _roles.Keys.Select(identity => identity.High).ToHashSet());
    }

    private void AddBasin(PreparedWatershed watershed)
    {
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
    }

    internal bool Matches(PreparedWatershed? watershed, PreparedWatershedNetwork? network, TerrainIdentityLayer terrain) =>
        ReferenceEquals(_watershed, watershed) && ReferenceEquals(_network, network) && ReferenceEquals(_terrain, terrain);
    internal IEnumerable<EntityId> Identities => _roles.Keys;
    internal bool Contains(EntityId identity) => _roles.ContainsKey(identity);
    internal bool ContainsPrefix(ulong prefix) => _identityPrefixes.Value.Contains(prefix);

    internal void ValidateScope(TerrainScope scope)
    {
        if (_watershed is not null && _watershed.Request.Scope != scope || _network is not null && _network.Scope != scope)
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

using OpenFantasy.Core;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Content;

/// <summary>Stable definition keys and seeded instance roles with local collision rejection.</summary>
internal sealed class FarmIdentityScope
{
    private readonly byte[] _seed;
    private readonly Dictionary<EntityId, string> _roles = [];

    internal FarmIdentityScope(byte[] seed) => _seed = (byte[])seed.Clone();

    internal EntityId Definition(string key) => Register(EntityId.DeriveGenerated(
        "openfantasy.authored-content"u8, "native-farm.definitions.v1", key, "definition", 0), "definition:" + key);

    internal EntityId Instance(string role) => Register(EntityId.DeriveGenerated(
        _seed, "native-farm.instances.v1", role, "instance", 0), "instance:" + role);

    internal void ValidateTerrainIdentities(TerrainRegionSnapshot region)
    {
        foreach (TerrainCell cell in region.Cells)
        {
            if (cell.SourceId is EntityId source)
            {
                Register(source, "terrain-source:" + source);
            }
            if (cell.FormationId is EntityId formation)
            {
                Register(formation, "terrain-formation:" + formation);
            }
        }
    }

    private EntityId Register(EntityId id, string role)
    {
        if (_roles.TryGetValue(id, out string? existing) && existing != role)
        {
            throw new InvalidDataException("A generated farm identity collides with another admitted role.");
        }
        _roles[id] = role;
        return id;
    }
}

using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Immutable identities admitted in one local root, including named historical causes.
/// Membership does not prove a committed cause, authenticate a caller or cover absent world history.
/// </summary>
public sealed class WorldIdentityIndex
{
    private readonly Dictionary<EntityId, WorldIdentityRole> _dynamicRoles;

    internal WorldIdentityIndex(Dictionary<EntityId, WorldIdentityRole> dynamicRoles, TerrainIdentityLayer terrain)
    {
        _dynamicRoles = new Dictionary<EntityId, WorldIdentityRole>(dynamicRoles);
        Terrain = terrain;
    }

    internal TerrainIdentityLayer Terrain { get; }

    public bool Contains(EntityId identity) => _dynamicRoles.ContainsKey(identity) || Terrain.Contains(identity);
}

internal enum WorldIdentityRole
{
    World, Branch, Realm, Actor, Principal, Session, Custodian, Lot, CountedSource, ItemDefinition,
    Reservation, Form, Body, Place, BodyDefinition, Injury, CauseReference, TerrainFormation,
    TerrainSource, Language, LanguageRegister, Utterance, Dialogue, GatherNode, GatherNodeDefinition,
    CraftRecipeDefinition, PhysicalRecipeDefinition, PhysicalMaterialDefinition, ProductionBatch, TransformedLot
}

/// <summary>One immutable terrain dependency set; reference equality is the only reuse criterion.</summary>
internal sealed class TerrainIdentityLayer
{
    private readonly TerrainRegionSnapshot[] _regions;
    private readonly Dictionary<EntityId, WorldIdentityRole> _roles;
    private readonly Dictionary<EntityId, ContentHash> _materialPins;

    internal TerrainIdentityLayer(IEnumerable<TerrainRegionSnapshot> regions,
        Dictionary<EntityId, WorldIdentityRole> roles, Dictionary<EntityId, ContentHash> materialPins)
    {
        _regions = regions.ToArray();
        // The builder transfers these private dictionaries and never mutates them after construction.
        _roles = roles;
        _materialPins = materialPins;
    }

    internal bool Matches(IReadOnlyList<TerrainRegionSnapshot> regions)
    {
        if (regions.Count != _regions.Length)
        {
            return false;
        }
        for (int index = 0; index < _regions.Length; index++)
        {
            if (!ReferenceEquals(regions[index], _regions[index]))
            {
                return false;
            }
        }
        return true;
    }

    internal bool Contains(EntityId identity) => _roles.ContainsKey(identity);

    internal bool TryGetRole(EntityId identity, out WorldIdentityRole role) => _roles.TryGetValue(identity, out role);

    internal bool TryGetMaterialPin(EntityId identity, out ContentHash? version) =>
        _materialPins.TryGetValue(identity, out version);
}

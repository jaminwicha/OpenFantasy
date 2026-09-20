using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Stepped actor traversal and explicit complete foreign-construction support.</summary>
internal static class WorldSpatialCodecV4
{
    internal static byte[] Encode(LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (space.GroundSupport != GroundSupportMode.SteppedTerrainAndConstruction
            || space.StructureSupport != StructureSupportMode.TerrainAndConstruction)
        {
            throw new ArgumentException("Spatial version four requires both stepped actor and coupled construction support.", nameof(space));
        }
        return WorldSpatialCodecV1.EncodeComponents(space, 4);
    }

    internal static LocalSpatialSnapshot Decode(ReadOnlySpan<byte> bytes, IEnumerable<SpatialStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        var components = WorldSpatialCodecV1.DecodeComponents(bytes, 4);
        return new(components.Scope, components.Regions, components.Actors, structures,
            GroundSupportMode.SteppedTerrainAndConstruction, StructureSupportMode.TerrainAndConstruction);
    }
}

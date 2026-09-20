using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Spatial components admitted under stepped traversal with exact derived construction support.</summary>
internal static class WorldSpatialCodecV3
{
    internal static byte[] Encode(LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (space.GroundSupport != GroundSupportMode.SteppedTerrainAndConstruction || space.StructureSupport != StructureSupportMode.TerrainOnly)
        {
            throw new ArgumentException("Spatial version three requires stepped terrain and construction support.", nameof(space));
        }
        return WorldSpatialCodecV1.EncodeComponents(space, 3);
    }

    internal static LocalSpatialSnapshot Decode(ReadOnlySpan<byte> bytes, IEnumerable<SpatialStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        var components = WorldSpatialCodecV1.DecodeComponents(bytes, 3);
        return new LocalSpatialSnapshot(components.Scope, components.Regions, components.Actors,
            structures, GroundSupportMode.SteppedTerrainAndConstruction);
    }
}

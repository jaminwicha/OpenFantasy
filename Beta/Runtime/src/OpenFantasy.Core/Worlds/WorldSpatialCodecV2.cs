using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Spatial components whose actor support derives from the separately retained complete construction state.</summary>
internal static class WorldSpatialCodecV2
{
    internal static byte[] Encode(LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (space.GroundSupport != GroundSupportMode.TerrainAndConstruction)
        {
            throw new ArgumentException("Spatial version two requires terrain and construction ground support.", nameof(space));
        }
        return WorldSpatialCodecV1.EncodeComponents(space, 2);
    }

    internal static LocalSpatialSnapshot Decode(ReadOnlySpan<byte> bytes, IEnumerable<SpatialStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        var components = WorldSpatialCodecV1.DecodeComponents(bytes, 2);
        return new LocalSpatialSnapshot(components.Scope, components.Regions, components.Actors,
            structures, GroundSupportMode.TerrainAndConstruction);
    }
}

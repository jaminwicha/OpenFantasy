using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Joins actual present actors and terrain while allowing independently qualified structure projection.</summary>
internal static class ConstructionSourceFrame
{
    internal static void ValidateSpace(LocalSpatialSnapshot actual, LocalSpatialSnapshot supplied)
    {
        if (actual.Scope != supplied.Scope || actual.GroundSupport != supplied.GroundSupport
            || actual.StructureSupport != supplied.StructureSupport)
            throw new ArgumentException("Construction work must retain the actual source frame's scope and support interpretation.");
        ulong version = actual.StructureSupport == StructureSupportMode.TerrainAndConstruction ? 4UL
            : actual.GroundSupport switch
            {
                GroundSupportMode.SteppedTerrainAndConstruction => 3UL,
                GroundSupportMode.TerrainAndConstruction => 2UL,
                _ => 1UL
            };
        if (!WorldSpatialCodecV1.EncodeComponents(actual, version).AsSpan()
                .SequenceEqual(WorldSpatialCodecV1.EncodeComponents(supplied, version)))
            throw new ArgumentException("Construction work requires the exact current source actors, body state and terrain regions.");
    }
}

using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Requires complete geometric sealing for every fixed vessel owned by a contained world.</summary>
public static class WorldReservoirContainmentBinding
{
    public static void Validate(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ReservoirSeals is not { } policy)
        {
            return;
        }
        if (world.Reservoirs is null || world.Transport is not null || world.Excavation is not null
            || world.ConstructionPhysical is not null || world.StructuralPolicy is not null)
        {
            throw new ArgumentException("Contained reservoirs require their fixed vessel interpretation without transport or physical displacement.", nameof(world));
        }
        IReadOnlyList<ReservoirBoundaryResult> results = new ReservoirBoundaryEvaluator().AssessBoundaries(world, policy);
        if (results.Any(result => !result.IsContained))
        {
            throw new ArgumentException("A contained reservoir world requires complete sealed nominal interiors, floors and walls without occupants.", nameof(world));
        }
        if (world.LiquidCells is not null && results.Any(result => result.ConstructedSealedFaces != 0))
        {
            throw new ArgumentException("Detailed liquid flow requires terrain-supplied enclosure until construction pressure and fluid loads are admitted.", nameof(world));
        }
    }
}

using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Spatial;

public sealed partial class LocalSpatialSnapshot
{
    public const int MaximumStructures = 32;
    public const int MaximumAccessRequirements = 32;

    /// <summary>Same-floor actor-center sightline to a point at that center height, without a fake target actor.</summary>
    public SpatialInteraction QueryPointInteraction(EntityId actorId, SubunitPosition target, long maximumRangeSubunits)
    {
        if (maximumRangeSubunits is < 1 or > InteractionRangeSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRangeSubunits));
        }
        ActorPresence actor = GetActor(actorId);
        if (actor.Feet.Z != target.Z)
        {
            return SpatialInteraction.DifferentFloor;
        }
        var center = _volumes[actorId].DoubledCenter;
        return TerrainSightline.QueryCenters(_terrain, center,
            ((Int128)target.X * 2, (Int128)target.Y * 2, center.Z), maximumRangeSubunits, Structures);
    }

    public StructurePlacementResult PrepareStructurePlacement(SpatialStructure proposed,
        IEnumerable<GroundAccessRequirement> requiredAccess, int? maximumExpansions = null)
    {
        StructurePlacementStatus status = _structureQueries.AssessPlacement(proposed, requiredAccess, maximumExpansions);
        if (status != StructurePlacementStatus.Prepared) return new(status);
        try
        {
            return new(status, WithStructures(Structures.Append(proposed)));
        }
        catch (OverflowException)
        {
            return new(StructurePlacementStatus.NumericLimit);
        }
    }

    private SpatialOccupancy QueryStructure(SpatialStructure proposed) => _structureQueries.QueryStructure(proposed);

    private ReadOnlyCollection<SpatialStructure> ReadStructures(IEnumerable<SpatialStructure> structures) =>
        SpatialStructureQueries.ReadStructures(Scope, Actors, structures);
}

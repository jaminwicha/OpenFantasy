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
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(requiredAccess);
        int budget = maximumExpansions ?? (GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction
            ? DefaultWalkingRouteExpansions : GroundRouteSearch.MaximumExpansions);
        if (GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction)
        {
            if (budget is < 1 or > MaximumWalkingRouteExpansions)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExpansions));
            }
        }
        else
        {
            GroundRouteSearch.ValidateBudget(budget);
        }
        List<GroundAccessRequirement> access = [];
        foreach (GroundAccessRequirement requirement in requiredAccess)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            if (access.Count == MaximumAccessRequirements)
            {
                throw new ArgumentException("Placement exceeds its bounded required-access set.", nameof(requiredAccess));
            }
            GetActor(requirement.ActorId);
            access.Add(requirement);
        }
        if (access.Count == 0)
        {
            throw new ArgumentException("Placement must qualify at least one explicit access connection.", nameof(requiredAccess));
        }
        // Definition/material/cause collisions are also checked by the complete world admission owner.
        ReadStructures(Structures.Append(proposed));
        try
        {
            SpatialOccupancy occupancy = QueryStructure(proposed);
            if (occupancy != SpatialOccupancy.Clear)
            {
                return new StructurePlacementResult(PlacementStatus(occupancy));
            }
            LocalSpatialSnapshot candidate = WithStructures(Structures.Append(proposed));
            foreach (GroundAccessRequirement requirement in access)
            {
                GroundRouteResult route = candidate.QueryGroundRoute(requirement.ActorId,
                    requirement.From, requirement.To, budget);
                if (route.Status != GroundRouteStatus.Reachable)
                {
                    return new StructurePlacementResult(route.Status switch
                    {
                        GroundRouteStatus.Unavailable => StructurePlacementStatus.Unavailable,
                        GroundRouteStatus.ExpansionLimit => StructurePlacementStatus.AccessLimit,
                        GroundRouteStatus.NumericLimit => StructurePlacementStatus.NumericLimit,
                        _ => StructurePlacementStatus.AccessBlocked
                    });
                }
            }
            return new StructurePlacementResult(StructurePlacementStatus.Prepared, candidate);
        }
        catch (OverflowException)
        {
            return new StructurePlacementResult(StructurePlacementStatus.NumericLimit);
        }
    }

    private SpatialOccupancy QueryStructure(SpatialStructure proposed)
    {
        bool missing = false;
        bool blocked = false;
        bool unsupported = false;
        foreach (SubunitBox box in proposed.Boxes)
        {
            SpatialOccupancy status = _terrain.Query(box.Volume, box.Minimum.Z == proposed.GroundPlaneZ);
            missing |= status == SpatialOccupancy.Unavailable;
            blocked |= status == SpatialOccupancy.BlockedTerrain;
            unsupported |= status == SpatialOccupancy.Unsupported;
        }
        if (missing) { return SpatialOccupancy.Unavailable; }
        if (blocked) { return SpatialOccupancy.BlockedTerrain; }
        foreach (SpatialStructure existing in Structures)
        {
            if (existing.StructureId != proposed.StructureId && proposed.Boxes.Any(box =>
                existing.Boxes.Any(other => box.Volume.Overlaps(other.Volume))))
            {
                return SpatialOccupancy.BlockedStructure;
            }
        }
        if (proposed.Boxes.Any(box => _volumes.Values.Any(volume => volume.Overlaps(box.Volume))))
        {
            return SpatialOccupancy.BlockedActor;
        }
        return unsupported ? SpatialOccupancy.Unsupported : SpatialOccupancy.Clear;
    }

    private ReadOnlyCollection<SpatialStructure> ReadStructures(IEnumerable<SpatialStructure> structures)
    {
        HashSet<EntityId> occupied = [Scope.WorldId, Scope.BranchId, Scope.RealmId];
        foreach (ActorPresence actor in Actors)
        {
            occupied.UnionWith([actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId!.Value,
                actor.HomePlaceId, actor.Condition.Definition.DefinitionId]);
        }
        List<SpatialStructure> copied = [];
        foreach (SpatialStructure structure in structures)
        {
            ArgumentNullException.ThrowIfNull(structure);
            if (copied.Count == MaximumStructures || !occupied.Add(structure.StructureId))
            {
                throw new ArgumentException("Structures require bounded unique identities separate from known spatial roles.", nameof(structures));
            }
            copied.Add(structure);
        }
        return Array.AsReadOnly(copied.OrderBy(value => value.StructureId).ToArray());
    }

    private static StructurePlacementStatus PlacementStatus(SpatialOccupancy occupancy) => occupancy switch
    {
        SpatialOccupancy.Unavailable => StructurePlacementStatus.Unavailable,
        SpatialOccupancy.BlockedTerrain => StructurePlacementStatus.BlockedTerrain,
        SpatialOccupancy.BlockedActor => StructurePlacementStatus.BlockedActor,
        SpatialOccupancy.BlockedStructure => StructurePlacementStatus.BlockedStructure,
        SpatialOccupancy.Unsupported => StructurePlacementStatus.Unsupported,
        _ => throw new ArgumentException("The occupancy result is not a placement rejection.", nameof(occupancy))
    };
}

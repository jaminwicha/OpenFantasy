using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Shared geometric placement and access checks; no material transaction or partial-world admission.</summary>
internal sealed class SpatialStructureQueries(TerrainScope scope, TerrainVolumeQueries terrain,
    SpatialMovementQueries movement, IReadOnlyList<ActorPresence> actors,
    IReadOnlyList<SpatialStructure> structures, StructureSupportMode structureSupport)
{
    internal StructurePlacementStatus AssessPlacement(SpatialStructure proposed,
        IEnumerable<GroundAccessRequirement> requiredAccess, int? maximumExpansions)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(requiredAccess);
        int budget = maximumExpansions ?? (movement.GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction
            ? LocalSpatialSnapshot.DefaultWalkingRouteExpansions : GroundRouteSearch.MaximumExpansions);
        if (movement.GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction)
        {
            if (budget is < 1 or > LocalSpatialSnapshot.MaximumWalkingRouteExpansions)
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
            if (access.Count == LocalSpatialSnapshot.MaximumAccessRequirements)
            {
                throw new ArgumentException("Placement exceeds its bounded required-access set.", nameof(requiredAccess));
            }
            movement.GetActor(requirement.ActorId);
            access.Add(requirement);
        }
        if (access.Count == 0)
        {
            throw new ArgumentException("Placement must qualify at least one explicit access connection.", nameof(requiredAccess));
        }
        // Definition/material/cause collisions are also checked by the complete world admission owner.
        var combined = ReadStructures(scope, actors, structures.Append(proposed));
        try
        {
            SpatialOccupancy occupancy = QueryStructure(proposed);
            if (occupancy != SpatialOccupancy.Clear) return PlacementStatus(occupancy);
            var candidate = movement.WithStructures(combined);
            foreach (GroundAccessRequirement requirement in access)
            {
                GroundRouteResult route = candidate.QueryGroundRoute(requirement.ActorId,
                    requirement.From, requirement.To, budget);
                if (route.Status != GroundRouteStatus.Reachable)
                {
                    return route.Status switch
                    {
                        GroundRouteStatus.Unavailable => StructurePlacementStatus.Unavailable,
                        GroundRouteStatus.ExpansionLimit => StructurePlacementStatus.AccessLimit,
                        GroundRouteStatus.NumericLimit => StructurePlacementStatus.NumericLimit,
                        _ => StructurePlacementStatus.AccessBlocked
                    };
                }
            }
            return StructurePlacementStatus.Prepared;
        }
        catch (OverflowException)
        {
            return StructurePlacementStatus.NumericLimit;
        }
    }

    internal SpatialOccupancy QueryStructure(SpatialStructure proposed)
    {
        bool missing = false;
        bool blocked = false;
        bool unsupported = false;
        foreach (SubunitBox box in proposed.Boxes)
        {
            bool isBase = box.Minimum.Z == proposed.GroundPlaneZ;
            SpatialOccupancy status = terrain.Query(box.Volume,
                isBase && structureSupport == StructureSupportMode.TerrainOnly);
            if (status == SpatialOccupancy.Clear && isBase
                && structureSupport == StructureSupportMode.TerrainAndConstruction)
            {
                status = FootingQuery.Query(terrain,
                    structures.Where(value => value.StructureId != proposed.StructureId).ToArray(), box.Volume);
            }
            missing |= status == SpatialOccupancy.Unavailable;
            blocked |= status == SpatialOccupancy.BlockedTerrain;
            unsupported |= status == SpatialOccupancy.Unsupported;
        }
        if (missing) { return SpatialOccupancy.Unavailable; }
        if (blocked) { return SpatialOccupancy.BlockedTerrain; }
        foreach (SpatialStructure existing in structures)
        {
            if (existing.StructureId != proposed.StructureId && proposed.Boxes.Any(box =>
                existing.Boxes.Any(other => box.Volume.Overlaps(other.Volume))))
            {
                return SpatialOccupancy.BlockedStructure;
            }
        }
        if (proposed.Boxes.Any(box => movement.OverlapsAnyActor(box.Volume)))
        {
            return SpatialOccupancy.BlockedActor;
        }
        return unsupported ? SpatialOccupancy.Unsupported : SpatialOccupancy.Clear;
    }

    internal static ReadOnlyCollection<SpatialStructure> ReadStructures(TerrainScope scope, IReadOnlyList<ActorPresence> actors, IEnumerable<SpatialStructure> structures)
    {
        HashSet<EntityId> occupied = [scope.WorldId, scope.BranchId, scope.RealmId];
        foreach (ActorPresence actor in actors)
        {
            occupied.UnionWith([actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId!.Value,
                actor.HomePlaceId, actor.Condition.Definition.DefinitionId]);
        }
        List<SpatialStructure> copied = [];
        foreach (SpatialStructure structure in structures)
        {
            ArgumentNullException.ThrowIfNull(structure);
            if (copied.Count == LocalSpatialSnapshot.MaximumStructures || !occupied.Add(structure.StructureId))
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

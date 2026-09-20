using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Structures;

public sealed record ConstructionSettlementResult(LocalSpatialSnapshot Space, ConstructionPhysicalSnapshot Physical);

/// <summary>
/// Resolves one intact rigid assembly and harmless actor drops after an owned terrain removal.
/// The caller publishes this complete candidate with excavation work, source custody and load admission.
/// </summary>
public sealed class ConstructionSettlementResolver
{
    public ConstructionSettlementResult Resolve(LocalSpatialSnapshot space, IEnumerable<TerrainRegionSnapshot> regions,
        ConstructionPhysicalSnapshot physical, StructuralSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(physical);
        ArgumentNullException.ThrowIfNull(policy);
        TerrainRegionSnapshot[] terrainRegions = regions.Take(LocalSpatialSnapshot.MaximumRegions + 1).ToArray();
        if (terrainRegions.Length is 0 or > LocalSpatialSnapshot.MaximumRegions)
        {
            throw new ArgumentException("Settlement requires a bounded complete local terrain catalog.", nameof(regions));
        }
        var terrain = new TerrainVolumeQueries(terrainRegions);
        var affected = physical.GetStructures().Where(structure => structure.Boxes
            .Where(box => box.Minimum.Z == structure.GroundPlaneZ)
            .Any(box => terrain.Query(box.Volume, requireGroundSupport: true) != SpatialOccupancy.Clear)).ToArray();
        if (affected.Length > 1)
        {
            throw new ArgumentException("Interacting falling assemblies require a coupled collision owner.");
        }

        ActorPresence[] actors = space.Actors.ToArray();
        ConstructionPhysicalSnapshot settled = physical;
        HashSet<EntityId> riders = [];
        if (affected.Length == 1)
        {
            SpatialStructure moving = affected[0];
            ConstructionProject project = physical.Construction.Projects.Single(value => value.Installed?.StructureId == moving.StructureId);
            ValidateIntactPlacement(project, physical);
            riders = FindRiders(moving, actors);
            var scene = new VerticalDescentQuery(space.Scope, terrainRegions,
                physical.GetStructures().Where(value => value.StructureId != moving.StructureId),
                actors.Where(value => !riders.Contains(value.ActorId)));
            long? drop = null;
            foreach (SubunitBox bottom in moving.Boxes.Where(value => value.Minimum.Z == moving.GroundPlaneZ))
            {
                VerticalDescentResult contact = scene.QueryBox(bottom, policy.MaximumDropSubunits);
                if (contact.Status != VerticalDescentStatus.Landed || contact.DropSubunits <= 0
                    || drop is not null && drop != contact.DropSubunits)
                {
                    throw new ArgumentException("Rigid settling requires equal complete first contact beneath every lowest member.");
                }
                drop = contact.DropSubunits;
            }
            long displacement = drop ?? throw new ArgumentException("A settling assembly requires a retained base.");
            if (displacement > policy.SafeMemberDropSubunits || riders.Count != 0 && displacement > policy.SafeActorDropSubunits)
            {
                throw new ArgumentException("The contact requires damage or injury beyond the harmless settlement calibration.");
            }
            _ = policy.FlightMilliseconds(displacement);
            // Relative positions within this one rigid group remain unchanged throughout the shared
            // trajectory. Every outside obstacle remains in the scene for every member and rider.
            foreach (SubunitBox box in moving.Boxes) { ValidateSweep(scene, box, displacement); }
            foreach (ActorPresence actor in actors.Where(value => riders.Contains(value.ActorId)))
            {
                ValidateSweep(scene, Box(BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions)), displacement);
            }
            settled = physical.WithMembers(physical.Revision, physical.Members
                .Where(value => value.StructureId == moving.StructureId)
                .Select(value => value.WithState(value.Revision, value.ConditionParts,
                    value.Displacement.Offset(0, 0, -displacement), ConstructionMemberDisposition.Resting)));
            actors = actors.Select(actor => riders.Contains(actor.ActorId)
                ? actor.At(actor.Feet.Offset(0, 0, -displacement)) : actor).ToArray();
        }

        // Restore settled material before independent bodies seek contact. Other bodies remain solid
        // obstacles; a body cannot become a floor or disappear because it also needs to fall.
        for (int index = 0; index < actors.Length; index++)
        {
            ActorPresence actor = actors[index];
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            long stepHeight = Math.Min(LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits,
                actor.Condition.Definition.ClearanceDimensions.HeightSubunits / 4);
            if (FootingQuery.QueryStepped(terrain, settled.GetStructures(), volume, stepHeight) == SpatialOccupancy.Clear)
            {
                continue;
            }
            if (riders.Contains(actor.ActorId))
            {
                throw new ArgumentException("A rigid rider must retain complete contact with its settled assembly.");
            }
            var scene = new VerticalDescentQuery(space.Scope, terrainRegions, settled.GetStructures(), actors);
            VerticalDescentResult contact = scene.QueryActor(actor.ActorId, policy.MaximumDropSubunits);
            if (contact.Status != VerticalDescentStatus.Landed || contact.DropSubunits <= 0
                || contact.DropSubunits > policy.SafeActorDropSubunits)
            {
                throw new ArgumentException("The affected actor requires unresolved collision, flight or injury handling.");
            }
            _ = policy.FlightMilliseconds(contact.DropSubunits);
            actors[index] = actor.At(actor.Feet.Offset(0, 0, -contact.DropSubunits));
        }
        var candidate = new LocalSpatialSnapshot(space.Scope, terrainRegions, actors, settled.GetStructures(), space.GroundSupport);
        settled.ValidateStableGeometry(candidate);
        return new(candidate, settled);
    }

    private static void ValidateIntactPlacement(ConstructionProject project, ConstructionPhysicalSnapshot physical)
    {
        ConstructionMemberState first = physical.GetMember(project.Installed!.StructureId, 0);
        foreach (ConstructionMemberState member in physical.Members.Where(value => value.StructureId == project.Installed.StructureId))
        {
            MillimetreBox box = project.Definition.Members[(int)member.MemberIndex];
            if (member.Displacement != first.Displacement || member.Disposition != first.Disposition
                || member.Displacement.X != 0 || member.Displacement.Y != 0 || member.Displacement.Z > 0
                || member.Disposition == ConstructionMemberDisposition.Falling
                || (Int128)box.MinZ * StructureBearingSurface.TicksPerMillimetre % StructureBearingSurface.TicksPerSubunit != 0
                || (Int128)box.MaxZ * StructureBearingSurface.TicksPerMillimetre % StructureBearingSurface.TicksPerSubunit != 0)
            {
                throw new ArgumentException("Intact settling requires uniform vertical placement and exact subunit contact planes.");
            }
        }
    }

    private static HashSet<EntityId> FindRiders(SpatialStructure moving, IEnumerable<ActorPresence> actors)
    {
        HashSet<EntityId> riders = [];
        const long ticks = StructureBearingSurface.TicksPerSubunit;
        foreach (ActorPresence actor in actors)
        {
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            var footprint = new BoundaryRectangle((Int128)volume.MinimumX * ticks, (Int128)volume.MinimumY * ticks,
                (Int128)volume.MaximumX * ticks, (Int128)volume.MaximumY * ticks);
            var surfaces = moving.BearingSurfaces.Where(surface => surface.TopTicks == (Int128)actor.Feet.Z * ticks
                && surface.Bounds.MinU < footprint.MaxU && surface.Bounds.MaxU > footprint.MinU
                && surface.Bounds.MinV < footprint.MaxV && surface.Bounds.MaxV > footprint.MinV).ToArray();
            if (surfaces.Length == 0) { continue; }
            _ = StructuralContactWeights.Resolve(footprint, surfaces.Select(surface =>
                new StructuralContactSurface(moving.StructureId, surface.Bounds, surface.StandingZ, surface.TopTicks)));
            riders.Add(actor.ActorId);
        }
        return riders;
    }

    private static void ValidateSweep(VerticalDescentQuery scene, SubunitBox box, long drop)
    {
        VerticalDescentResult result = scene.QueryBox(box, drop);
        if (result.DropSubunits != drop || result.Status is not VerticalDescentStatus.Landed and not VerticalDescentStatus.NoContactWithinLimit)
        {
            throw new ArgumentException("Every moving member and rider requires an unobstructed complete swept trajectory.");
        }
    }

    private static SubunitBox Box(BodyVolume volume) => new(new(volume.MinimumX, volume.MinimumY, volume.MinimumZ),
        new(volume.MaximumX, volume.MaximumY, volume.MaximumZ));
}

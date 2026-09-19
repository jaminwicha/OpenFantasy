using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Spatial;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class StructuralFootingBoundaryTests
{
    [Fact]
    public void Bound_actor_load_can_survive_one_lost_half_footprint_while_actual_footing_is_invalid()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        EntityId playerId = AdmissionFixture.Id(10);
        ActorPresence[] actors = basis.Space.Actors.Select(actor => At(actor,
            new SubunitPosition(actor.ActorId == playerId ? 256 : 768, 128, 256))).ToArray();
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions, actors);
        WorldSnapshot world = ConstructionWorldFixture.Copy(basis, space: space);
        ActorPresence player = space.GetActor(playerId);
        Assert.Equal(128, player.Condition.Definition.ClearanceDimensions.WidthSubunits);
        Assert.Equal(128, player.Condition.Definition.ClearanceDimensions.DepthSubunits);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(playerId, player.Feet));

        TerrainRegionSnapshot terrain = Assert.Single(space.Regions);
        StableTerrainBearing[] anchors = Enumerable.Range(0, 4).Select(x =>
        {
            TerrainCell cell = terrain.GetCell(new CellCoordinate(x, 0, 0));
            Assert.True(cell.IsSolid);
            return new StableTerrainBearing(Id((ulong)x + 1), cell.Coordinate,
                cell.SourceId!.Value, cell.Material!, new(1_000_000));
        }).ToArray();
        var bodies = actors.Select(actor => new StructuralBodyMass(actor.Binding.DefinitionId,
            actor.Binding.DefinitionVersion, new(1_000))).DistinctBy(value => value.DefinitionId);
        var masses = new StructuralMassCatalog(bodies,
            [new(GatheringFixture.Axe, new(50)), new(world.Construction!.Definitions.Single().RequiredTool.Item, new(75))]);
        ConstructionBearingModel model = ConstructionBearingModel.Bind(world, [], anchors, masses);
        EntityId actorElement = ConstructionBearingModel.ActorIdentity(playerId);
        StructuralBearing[] contacts = model.Graph.Bearings.Where(value => value.SupportedElementId == actorElement).ToArray();
        Assert.Equal(2, contacts.Length);
        Assert.Equal(new[] { anchors[0].ElementId, anchors[1].ElementId },
            contacts.Select(value => value.SupportingElementId).Order());
        // The actor's 128-by-128 footprint straddles x=256: each real cell owns a
        // 64-by-128 patch in subunits, converted exactly to squared 1/256 mm ticks.
        Assert.All(contacts, contact => Assert.Equal(8_192_000_000UL, contact.Weight));
        Assert.Equal((ushort)1, model.Graph.Elements.Single(value => value.ElementId == actorElement).MinimumSupportCount);
        UInt128 actorLoad = masses.ActorLoad(world, playerId);

        StructuralLoadResolution original = Finish(StructuralLoadResolution.Begin(model.Graph, []));
        Assert.Empty(original.Failures);
        Assert.Equal(actorLoad / 2, original.ActiveLoads[anchors[0].ElementId]);
        Assert.Equal(actorLoad / 2, original.ActiveLoads[anchors[1].ElementId]);
        StructuralLoadResolution planned = Finish(StructuralLoadResolution.Begin(model.Graph, [anchors[0].ElementId]));
        Assert.Equal(StructuralFailureReason.Removed, Assert.Single(planned.Failures).Reason);
        Assert.True(planned.ActiveLoads.ContainsKey(actorElement));
        Assert.Equal(actorLoad, planned.ActiveLoads[actorElement]);
        Assert.Equal(actorLoad, planned.ActiveLoads[anchors[1].ElementId]);

        // Removing the same physical cell is a separate occupancy question. A retained
        // load edge can accept redistributed mass without covering the missing half-footprint.
        TerrainRegionSnapshot excavated = terrain.Excavate(terrain.Revision, [anchors[0].Cell]);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(space.Scope, [excavated], actors));
        ActorPresence[] safelyPlaced = actors.Select(actor => actor.ActorId == playerId
            ? At(actor, new SubunitPosition(384, 128, 256)) : actor).ToArray();
        var geometry = new LocalSpatialSnapshot(space.Scope, [excavated], safelyPlaced);
        Assert.Equal(SpatialOccupancy.Unsupported, geometry.QueryOccupancy(playerId, player.Feet));
        Assert.Equal(SpatialOccupancy.Clear, geometry.QueryOccupancy(playerId, safelyPlaced.Single(value => value.ActorId == playerId).Feet));
        Assert.Equal(world.ContentHash, model.WorldHash);
        Assert.True(world.Space.Regions.Single().GetCell(anchors[0].Cell).IsSolid);
    }

    [Fact]
    public void A_newly_exposed_lower_contact_requires_new_geometry_and_load_attachment()
    {
        ActorPresence actor = SpatialFixture.Actor(1, new(128, 128, 512), SpatialFixture.Body(32, 32, 128));
        var lowerBox = new SubunitBox(new(0, 0, 256), new(256, 256, 480));
        var upperBox = new SubunitBox(new(0, 0, 480), new(256, 256, 512));
        var lowerSurface = new StructureBearingSurface(new(0, 0, 256_000, 256_000), 480_000);
        var upperSurface = new StructureBearingSurface(new(0, 0, 256_000, 256_000), 512_000);
        var assembled = new SpatialStructure(Id(20), [lowerBox, upperBox], [lowerSurface, upperSurface]);
        TerrainRegionSnapshot terrain = SpatialFixture.Terrain();
        var initial = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], [assembled],
            GroundSupportMode.SteppedTerrainAndConstruction);
        Assert.Equal(SpatialOccupancy.Clear, initial.QueryOccupancy(actor.ActorId, actor.Feet));

        // This generic graph explicitly mirrors the original uppermost contact: actor ->
        // upper member -> lower member -> foundation. It claims no funded world admission.
        EntityId actorElement = ConstructionBearingModel.ActorIdentity(actor.ActorId);
        var graph = new StructuralLoadGraph(
            [new(actorElement, new(0), new(100), new(1_000), 10_000, false),
                new(Id(21), new(10), new(0), new(1_000), 10_000, false),
                new(Id(22), new(20), new(0), new(1_000), 10_000, false),
                new(Id(23), new(0), new(0), new(1_000), 10_000, true, 0)],
            [new(Id(31), actorElement, Id(21), 1), new(Id(32), Id(21), Id(22), 1), new(Id(33), Id(22), Id(23), 1)]);
        Assert.Empty(Finish(StructuralLoadResolution.Begin(graph, [])).Failures);
        StructuralLoadResolution planned = Finish(StructuralLoadResolution.Begin(graph, [Id(21)]));
        Assert.Equal(StructuralFailureReason.Unsupported, planned.Failures.Single(value => value.ElementId == actorElement).Reason);
        Assert.True(planned.ActiveLoads.ContainsKey(Id(22)));

        var remaining = new SpatialStructure(assembled.StructureId, [lowerBox], [lowerSurface]);
        Assert.Throws<ArgumentException>(() => initial.WithStructures([remaining]));
        ActorPresence lowered = At(actor, new SubunitPosition(128, 128, 480));
        var candidateGeometry = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [lowered], [remaining],
            GroundSupportMode.SteppedTerrainAndConstruction);
        Assert.Equal(SpatialOccupancy.Unsupported, candidateGeometry.QueryOccupancy(actor.ActorId, actor.Feet));
        Assert.Equal(SpatialOccupancy.Clear, candidateGeometry.QueryOccupancy(actor.ActorId, lowered.Feet));
        Assert.DoesNotContain(graph.Bearings, value => value.SupportedElementId == actorElement && value.SupportingElementId == Id(22));
        Assert.Equal(new SubunitPosition(128, 128, 512), initial.GetActor(actor.ActorId).Feet);
    }

    private static ActorPresence At(ActorPresence actor, SubunitPosition feet) => new(actor.Binding,
        actor.Condition, actor.Scope, feet, actor.Revision, actor.HomePlaceId);

    private static EntityId Id(ulong ordinal) => new(0x666f6f74696e67, ordinal);

    private static StructuralLoadResolution Finish(StructuralLoadResolution state)
    {
        int calls = 0;
        while (!state.IsComplete)
        {
            Assert.InRange(calls++, 0, 16);
            state = state.Advance();
        }
        return state;
    }
}

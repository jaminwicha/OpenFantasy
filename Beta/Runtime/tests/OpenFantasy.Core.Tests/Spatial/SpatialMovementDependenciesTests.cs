using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialMovementDependenciesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_reads_keep_real_neighbour_collision_and_omit_distant_bodies(bool blocked)
    {
        var scene = Scene(blocked);
        var actor = scene.Actors.First();
        var prepared = SpatialMovementDependencies.Capture(scene, actor.ActorId, actor.Revision, 256, 0, 1);
        Assert.Single(prepared.Terrain.References);
        Assert.Equal(blocked ? 2 : 1, prepared.Actors.Count);
        Assert.DoesNotContain(prepared.Actors, value => value.Feet.X > 100 * 256);
        var actual = Evaluate(prepared, scene);
        var expected = scene.PrepareMove(actor.ActorId, actor.Revision, 256, 0);
        Assert.Equal(blocked ? SpatialMoveStatus.BlockedActor : SpatialMoveStatus.Prepared, actual.Status);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Candidate?.Feet, actual.Candidate?.Feet);
        prepared.ValidateCurrent(scene);
    }

    [Fact]
    public void Unrelated_movement_is_allowed_but_a_newly_arriving_collider_invalidates_preparation()
    {
        var scene = Scene(false);
        var mover = scene.Actors.First();
        var far = scene.Actors.Last();
        var prepared = SpatialMovementDependencies.Capture(scene, mover.ActorId, 0, 256, 0, 1);
        var unrelated = scene.WithActors([mover, At(far, far.Feet.Offset(256, 0, 0), 1)]);
        prepared.ValidateCurrent(unrelated);
        var arrived = scene.WithActors([mover, At(far, new(512, 128, 256), 1)]);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(arrived));
        var fresh = SpatialMovementDependencies.Capture(arrived, mover.ActorId, 0, 256, 0, 1);
        Assert.Equal(SpatialMoveStatus.BlockedActor, Evaluate(fresh, arrived).Status);
        Assert.Equal(SpatialMoveStatus.Prepared, Evaluate(prepared, scene).Status);
    }

    [Fact]
    public void Departures_position_changes_and_form_changes_refuse_even_without_a_revision_bump()
    {
        var scene = Scene(true);
        var mover = scene.Actors.First();
        var blocker = scene.Actors[1];
        var prepared = SpatialMovementDependencies.Capture(scene, mover.ActorId, 0, 256, 0, 1);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(scene.WithActors(scene.Actors.Where(actor => actor != blocker))));
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(scene.WithActors(scene.Actors.Select(actor =>
            actor == mover ? At(actor, actor.Feet.Offset(-32, 0, 0), actor.Revision) : actor))));
        var reformed = SpatialFixture.Actor(1, mover.Feet, SpatialFixture.Body(width: 181, id: 9001));
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(scene.WithActors(scene.Actors.Select(actor =>
            actor == mover ? reformed : actor))));
        Assert.Throws<InvalidOperationException>(() => SpatialMovementDependencies.Capture(scene, mover.ActorId, 1, 256, 0, 1));
    }

    [Fact]
    public void Introduced_structures_and_changed_bearing_surfaces_are_dependencies()
    {
        var scene = Scene(false);
        var actor = scene.Actors.First();
        var empty = SpatialMovementDependencies.Capture(scene, actor.ActorId, 0, 256, 0, 1);
        var wall = new SpatialStructure(SpatialFixture.Id(6000), [new SubunitBox(new(256, 0, 256), new(320, 256, 768))]);
        var blocked = scene.WithStructures([wall]);
        Assert.Throws<InvalidOperationException>(() => empty.ValidateCurrent(blocked));
        var prepared = SpatialMovementDependencies.Capture(blocked, actor.ActorId, 0, 256, 0, 1);
        Assert.Single(prepared.Structures);
        Assert.Equal(SpatialMoveStatus.BlockedStructure, Evaluate(prepared, blocked).Status);
        var supporting = new SpatialStructure(wall.StructureId, wall.Boxes,
            [new StructureBearingSurface(new(256000, 0, 320000, 256000), 768000)]);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(blocked.WithStructures([supporting])));
    }

    [Fact]
    public void Equivalent_reconstructed_dependencies_match_but_terrain_edits_and_support_modes_do_not()
    {
        var scene = Scene(false);
        var actor = scene.Actors.First();
        var prepared = SpatialMovementDependencies.Capture(scene, actor.ActorId, 0, 256, 0, 1);
        var equivalent = new LocalSpatialSnapshot(scene.Scope,
            scene.Regions.Select(region => TerrainRegionCodecV1.Decode(TerrainRegionCodecV1.Encode(region))),
            scene.Actors.Select(value => At(value, value.Feet, value.Revision)));
        prepared.ValidateCurrent(equivalent);
        var near = scene.Regions.Single(region => region.Bounds.Minimum.X < 0);
        var edited = near.Excavate(near.Revision, [new(-4, -4, 0)]);
        var changed = new LocalSpatialSnapshot(scene.Scope, scene.Regions.Select(region => region == near ? edited : region), scene.Actors);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(changed));
        var changedMode = new LocalSpatialSnapshot(scene.Scope, scene.Regions, scene.Actors,
            groundSupport: GroundSupportMode.TerrainAndConstruction);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(changedMode));
    }

    [Fact]
    public void Stair_evaluation_keeps_actual_support_and_requires_the_bound_terrain_plan()
    {
        var actor = SpatialFixture.Actor(1, new(160, 128, 256));
        var scene = StairWalkingTests.Scene(actor, StairWalkingTests.Tread(1, 256, 480, 320));
        var prepared = SpatialMovementDependencies.Capture(scene, actor.ActorId, 0, 32, 0, 1);
        var move = Evaluate(prepared, scene);
        Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
        Assert.Equal(320, move.Candidate!.Feet.Z);
        var other = new TerrainReadPlan(scene.Scope, prepared.Footprint.Bounds, prepared.Terrain.References, 1);
        Assert.Throws<ArgumentException>(() => prepared.Evaluate(other.Resolve(scene.Regions)));
    }

    private static LocalSpatialSnapshot Scene(bool blocked)
    {
        var actors = new List<ActorPresence> { SpatialFixture.Actor(1, new(128, 128, 256)) };
        if (blocked) actors.Add(SpatialFixture.Actor(2, new(512, 128, 256)));
        actors.Add(SpatialFixture.Actor(3, new(102 * 256, 128, 256)));
        return new(SpatialFixture.Scope,
            [SpatialFixture.Terrain(width: 8, depth: 8, height: 6), SpatialFixture.Terrain(x: 100, width: 8, depth: 8, height: 6)], actors);
    }

    private static ActorPresence At(ActorPresence actor, SubunitPosition feet, ulong revision) =>
        new(actor.Binding, actor.Condition, actor.Scope, feet, revision, actor.HomePlaceId);

    private static SpatialMoveResult Evaluate(SpatialMovementDependencies prepared, LocalSpatialSnapshot source) =>
        prepared.Evaluate(prepared.Terrain.Resolve(source.Regions.Where(region => prepared.Terrain.References.Any(reference => reference.Bounds == region.Bounds))));
}

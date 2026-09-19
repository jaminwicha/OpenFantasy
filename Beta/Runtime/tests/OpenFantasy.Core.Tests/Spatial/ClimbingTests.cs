using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class ClimbingTests
{
    [Fact]
    public void Assisted_ascent_and_descent_use_the_whole_body_and_increment_presence_revision()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256), Climber());
        var space = Scene(actor, [new(1, 0, 1)]);
        var up = space.PrepareClimb(actor.ActorId, 0, 256, 0, 256);
        Assert.Equal(SpatialMoveStatus.Prepared, up.Status);
        Assert.Equal(new SubunitPosition(384, 128, 512), up.Candidate!.Feet);
        Assert.Equal(1UL, up.Candidate.Revision);
        space = space.WithActors([up.Candidate]);
        var down = space.PrepareClimb(actor.ActorId, 1, -256, 0, -256);
        Assert.Equal(SpatialMoveStatus.Prepared, down.Status);
        Assert.Equal(actor.Feet, down.Candidate!.Feet);
    }

    [Fact]
    public void Valid_endpoint_does_not_allow_the_lift_to_pass_through_a_low_ceiling()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256), Climber());
        var terrain = SpatialFixture.Terrain(keptSolids: [new(1, 0, 1)]);
        // A separate installed beam leaves the actor's starting body clear, but blocks its upward sweep.
        var beam = new SpatialStructure(SpatialFixture.Id(12345),
            [new SubunitBox(new(-128, 0, 256), new(-64, 256, 768)),
             new SubunitBox(new(-128, 0, 768), new(256, 256, 832))]);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], [beam], GroundSupportMode.TerrainAndConstruction);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, new(384, 128, 512)));
        Assert.Equal(SpatialMoveStatus.BlockedStructure, space.PrepareClimb(actor.ActorId, 0, 256, 0, 256).Status);
    }

    [Fact]
    public void Climbing_cannot_jump_an_unsupported_gap_between_two_platforms()
    {
        var actor = SpatialFixture.Actor(1, new(64, 128, 512), Climber(32, 32, 128));
        var first = Platform(1, 0, 96, 512);
        var second = Platform(2, 160, 256, 576);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor],
            [first, second], GroundSupportMode.TerrainAndConstruction);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, new(224, 128, 576)));
        Assert.Equal(SpatialMoveStatus.Unsupported, space.PrepareClimb(actor.ActorId, 0, 160, 0, 64).Status);
    }

    [Fact]
    public void Manipulation_revision_mode_and_body_height_are_real_capability_gates()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        Assert.Equal(SpatialMoveStatus.Unavailable, Scene(actor, [new(1, 0, 1)]).PrepareClimb(actor.ActorId, 0, 256, 0, 256).Status);
        actor = SpatialFixture.Actor(1, actor.Feet, Climber());
        var space = Scene(actor, [new(1, 0, 1)]);
        Assert.Equal(SpatialMoveStatus.RevisionConflict, space.PrepareClimb(actor.ActorId, 9, 256, 0, 256).Status);
        var legacy = new LocalSpatialSnapshot(space.Scope, space.Regions, space.Actors);
        Assert.Equal(SpatialMoveStatus.Unavailable, legacy.PrepareClimb(actor.ActorId, 0, 256, 0, 256).Status);
        actor = SpatialFixture.Actor(1, actor.Feet, Climber(height: 128));
        Assert.Equal(SpatialMoveStatus.Unavailable, Scene(actor, [new(1, 0, 1)]).PrepareClimb(actor.ActorId, 0, 256, 0, 256).Status);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 0, 0)]
    [InlineData(257, 0, 1)]
    [InlineData(1, 0, -257)]
    [InlineData(long.MinValue, 0, 1)]
    public void Unsupported_displacements_refuse_before_geometry(long x, long y, long z) =>
        Assert.Throws<ArgumentException>(() => new ClimbIntent(x, y, z));

    private static BodyDefinition Climber(long width = 180, long depth = 140, long height = 464) => new(
        SpatialFixture.Id(9001), BodyNature.Corporeal, new(width, depth, height),
        [new BodyPartDefinition(new("support"), null, new(16, 16, 16), [BodyFunction.GroundContact, BodyFunction.Manipulation])],
        [new LocomotionRequirement(LocomotionMode.Ground, 1)]);

    private static LocalSpatialSnapshot Scene(ActorPresence actor, IEnumerable<CellCoordinate> retained) =>
        new(SpatialFixture.Scope, [SpatialFixture.Terrain(keptSolids: retained)], [actor],
            groundSupport: GroundSupportMode.TerrainAndConstruction);

    private static SpatialStructure Platform(ulong id, long x0, long x1, long top) => new(SpatialFixture.Id(1000 + id),
        [new SubunitBox(new(x0, 0, 256), new(x1, 256, top))],
        [new StructureBearingSurface(new((Int128)x0 * 1000, 0, (Int128)x1 * 1000, 256000), (Int128)top * 1000)]);
}

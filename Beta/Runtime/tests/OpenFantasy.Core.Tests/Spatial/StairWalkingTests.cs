using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class StairWalkingTests
{
    [Fact]
    public void Ordinary_samples_ascend_and_descend_real_treads_without_manipulation()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var space = Scene(actor, Tread(1, 256, 480, 320), Tread(2, 480, 704, 384));
        for (int i = 0; i < 15; i++)
        {
            var move = space.PrepareMove(actor.ActorId, (ulong)i, 32, 0);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            space = space.WithActors([move.Candidate!]);
        }
        Assert.Equal(new SubunitPosition(608, 128, 384), space.GetActor(actor.ActorId).Feet);
        for (int i = 0; i < 15; i++)
        {
            var move = space.PrepareMove(actor.ActorId, (ulong)(15 + i), -32, 0);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            space = space.WithActors([move.Candidate!]);
        }
        Assert.Equal(actor.Feet, space.GetActor(actor.ActorId).Feet);
    }

    [Fact]
    public void Mixed_tread_contact_requires_real_contact_at_the_foot_height()
    {
        var actor = SpatialFixture.Actor(1, new(192, 128, 320));
        var space = Scene(actor, Tread(1, 256, 480, 320));
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, actor.Feet));
        Assert.Equal(SpatialOccupancy.Unsupported, space.QueryOccupancy(actor.ActorId, actor.Feet.Offset(0, 0, 1)));
        var moved = space.PrepareMove(actor.ActorId, 0, 32, 0);
        Assert.Equal(SpatialMoveStatus.Prepared, moved.Status);
    }

    [Fact]
    public void Too_high_a_riser_or_a_body_spanning_three_levels_refuses()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var tall = Scene(actor, Tread(1, 256, 480, 321));
        Assert.NotEqual(SpatialMoveStatus.Prepared, tall.PrepareMove(actor.ActorId, 0, 64, 0).Status);
        var narrow = Scene(actor, Tread(1, 256, 288, 320), Tread(2, 288, 512, 384));
        var first = narrow.PrepareMove(actor.ActorId, 0, 64, 0);
        Assert.Equal(SpatialMoveStatus.Prepared, first.Status);
        narrow = narrow.WithActors([first.Candidate!]);
        Assert.NotEqual(SpatialMoveStatus.Prepared, narrow.PrepareMove(actor.ActorId, 1, 32, 0).Status);
    }

    [Fact]
    public void Legacy_constructed_support_does_not_silently_acquire_stair_walking()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor],
            [Tread(1, 256, 480, 320)], GroundSupportMode.TerrainAndConstruction);
        Assert.Equal(SpatialMoveStatus.BlockedStructure, space.PrepareMove(actor.ActorId, 0, 64, 0).Status);
    }

    [Fact]
    public void Quantized_physical_tops_retain_contact_and_upward_sweeps_respect_overhangs()
    {
        var actor = SpatialFixture.Actor(1, new(160, 128, 256));
        var fractional = new SpatialStructure(SpatialFixture.Id(3001),
            [new SubunitBox(new(256, 0, 256), new(480, 256, 282))],
            [new StructureBearingSurface(new(256000, 0, 480000, 256000), 281600)]);
        var space = Scene(actor, fractional);
        var move = space.PrepareMove(actor.ActorId, 0, 32, 0);
        Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
        Assert.Equal(282, move.Candidate!.Feet.Z);

        var beam = new SpatialStructure(SpatialFixture.Id(3002),
            [new SubunitBox(new(-128, 0, 256), new(-64, 256, 768)),
             new SubunitBox(new(-128, 0, 768), new(256, 256, 832))]);
        space = Scene(actor, Tread(1, 256, 640, 320), beam);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, new(416, 128, 320)));
        Assert.Equal(SpatialMoveStatus.BlockedStructure, space.PrepareMove(actor.ActorId, 0, 256, 0).Status);
    }

    internal static SpatialStructure Tread(ulong id, long minX, long maxX, long top) => new(
        SpatialFixture.Id(2000 + id), [new SubunitBox(new(minX, 0, 256), new(maxX, 256, top))],
        [new StructureBearingSurface(new((Int128)minX * 1000, 0, (Int128)maxX * 1000, 256000), (Int128)top * 1000)]);

    internal static LocalSpatialSnapshot Scene(ActorPresence actor, params SpatialStructure[] structures) =>
        new(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], structures,
            GroundSupportMode.SteppedTerrainAndConstruction);
}

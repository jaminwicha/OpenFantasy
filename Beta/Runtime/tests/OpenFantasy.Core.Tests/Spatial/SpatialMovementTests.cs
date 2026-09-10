using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialMovementTests
{
    [Fact]
    public void PreparedMovePreservesActorFormConditionHomeAndOriginalState()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), revision: 7);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        SpatialMoveResult result = scene.PrepareMove(actor.ActorId, 7, -32, 0);
        Assert.Equal(SpatialMoveStatus.Prepared, result.Status);
        ActorPresence candidate = Assert.IsType<ActorPresence>(result.Candidate);
        Assert.Equal(new SubunitPosition(96, 128, 256), candidate.Feet);
        Assert.Equal(8UL, candidate.Revision);
        Assert.Same(actor.Binding, candidate.Binding);
        Assert.Same(actor.Condition, candidate.Condition);
        Assert.Equal(actor.HomePlaceId, candidate.HomePlaceId);
        Assert.Equal(actor.Scope, candidate.Scope);
        Assert.Equal(new SubunitPosition(128, 128, 256), actor.Feet);
        Assert.Same(actor, scene.GetActor(actor.ActorId));
        var accepted = new LocalSpatialSnapshot(scene.Scope, scene.Regions, [candidate]);
        Assert.Equal(SpatialMoveStatus.RevisionConflict, accepted.PrepareMove(actor.ActorId, 7, -32, 0).Status);
    }

    [Fact]
    public void HumanAndNpcUseExactlyTheSamePresenceAndMovementRules()
    {
        var player = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var npc = SpatialFixture.Actor(2, new SubunitPosition(128, 896, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), player, npc);
        foreach (ActorPresence actor in scene.Actors)
        {
            SpatialMoveResult result = scene.PrepareMove(actor.ActorId, 0, 0, 256);
            Assert.Equal(SpatialMoveStatus.Prepared, result.Status);
            Assert.Equal(actor.Feet.Offset(0, 256, 0), result.Candidate!.Feet);
            Assert.Equal(1UL, result.Candidate.Revision);
        }
    }

    [Fact]
    public void SweptVolumeBlocksThinActorsEvenWhenDestinationIsClear()
    {
        var body = SpatialFixture.Body(16, 16, 256);
        var moving = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), body);
        var obstacle = SpatialFixture.Actor(2, new SubunitPosition(256, 128, 256), body);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), moving, obstacle);
        Assert.Equal(SpatialOccupancy.Clear, scene.QueryOccupancy(moving.ActorId, moving.Feet.Offset(256, 0, 0)));
        Assert.Equal(SpatialMoveStatus.BlockedActor, scene.PrepareMove(moving.ActorId, 0, 256, 0).Status);
        Assert.Null(scene.PrepareMove(moving.ActorId, 0, 256, 0).Candidate);
    }

    [Fact]
    public void BodyContactIsAllowedButAnyPositiveOverlapBlocks()
    {
        var body = SpatialFixture.Body(128, 128, 256);
        var first = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), body);
        var second = SpatialFixture.Actor(2, new SubunitPosition(384, 128, 256), body);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), first, second);
        Assert.Equal(SpatialMoveStatus.Prepared, scene.PrepareMove(first.ActorId, 0, 128, 0).Status);
        Assert.Equal(SpatialMoveStatus.BlockedActor, scene.PrepareMove(first.ActorId, 0, 129, 0).Status);
    }

    [Fact]
    public void SweptFootprintCannotCrossPartialMissingSupportOrHeadLevelSolid()
    {
        var region = SpatialFixture.Terrain(keptSolids: [new CellCoordinate(1, 0, 2)]);
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var scene = SpatialFixture.Scene(region, actor);
        Assert.Equal(SpatialMoveStatus.BlockedTerrain, scene.PrepareMove(actor.ActorId, 0, 256, 0).Status);
        var floor = SpatialFixture.Terrain();
        floor = floor.Excavate(floor.Revision, [new CellCoordinate(1, 0, 0)]);
        var unsupported = SpatialFixture.Scene(floor, actor);
        Assert.Equal(SpatialMoveStatus.Unsupported, unsupported.PrepareMove(actor.ActorId, 0, 40, 0).Status);
        var boundary = SpatialFixture.Scene(SpatialFixture.Terrain(0, 0, 1, 1, 4), actor);
        Assert.Equal(SpatialMoveStatus.Unavailable, boundary.PrepareMove(actor.ActorId, 0, 40, 0).Status);
    }

    [Fact]
    public void LossOfGroundFunctionStopsMovementWithoutDeletingBodyOccupancy()
    {
        var injury = new BodyInjury(SpatialFixture.Id(6000), SpatialFixture.Id(6001), new BodySlotKey("support"),
            BodyInjuryKind.FunctionLoss, BodyFunction.GroundContact);
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), injuries: [injury]);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        Assert.Equal(SpatialOccupancy.Clear, scene.QueryOccupancy(actor.ActorId, actor.Feet));
        var result = scene.PrepareMove(actor.ActorId, 0, 1, 0);
        Assert.Equal(SpatialMoveStatus.NoGroundLocomotion, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public void CardinalAndNumericLimitsRejectRatherThanClampOrWrap()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        foreach ((long x, long y) in new[] { (0L, 0L), (1L, 1L), (257L, 0L), (-257L, 0L), (long.MinValue, 0L) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => scene.PrepareMove(actor.ActorId, 0, x, y));
        }
        var exhausted = SpatialFixture.Actor(1, actor.Feet, revision: ulong.MaxValue);
        Assert.Equal(SpatialMoveStatus.NumericLimit, SpatialFixture.Scene(SpatialFixture.Terrain(), exhausted)
            .PrepareMove(exhausted.ActorId, ulong.MaxValue, 1, 0).Status);
        long nearMaximum = long.MaxValue - 128;
        var extreme = SpatialFixture.Actor(1, new SubunitPosition(nearMaximum, 128, 256));
        var extremeScene = SpatialFixture.Scene(SpatialFixture.Terrain(SubunitPosition.CellOf(nearMaximum), 0, 2, 1, 4), extreme);
        Assert.Equal(SpatialMoveStatus.NumericLimit, extremeScene.PrepareMove(extreme.ActorId, 0, 256, 0).Status);
    }

    [Fact]
    public void MaximumBodyEnvelopeUsesItsFullVolumeAcrossNegativeCells()
    {
        var body = SpatialFixture.Body(2048, 2048, 2048);
        var actor = SpatialFixture.Actor(1, new SubunitPosition(0, 0, 256), body);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(-5, -5, 11, 11, 16), actor);
        Assert.Equal(SpatialMoveStatus.Prepared, scene.PrepareMove(actor.ActorId, 0, 256, 0).Status);
        Assert.Equal(SpatialOccupancy.Unavailable, scene.QueryOccupancy(actor.ActorId, new SubunitPosition(769, 0, 256)));
    }
}

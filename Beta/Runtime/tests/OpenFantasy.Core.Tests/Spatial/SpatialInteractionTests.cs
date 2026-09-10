using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialInteractionTests
{
    [Fact]
    public void ExactThreeCellRangeIsInclusiveAndTheNextSubunitIsOutside()
    {
        var source = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var boundary = SpatialFixture.Actor(2, new SubunitPosition(896, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), source, boundary);
        Assert.Equal(SpatialInteraction.Reachable, scene.QueryInteraction(source.ActorId, boundary.ActorId));
        Assert.Equal(SpatialInteraction.Reachable, scene.QueryInteraction(boundary.ActorId, source.ActorId));
        var outside = SpatialFixture.Actor(2, new SubunitPosition(897, 128, 256));
        var other = SpatialFixture.Scene(SpatialFixture.Terrain(), source, outside);
        Assert.Equal(SpatialInteraction.OutOfRange, other.QueryInteraction(source.ActorId, outside.ActorId));
    }

    [Fact]
    public void RangeUsesCircleAndExactHalfSubunitCentersOfOddBodies()
    {
        var first = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), SpatialFixture.Body(100, 100, 256));
        var diagonal = SpatialFixture.Actor(2, new SubunitPosition(704, 704, 256), first.Condition.Definition);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), first, diagonal);
        Assert.Equal(SpatialInteraction.OutOfRange, scene.QueryInteraction(first.ActorId, diagonal.ActorId));
        var odd = SpatialFixture.Actor(2, new SubunitPosition(896, 128, 256), SpatialFixture.Body(101, 100, 256, id: 9001));
        var oddScene = SpatialFixture.Scene(SpatialFixture.Terrain(), first, odd);
        Assert.Equal(SpatialInteraction.OutOfRange, oddScene.QueryInteraction(first.ActorId, odd.ActorId));
        var inward = SpatialFixture.Actor(2, new SubunitPosition(895, 128, 256), odd.Condition.Definition);
        var inwardScene = SpatialFixture.Scene(SpatialFixture.Terrain(), first, inward);
        Assert.Equal(SpatialInteraction.Reachable, inwardScene.QueryInteraction(first.ActorId, inward.ActorId));
    }

    [Fact]
    public void DifferentFeetElevationRejectsEvenWhenXyRangeIsSmall()
    {
        var body = SpatialFixture.Body(100, 100, 128);
        var lower = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), body);
        var upper = SpatialFixture.Actor(2, new SubunitPosition(128, 128, 768), body);
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(keptSolids: [new CellCoordinate(0, 0, 2)]), lower, upper);
        Assert.Equal(SpatialInteraction.DifferentFloor, scene.QueryInteraction(lower.ActorId, upper.ActorId));
    }

    [Fact]
    public void SolidWallOccludesUntilItsActualCellIsExcavated()
    {
        var source = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var target = SpatialFixture.Actor(2, new SubunitPosition(896, 128, 256));
        var wall = new CellCoordinate(1, 0, 1);
        var terrain = SpatialFixture.Terrain(keptSolids: [wall]);
        var blocked = SpatialFixture.Scene(terrain, source, target);
        Assert.Equal(SpatialInteraction.Occluded, blocked.QueryInteraction(source.ActorId, target.ActorId));
        terrain = terrain.Excavate(terrain.Revision, [wall]);
        var opened = SpatialFixture.Scene(terrain, source, target);
        Assert.Equal(SpatialInteraction.Reachable, opened.QueryInteraction(source.ActorId, target.ActorId));
        Assert.Equal(SpatialInteraction.Occluded, blocked.QueryInteraction(source.ActorId, target.ActorId));
    }

    [Fact]
    public void CornerAndFaceGrazingCannotSeeThroughClosedSolidCells()
    {
        var source = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var target = SpatialFixture.Actor(2, new SubunitPosition(640, 640, 256));
        var corner = SpatialFixture.Scene(SpatialFixture.Terrain(keptSolids: [new CellCoordinate(1, 0, 1)]), source, target);
        Assert.Equal(SpatialInteraction.Occluded, corner.QueryInteraction(source.ActorId, target.ActorId));
        Assert.Equal(SpatialInteraction.Occluded, corner.QueryInteraction(target.ActorId, source.ActorId));
        source = SpatialFixture.Actor(1, new SubunitPosition(256, 128, 256));
        target = SpatialFixture.Actor(2, new SubunitPosition(256, 640, 256));
        var face = SpatialFixture.Scene(SpatialFixture.Terrain(keptSolids: [new CellCoordinate(0, 1, 1)]), source, target);
        Assert.Equal(SpatialInteraction.Occluded, face.QueryInteraction(source.ActorId, target.ActorId));
    }

    [Fact]
    public void MissingOcclusionCellsAreUnavailableButUnrelatedBoundingCellsAreIgnored()
    {
        var source = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var target = SpatialFixture.Actor(2, new SubunitPosition(896, 128, 256));
        var gap = new LocalSpatialSnapshot(SpatialFixture.Scope,
            [SpatialFixture.Terrain(0, 0, 1, 1, 4), SpatialFixture.Terrain(3, 0, 1, 1, 4)], [source, target]);
        Assert.Equal(SpatialInteraction.Unavailable, gap.QueryInteraction(source.ActorId, target.ActorId));
        target = SpatialFixture.Actor(2, new SubunitPosition(640, 640, 256));
        var regions = (from x in Enumerable.Range(0, 3)
                       from y in Enumerable.Range(0, 3)
                       where x != 0 || y != 2
                       select SpatialFixture.Terrain(x, y, 1, 1, 4)).ToArray();
        var diagonal = new LocalSpatialSnapshot(SpatialFixture.Scope, regions, [source, target]);
        Assert.Equal(SpatialInteraction.Reachable, diagonal.QueryInteraction(source.ActorId, target.ActorId));
    }

    [Fact]
    public void NegativeCoordinateSightlinesAndHugeSeparatedPositionsUseBoundedWideArithmetic()
    {
        var source = SpatialFixture.Actor(1, new SubunitPosition(-896, -128, 256));
        var target = SpatialFixture.Actor(2, new SubunitPosition(-128, -128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), source, target);
        Assert.Equal(SpatialInteraction.Reachable, scene.QueryInteraction(source.ActorId, target.ActorId));
        long low = long.MinValue + 128;
        long high = long.MaxValue - 128;
        source = SpatialFixture.Actor(1, new SubunitPosition(low, 128, 256));
        target = SpatialFixture.Actor(2, new SubunitPosition(high, 128, 256));
        var far = new LocalSpatialSnapshot(SpatialFixture.Scope,
            [SpatialFixture.Terrain(SubunitPosition.CellOf(low), 0, 1, 1, 4),
                SpatialFixture.Terrain(SubunitPosition.CellOf(high), 0, 1, 1, 4)], [source, target]);
        Assert.Equal(SpatialInteraction.OutOfRange, far.QueryInteraction(source.ActorId, target.ActorId));
        Assert.Equal(SpatialInteraction.OutOfRange, far.QueryInteraction(target.ActorId, source.ActorId));
    }

    [Fact]
    public void CenterSegmentUsesBothBodyHeightsAndDoesNotFlattenVerticalOcclusion()
    {
        var source = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), SpatialFixture.Body(height: 128));
        var target = SpatialFixture.Actor(2, new SubunitPosition(896, 128, 256), SpatialFixture.Body(height: 1024, id: 9001));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(keptSolids: [new CellCoordinate(2, 0, 2)]), source, target);
        Assert.Equal(SpatialInteraction.Occluded, scene.QueryInteraction(source.ActorId, target.ActorId));
        Assert.Equal(SpatialInteraction.Occluded, scene.QueryInteraction(target.ActorId, source.ActorId));
    }

    [Fact]
    public void InvalidActorsAndSelfInteractionRemainExplicitCallerErrors()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        Assert.Throws<ArgumentException>(() => scene.QueryInteraction(actor.ActorId, actor.ActorId));
        Assert.Throws<ArgumentException>(() => scene.QueryInteraction(actor.ActorId, SpatialFixture.Id(999)));
        Assert.Throws<ArgumentException>(() => scene.GetActor(SpatialFixture.Id(999)));
        Assert.Throws<ArgumentNullException>(() => new LocalSpatialSnapshot(null!, scene.Regions, scene.Actors));
        Assert.Throws<ArgumentNullException>(() => new LocalSpatialSnapshot(scene.Scope, null!, scene.Actors));
        Assert.Throws<ArgumentNullException>(() => new LocalSpatialSnapshot(scene.Scope, scene.Regions, null!));
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(scene.Scope, [], []));
    }
}

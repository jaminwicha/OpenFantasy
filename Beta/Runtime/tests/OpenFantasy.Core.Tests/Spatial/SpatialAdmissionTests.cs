using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Actors;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialAdmissionTests
{
    [Fact]
    public void SubunitFloorDivisionAndCheckedOffsetsHandleNegativeAndExtremeCoordinates()
    {
        Assert.Equal(new CellCoordinate(-1, -1, -2), new SubunitPosition(-1, -256, -257).Cell);
        Assert.Equal(new CellCoordinate(0, 1, 2), new SubunitPosition(255, 256, 512).Cell);
        Assert.Equal(-36_028_797_018_963_968L, SubunitPosition.CellOf(long.MinValue));
        Assert.Equal(36_028_797_018_963_967L, SubunitPosition.CellOf(long.MaxValue));
        Assert.Throws<OverflowException>(() => new SubunitPosition(long.MaxValue, 0, 0).Offset(1, 0, 0));
        Assert.Throws<OverflowException>(() => new SubunitPosition(0, long.MinValue, 0).Offset(0, -1, 0));
    }

    [Fact]
    public void ActualHumanoidRequiresTwoCellsOfHeadroomWithoutColliderShrinking()
    {
        BodyDefinition humanoid = ActorFixture.Definition("humanoid-candidate");
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), humanoid);
        var region = SpatialFixture.Terrain(keptSolids: [new CellCoordinate(1, 0, 2)]);
        var scene = SpatialFixture.Scene(region, actor);
        Assert.Equal(464, humanoid.ClearanceDimensions.HeightSubunits);
        Assert.Equal(SpatialOccupancy.Clear, scene.QueryOccupancy(actor.ActorId, actor.Feet));
        Assert.Equal(SpatialOccupancy.BlockedTerrain, scene.QueryOccupancy(actor.ActorId, actor.Feet.Offset(256, 0, 0)));
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region,
            SpatialFixture.Actor(2, actor.Feet.Offset(256, 0, 0), humanoid)));
    }

    [Fact]
    public void CompleteFootprintRequiresEveryTouchedFloorCellAndExactFeetElevation()
    {
        var region = SpatialFixture.Terrain();
        region = region.Excavate(region.Revision, [new CellCoordinate(1, 0, 0)]);
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), SpatialFixture.Body(width: 256));
        var scene = SpatialFixture.Scene(region, actor);
        Assert.Equal(SpatialOccupancy.Unsupported, scene.QueryOccupancy(actor.ActorId, new SubunitPosition(129, 128, 256)));
        Assert.Equal(SpatialOccupancy.Unsupported, scene.QueryOccupancy(actor.ActorId, actor.Feet.Offset(0, 0, 1)));
        // Lowering the body crosses into cell Z=0 and asks for support at Z=-1, outside this region.
        Assert.Equal(SpatialOccupancy.Unavailable, scene.QueryOccupancy(actor.ActorId, actor.Feet.Offset(0, 0, -1)));
        var below = new StratifiedTerrainGenerator().Generate(region.Scope, region.Seed, region.Rules,
            new TerrainRegionBounds(new CellCoordinate(region.Bounds.Minimum.X, region.Bounds.Minimum.Y, -1),
                region.Bounds.Width, region.Bounds.Depth, 1));
        var complete = new LocalSpatialSnapshot(scene.Scope, [below, region], [actor]);
        Assert.Equal(SpatialOccupancy.BlockedTerrain, complete.QueryOccupancy(actor.ActorId, actor.Feet.Offset(0, 0, -1)));
    }

    [Fact]
    public void OddDimensionsKeepExtraSubunitPositiveAndHalfOpenMaximumDoesNotTouchNextCell()
    {
        var region = SpatialFixture.Terrain(0, 0, 1, 1, 2);
        var actor = SpatialFixture.Actor(1, new SubunitPosition(127, 127, 256), SpatialFixture.Body(255, 255, 255));
        var scene = SpatialFixture.Scene(region, actor);
        Assert.Equal(SpatialOccupancy.Clear, scene.QueryOccupancy(actor.ActorId, new SubunitPosition(128, 128, 256)));
        Assert.Equal(SpatialOccupancy.Unavailable, scene.QueryOccupancy(actor.ActorId, new SubunitPosition(129, 128, 256)));
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region,
            SpatialFixture.Actor(2, new SubunitPosition(128, 128, 256), SpatialFixture.Body(255, 255, 257))));
    }

    [Fact]
    public void AdjacentHorizontalAndVerticalRegionsProvideCompleteSharedBodyDependencies()
    {
        var whole = SpatialFixture.Terrain(-1, -1, 4, 4, 4);
        var generator = new StratifiedTerrainGenerator();
        TerrainRegionSnapshot Slice(TerrainRegionBounds bounds)
        {
            var result = generator.Generate(whole.Scope, whole.Seed, whole.Rules, bounds);
            var edits = result.Cells.Where(cell => whole.GetCell(cell.Coordinate).IsExcavated)
                .Select(cell => cell.Coordinate).ToArray();
            return edits.Length == 0 ? result : result.Excavate(0, edits);
        }
        var regions = new[]
        {
            Slice(new TerrainRegionBounds(new CellCoordinate(-1, -1, 0), 2, 4, 2)),
            Slice(new TerrainRegionBounds(new CellCoordinate(1, -1, 0), 2, 4, 2)),
            Slice(new TerrainRegionBounds(new CellCoordinate(-1, -1, 2), 4, 4, 2))
        };
        var actor = SpatialFixture.Actor(1, new SubunitPosition(256, 128, 256));
        var scene = new LocalSpatialSnapshot(SpatialFixture.Scope, regions.Reverse(), [actor]);
        Assert.Equal(SpatialOccupancy.Clear, scene.QueryOccupancy(actor.ActorId, actor.Feet));
        Assert.Equal(SpatialMoveStatus.Prepared, scene.PrepareMove(actor.ActorId, 0, 32, 0).Status);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope, regions.Take(2), [actor]));
    }

    [Fact]
    public void OverlapForeignScopeOrDifferentGenerationPinsCannotBeAdmitted()
    {
        var region = SpatialFixture.Terrain();
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope, [region, region], []));
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            [region, SpatialFixture.Terrain(12, -4, scope: SpatialFixture.Scope with { RealmId = SpatialFixture.Id(42) })], []));
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            [region, SpatialFixture.Terrain(12, -4, seed: SpatialFixture.Id(42))], []));
        var alternate = new StratifiedTerrainGenerator().Generate(region.Scope, region.Seed,
            new StratifiedTerrainRules(2, 1, 1, region.Rules.Materials),
            new TerrainRegionBounds(new CellCoordinate(12, -4, 0), 16, 16, 8));
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope, [region, alternate], []));
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256),
            scope: SpatialFixture.Scope with { BranchId = SpatialFixture.Id(43) });
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region, actor));
    }

    [Fact]
    public void ActorAndFormIdentityAliasesOverlappingBodiesAndWrongConditionPinsReject()
    {
        var region = SpatialFixture.Terrain();
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region, actor, actor));
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region, actor,
            SpatialFixture.Actor(2, new SubunitPosition(129, 128, 256))));
        BodyDefinition body = actor.Condition.Definition;
        var sharedForm = ActorFormBinding.Corporeal(SpatialFixture.Id(102), actor.Binding.FormInstanceId,
            SpatialFixture.Id(302), body);
        var other = new ActorPresence(sharedForm, actor.Condition, actor.Scope, actor.Feet.Offset(256, 0, 0), 0, actor.HomePlaceId);
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region, actor, other));
        Assert.Throws<ArgumentException>(() => new ActorPresence(actor.Binding,
            new BodyConditionSnapshot(SpatialFixture.Id(999), body, []), actor.Scope, actor.Feet, 0, actor.HomePlaceId));
        Assert.Throws<ArgumentException>(() => new ActorPresence(actor.Binding, actor.Condition,
            actor.Scope, actor.Feet, 0, actor.ActorId));
    }

    [Fact]
    public void UnsupportedFormsBodyEnvelopesAndNumericBoundsFailAdmissionExplicitly()
    {
        var region = SpatialFixture.Terrain();
        var spiritBody = ActorFixture.Definition("incorporeal-presence-candidate");
        var spirit = ActorFormBinding.IndependentSpirit(SpatialFixture.Id(101), SpatialFixture.Id(201), spiritBody);
        var presence = new ActorPresence(spirit, new BodyConditionSnapshot(spirit.FormInstanceId, spiritBody, []),
            SpatialFixture.Scope, new SubunitPosition(128, 128, 256), 0, SpatialFixture.Id(500));
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region, presence));
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(region,
            SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), SpatialFixture.Body(width: 2049))));
        Assert.Throws<OverflowException>(() => SpatialFixture.Scene(region,
            SpatialFixture.Actor(1, new SubunitPosition(long.MinValue, 128, 256))));
        Assert.Throws<OverflowException>(() => SpatialFixture.Scene(region,
            SpatialFixture.Actor(1, new SubunitPosition(128, 128, long.MaxValue))));
    }

    [Fact]
    public void MaximumCollectionsAreCopiedSortedAndRejectTheirFirstExcessEntry()
    {
        var regions = Enumerable.Range(0, LocalSpatialSnapshot.MaximumRegions)
            .Select(index => SpatialFixture.Terrain(index, 0, 1, 1, 3)).Reverse().ToArray();
        var empty = new LocalSpatialSnapshot(SpatialFixture.Scope, regions, []);
        Assert.Equal(0, empty.Regions[0].Bounds.Minimum.X);
        Assert.Equal(15, empty.Regions[^1].Bounds.Minimum.X);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            regions.Append(SpatialFixture.Terrain(16, 0, 1, 1, 3)), []));
        var terrain = SpatialFixture.Terrain(0, 0, 8, 8, 4);
        var actors = Enumerable.Range(0, LocalSpatialSnapshot.MaximumActors).Select(index => SpatialFixture.Actor(
            (ulong)index, new SubunitPosition(index % 8 * 256 + 128, index / 8 * 256 + 128, 256))).Reverse().ToArray();
        var scene = SpatialFixture.Scene(terrain, actors);
        Assert.Equal(SpatialFixture.Id(100), scene.Actors[0].ActorId);
        Assert.Throws<ArgumentException>(() => SpatialFixture.Scene(terrain,
            [.. actors, SpatialFixture.Actor(70, new SubunitPosition(128, 128, 256))]));
        actors[0] = actors[1];
        regions[0] = regions[1];
        Assert.Equal(64, scene.Actors.Count);
        Assert.Equal(SpatialFixture.Id(163), scene.Actors[^1].ActorId);
        Assert.Equal(15, empty.Regions[^1].Bounds.Minimum.X);
        Assert.Throws<NotSupportedException>(() => ((IList<ActorPresence>)scene.Actors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TerrainRegionSnapshot>)empty.Regions).Clear());
    }
}

using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class StructureGeometryTests
{
    [Fact]
    public void ThinStructureBlocksSweptBodyAlthoughDestinationIsClear()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256), SpatialFixture.Body(16, 16, 256));
        var obstacle = Structure(700, 248, 64, 256, 252, 192, 300);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], [obstacle]);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, actor.Feet.Offset(256, 0, 0)));
        Assert.Equal(SpatialMoveStatus.BlockedStructure, space.PrepareMove(actor.ActorId, 0, 256, 0).Status);
        Assert.Equal(SpatialMoveStatus.Prepared, space.PrepareMove(actor.ActorId, 0, 112, 0).Status);
        Assert.Equal(SpatialMoveStatus.BlockedStructure, space.PrepareMove(actor.ActorId, 0, 113, 0).Status);
    }

    [Fact]
    public void StructureCollectionsAreCopiedAndActorReplacementRetainsCollision()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var boxes = new List<SubunitBox> { Box(500, 0, 256, 510, 256, 800) };
        var obstacle = new SpatialStructure(SpatialFixture.Id(700), boxes);
        boxes.Clear();
        var structures = new List<SpatialStructure> { obstacle };
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], structures);
        structures.Clear();
        var moved = space.PrepareMove(actor.ActorId, 0, 32, 0).Candidate!;
        var candidate = space.WithActors([moved]);
        Assert.Single(candidate.Structures);
        Assert.Single(obstacle.Boxes);
        Assert.Equal(SpatialMoveStatus.BlockedStructure, candidate.PrepareMove(actor.ActorId, 1, 256, 0).Status);
        Assert.Equal(actor.Feet, space.GetActor(actor.ActorId).Feet);
    }

    [Fact]
    public void ElevatedMemberNeedsOnlyDeclaredAssemblyBaseContacts()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(-128, -128, 256));
        var structure = new SpatialStructure(SpatialFixture.Id(700),
            [Box(512, 0, 256, 528, 16, 400), Box(512, 0, 400, 768, 16, 416)]);
        var terrain = SpatialFixture.Terrain();
        terrain = terrain.Excavate(terrain.Revision, [new CellCoordinate(2, 0, 0)]);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], [structure]));
        var supported = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], [structure]);
        Assert.Single(supported.Structures);
    }

    [Fact]
    public void ABaseFaceCannotHideMissingSupportAtOneFootprintEdge()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(-128, -128, 256));
        var terrain = SpatialFixture.Terrain();
        terrain = terrain.Excavate(terrain.Revision, [new CellCoordinate(3, 0, 0)]);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor]);
        var result = space.PrepareStructurePlacement(Structure(700, 760, 32, 256, 780, 64, 280),
            [new GroundAccessRequirement(actor.ActorId, actor.Feet, actor.Feet)]);
        Assert.Equal(StructurePlacementStatus.Unsupported, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public void OverlappingQuantizedMembersWithinOneAssemblyAreAUnion()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var obstacle = new SpatialStructure(SpatialFixture.Id(700),
            [Box(512, 0, 256, 528, 64, 300), Box(520, 32, 256, 540, 96, 320)]);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], [obstacle]);
        Assert.Equal(SpatialOccupancy.BlockedStructure, space.QueryOccupancy(actor.ActorId, new SubunitPosition(520, 32, 256)));
        Assert.Throws<ArgumentException>(() => space.WithStructures([obstacle, Structure(701, 530, 32, 256, 550, 96, 320)]));
    }

    [Fact]
    public void HighStructureOccludesBothDialogueAndResourceInteraction()
    {
        var first = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var second = SpatialFixture.Actor(2, new SubunitPosition(640, 128, 256));
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [first, second],
            [Structure(700, 380, 0, 256, 388, 256, 1000)]);
        Assert.Equal(SpatialInteraction.Occluded, space.QueryInteraction(first.ActorId, second.ActorId));
        Assert.Equal(SpatialInteraction.Occluded, ResourceInteractionQueries.Query(space, first.ActorId, new SubunitPosition(512, 128, 256)));
        var low = space.WithStructures([Structure(701, 380, 0, 256, 388, 256, 300)]);
        Assert.Equal(SpatialInteraction.Reachable, low.QueryInteraction(first.ActorId, second.ActorId));
    }

    [Fact]
    public void PlacementRejectsActorOverlapUnknownVolumeAndConflictingIdentities()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = SpatialFixture.Scene(SpatialFixture.Terrain(0, 0, 4, 4, 4), actor);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, actor.Feet)];
        Assert.Equal(StructurePlacementStatus.BlockedActor,
            space.PrepareStructurePlacement(Structure(700, 128, 128, 256, 140, 140, 300), access).Status);
        Assert.Equal(StructurePlacementStatus.Unavailable,
            space.PrepareStructurePlacement(Structure(700, 1020, 0, 256, 1030, 20, 300), access).Status);
        Assert.Throws<ArgumentException>(() => space.WithStructures([new SpatialStructure(actor.ActorId,
            [Box(512, 0, 256, 520, 20, 300)])]));
    }

    [Fact]
    public void ShapesRejectInvertedUnboundedDuplicateAndExcessiveInputs()
    {
        Assert.Throws<ArgumentException>(() => Box(1, 0, 0, 1, 1, 1));
        Assert.Throws<ArgumentException>(() => Box(2, 0, 0, 1, 1, 1));
        Assert.Throws<ArgumentException>(() => new SpatialStructure(SpatialFixture.Id(700), []));
        var box = Box(0, 0, 256, 1, 1, 257);
        Assert.Throws<ArgumentException>(() => new SpatialStructure(SpatialFixture.Id(700), [box, box]));
        Assert.Throws<ArgumentException>(() => Structure(700, long.MinValue, 0, 256, long.MaxValue, 1, 257));
        Assert.Throws<ArgumentException>(() => new SpatialStructure(SpatialFixture.Id(700),
            Enumerable.Range(0, 33).Select(i => Box(i, 0, 256, i + 1, 1, 257))));
    }

    [Fact]
    public void BaseContactsSupportRaisedPartsWithoutPretendingTheVoidUnderThemIsTerrain()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(-128, -128, 256));
        var terrain = SpatialFixture.Terrain();
        terrain = terrain.Excavate(terrain.Revision, [new CellCoordinate(3, 0, 0)]);
        var assembly = new SpatialStructure(SpatialFixture.Id(700),
            [Box(512, 0, 256, 528, 16, 400), Box(512, 0, 400, 1024, 16, 416)]);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], [assembly]);
        Assert.Single(space.Structures);
        Assert.Equal(SpatialOccupancy.Unsupported, space.QueryOccupancy(actor.ActorId, new SubunitPosition(896, 128, 256)));
    }

    [Fact]
    public void ResourcesCannotUseAnAnchorInsideInstalledMaterial()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor],
            [Structure(700, 500, 100, 256, 520, 160, 300)]);
        Assert.Equal(SpatialOccupancy.BlockedStructure,
            ResourceInteractionQueries.QueryAnchor(space, new SubunitPosition(510, 128, 256)));
        Assert.Equal(SpatialInteraction.Unavailable,
            ResourceInteractionQueries.Query(space, actor.ActorId, new SubunitPosition(510, 128, 256)));
    }

    [Fact]
    public void StructureSpanLimitAndNegativeBoundsRemainExact()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(-640, -640, 256));
        var obstacle = Structure(700, -2048, -256, 256, 0, -128, 300);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain(-10, -4, 16, 16)], [actor], [obstacle]);
        Assert.Single(space.Structures);
        Assert.Throws<ArgumentException>(() => Structure(701, -2049, -256, 256, 0, -128, 300));
        Assert.Equal(SpatialOccupancy.BlockedStructure,
            space.QueryOccupancy(actor.ActorId, new SubunitPosition(-640, -192, 256)));
    }

    internal static SubunitBox Box(long x, long y, long z, long maxX, long maxY, long maxZ) =>
        new(new SubunitPosition(x, y, z), new SubunitPosition(maxX, maxY, maxZ));
    internal static SpatialStructure Structure(ulong id, long x, long y, long z, long maxX, long maxY, long maxZ) =>
        new(SpatialFixture.Id(id), [Box(x, y, z, maxX, maxY, maxZ)]);
}

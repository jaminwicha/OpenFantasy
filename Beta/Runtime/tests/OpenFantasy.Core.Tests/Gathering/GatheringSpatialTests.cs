using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Gathering.GatheringFixture;

namespace OpenFantasy.Core.Tests.Gathering;

public sealed class GatheringSpatialTests
{
    [Fact]
    public void ResourceRangeIsInclusiveAndSymmetricAtNegativeCoordinates()
    {
        WorldSnapshot world = GatheringFixture.Create();
        foreach (long direction in new long[] { -1, 1 })
        {
            Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(world.Space, Id(10),
                new SubunitPosition(direction * 448, 0, 256)));
            Assert.NotEqual(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(world.Space, Id(10),
                new SubunitPosition(direction * 449, 0, 256)));
        }
        Assert.Equal(SpatialInteraction.DifferentFloor,
            ResourceInteractionQueries.Query(world.Space, Id(10), new SubunitPosition(256, 0, 512)));
        Assert.Equal(SpatialInteraction.Unavailable,
            ResourceInteractionQueries.Query(world.Space, Id(10), new SubunitPosition(long.MaxValue, long.MinValue, 256)));
    }

    [Fact]
    public void BodyCenterSightlineIsBlockedEvenWhenTheResourceAnchorHasClearSupport()
    {
        WorldSnapshot original = GatheringFixture.Create();
        TerrainRegionSnapshot raised = new StratifiedTerrainGenerator().Generate(original.Space.Scope, Id(500),
            new StratifiedTerrainRules(1, 0, 1, original.Space.Regions[0].Rules.Materials),
            new TerrainRegionBounds(new CellCoordinate(-2, -2, -1), 8, 8, 5));
        TerrainRegionSnapshot openings = raised.Excavate(0, [new(0, 0, 1), new(2, 0, 1)]);
        ActorPresence actor = original.Space.GetActor(Id(10));
        var shifted = new ActorPresence(actor.Binding, actor.Condition, actor.Scope,
            new SubunitPosition(128, 128, 256), 0, actor.HomePlaceId);
        var space = new LocalSpatialSnapshot(original.Space.Scope, [openings], [shifted]);
        var target = new SubunitPosition(576, 128, 256);
        Assert.Equal(SpatialOccupancy.Clear, ResourceInteractionQueries.QueryAnchor(space, target));
        Assert.NotEqual(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(space, Id(10), target));
        TerrainRegionSnapshot passage = openings.Excavate(1, [new(1, 0, 1)]);
        var cleared = new LocalSpatialSnapshot(space.Scope, [passage], space.Actors);
        Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(cleared, Id(10), target));
    }

    [Fact]
    public void AdmissionRejectsUnsupportedBlockedMissingAndOffPlaneAnchors()
    {
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(feet: new SubunitPosition(256, 0, 0)));
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(feet: new SubunitPosition(256, 0, 257)));
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(feet: new SubunitPosition(long.MaxValue, 0, 256)));
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(feet: new SubunitPosition(256, 0, 512)));
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(feet: new SubunitPosition(256, 256, 256),
            changeSpace: space => new LocalSpatialSnapshot(space.Scope,
                [space.Regions[0].Excavate(0, [new CellCoordinate(1, 1, 0)])], space.Actors)));
    }

    [Fact]
    public void NonblockingNodesCanShareAnActorCellButCannotAliasWorldIdentityKinds()
    {
        WorldSnapshot world = GatheringFixture.Create(feet: new SubunitPosition(0, 0, 256));
        Assert.Equal(WorldActionRejection.None, Plan(world).Rejection);
        foreach (EntityId collision in new[] { Id(10), Id(40), Id(50), Id(60), Id(71), Id(80), Id(90), Id(100) })
        {
            GatheringSnapshot original = world.Gathering!;
            Assert.Throws<ArgumentException>(() => With(world, gathering:
                new GatheringSnapshot(original.WorldId, original.BranchId, original.AdvancingStep,
                    [new(collision, Oak, Source, 20, original.Nodes[0].Feet, 0, 0, 5)], original.Tools, original.Actors)));
        }
    }
}

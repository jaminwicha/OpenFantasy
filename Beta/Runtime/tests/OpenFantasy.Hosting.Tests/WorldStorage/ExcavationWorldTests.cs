using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ExcavationWorldTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Player_and_npc_pay_work_before_one_atomic_whole_cell_yield(bool npc)
    {
        var f = new ExcavationWorldFixture(npc);
        var world = f.Earn(f.Genesis);
        for (int work = 1; work <= 3; work++)
        {
            var decision = f.Step(world, f.Command(world));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            world = decision.Candidate;
            Assert.Equal((ulong)work, world.Excavation!.Sites.Single().CompletedWork);
            var cell = world.Space.Regions.Single(r => r.Bounds.Contains(ExcavationWorldFixture.Target)).GetCell(ExcavationWorldFixture.Target);
            Assert.Equal(work == 3, cell.IsExcavated);
            Assert.Equal(work == 3 ? 1UL : 0UL, world.Inventory.Custody.Lots.Where(l => l.Source.SourceId == f.Site.CountedSourceId).Aggregate(0UL, (n, l) => n + l.Source.Quantity));
            Assert.Equal(world.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ContentHash);
            if (work != 3) world = f.Earn(world);
        }
        var exhausted = f.Step(f.Earn(world), f.Command(f.Earn(world)));
        Assert.Equal(WorldActionRejection.Unavailable, exhausted.Receipts.Single().Rejection);
    }

    [Fact]
    public void Removing_an_occupied_floor_refuses_work_and_preserves_terrain_and_stock()
    {
        var f = new ExcavationWorldFixture(occupiedAbove: true);
        var world = f.Earn(f.Genesis);
        var decision = f.Step(world, f.Command(world));
        Assert.Equal(WorldActionRejection.Unavailable, decision.Receipts.Single().Rejection);
        Assert.Equal(world.Excavation!.ContentHash, decision.Candidate.Excavation!.ContentHash);
        Assert.Equal(world.Inventory.Custody.ContentHash, decision.Candidate.Inventory.Custody.ContentHash);
        Assert.Equal(world.Space.Regions.Last().ComputeContentHash(), decision.Candidate.Space.Regions.Last().ComputeContentHash());
    }
}

using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class TerrainReadPreparationTests
{
    [Fact]
    public void File_backed_reads_load_only_required_pins_and_reuse_real_occupancy_queries()
    {
        var world = WorldServiceFixture.Genesis();
        var basis = world.Space.Regions.Single();
        var generator = new StratifiedTerrainGenerator();
        var left = generator.Generate(basis.Scope, basis.Seed, basis.Rules, new(new(-2, -2, -1), 2, 4, 5));
        var right = generator.Generate(basis.Scope, basis.Seed, basis.Rules, new(new(0, -2, -1), 4, 4, 5));
        var distant = generator.Generate(basis.Scope, basis.Seed, basis.Rules, new(new(100, -2, -1), 4, 4, 5));
        TerrainRegionSnapshot[] regions = [left, right, distant];
        var plan = new TerrainReadPlan(basis.Scope, new(new(-1, -1, 0), 2, 2, 3),
            regions.Select(TerrainRegionReference.Capture), 2);
        var directory = Directory.CreateTempSubdirectory("openfantasy-terrain-read-");
        try
        {
            foreach (var region in regions)
                File.WriteAllBytes(Path.Combine(directory.FullName, region.ComputeContentHash().Value), TerrainRegionCodecV1.Encode(region));
            var reads = new List<ContentHash>();
            var loader = new TerrainReadPreparation((reference, limit) =>
            {
                reads.Add(reference.ContentHash);
                string path = Path.Combine(directory.FullName, reference.ContentHash.Value);
                Assert.True(new FileInfo(path).Length <= limit);
                return File.ReadAllBytes(path);
            });
            var loaded = loader.Load(plan, 2 * TerrainRegionCodecV1.MaximumEncodedByteLength,
                left.Bounds.CellCount + right.Bounds.CellCount);
            Assert.Equal(plan.References.Select(reference => reference.ContentHash), reads);
            Assert.DoesNotContain(distant.ComputeContentHash(), reads);
            var complete = new LocalSpatialSnapshot(basis.Scope, regions, world.Space.Actors);
            var local = new LocalSpatialSnapshot(basis.Scope, loaded.Regions, world.Space.Actors);
            var actor = world.Space.GetActor(WorldServiceFixture.Id(10));
            var expected = complete.PrepareMove(actor.ActorId, actor.Revision, -32, 0);
            var actual = local.PrepareMove(actor.ActorId, actor.Revision, -32, 0);
            Assert.Equal(SpatialMoveStatus.Prepared, actual.Status);
            Assert.Equal(expected.Status, actual.Status);
            Assert.Equal(expected.Candidate!.Feet, actual.Candidate!.Feet);
            Assert.Equal(-1, actual.Candidate.Feet.Cell.X);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Missing_corrupted_stale_and_over_budget_archives_do_not_produce_a_read_set()
    {
        var region = WorldServiceFixture.Genesis().Space.Regions.Single();
        var reference = TerrainRegionReference.Capture(region);
        var plan = new TerrainReadPlan(region.Scope, region.Bounds, [reference], 1);
        byte[] bytes = TerrainRegionCodecV1.Encode(region);
        int cells = region.Bounds.CellCount;
        Assert.Throws<InvalidDataException>(() => new TerrainReadPreparation((_, _) => null).Load(plan, bytes.Length, cells));
        byte[] corrupted = (byte[])bytes.Clone();
        corrupted[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => new TerrainReadPreparation((_, _) => corrupted).Load(plan, bytes.Length, cells));
        var changed = region.Excavate(region.Revision, [region.Cells.First(cell => cell.IsSolid).Coordinate]);
        byte[] stale = TerrainRegionCodecV1.Encode(changed);
        Assert.Throws<InvalidDataException>(() => new TerrainReadPreparation((_, _) => stale)
            .Load(plan, TerrainRegionCodecV1.MaximumEncodedByteLength, cells));
        Assert.Throws<InvalidDataException>(() => new TerrainReadPreparation((_, limit) =>
        {
            Assert.Equal(bytes.Length - 1, limit);
            return bytes;
        }).Load(plan, bytes.Length - 1, cells));
        bool readBeforeBudgetCheck = false;
        Assert.Throws<InvalidOperationException>(() => new TerrainReadPreparation((_, _) =>
        {
            readBeforeBudgetCheck = true;
            return bytes;
        }).Load(plan, bytes.Length, cells - 1));
        Assert.False(readBeforeBudgetCheck);
        var undersized = new TerrainRegionBounds(region.Bounds.Minimum, 1, 1, 1);
        var falsePlan = new TerrainReadPlan(region.Scope, undersized,
            [new(region.Scope, undersized, region.Revision, reference.ContentHash)], 1);
        var headerFailure = Assert.Throws<InvalidDataException>(() =>
            new TerrainReadPreparation((_, _) => bytes).Load(falsePlan, bytes.Length, 1));
        Assert.Contains("header", Assert.IsType<FormatException>(headerFailure.InnerException).Message);
        Assert.Equal(reference, TerrainRegionReference.Capture(
            new TerrainReadPreparation((_, _) => bytes).Load(plan, bytes.Length, cells).Regions.Single()));
    }

    [Fact]
    public void A_later_read_failure_or_cumulative_budget_shortfall_cannot_return_a_partial_set()
    {
        var left = WorldServiceFixture.Genesis().Space.Regions.Single();
        var right = new StratifiedTerrainGenerator().Generate(left.Scope, left.Seed, left.Rules,
            new(left.Bounds.Minimum.Offset(left.Bounds.Width, 0, 0), left.Bounds.Width,
                left.Bounds.Depth, left.Bounds.Height));
        var plan = new TerrainReadPlan(left.Scope, new(new(left.Bounds.Maximum.X, 0, 0), 2, 1, 2),
            new[] { left, right }.Select(TerrainRegionReference.Capture), 2);
        byte[] first = TerrainRegionCodecV1.Encode(left);
        byte[] second = TerrainRegionCodecV1.Encode(right);
        var calls = new List<ContentHash>();
        var unavailable = new TerrainReadPreparation((reference, _) =>
        {
            calls.Add(reference.ContentHash);
            return reference.Bounds == left.Bounds ? first : null;
        });
        Assert.Throws<InvalidDataException>(() => unavailable.Load(plan, first.Length + second.Length,
            plan.TotalLoadedCells));
        Assert.Equal(plan.References.Select(reference => reference.ContentHash), calls);
        var cumulative = new TerrainReadPreparation((reference, limit) =>
        {
            if (reference.Bounds == left.Bounds) return first;
            Assert.Equal(second.Length - 1, limit);
            return second;
        });
        Assert.Throws<InvalidDataException>(() => cumulative.Load(plan, first.Length + second.Length - 1,
            plan.TotalLoadedCells));
        Assert.Equal(0UL, left.Revision);
        Assert.Equal(0UL, right.Revision);
    }
}

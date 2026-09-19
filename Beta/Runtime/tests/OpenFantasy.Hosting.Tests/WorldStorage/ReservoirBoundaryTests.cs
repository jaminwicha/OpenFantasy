using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ReservoirBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualRetainedTerrainSealsFloorsAndEveryExposedWallAcrossRegionSeams(bool split)
    {
        var f = new ReservoirBoundaryFixture(splitRegions: split);
        byte[] before = WorldSnapshotCodec.Encode(f.World);
        var report = f.Assess();
        Assert.True(report.Completed);
        Assert.True(report.IsContained);
        Assert.Equal(f.World.ContentHash, report.WorldHash);
        Assert.Equal(f.World.Reservoirs!.ContentHash, report.ReservoirHash);
        Assert.Equal(f.Policy.Version, report.PolicyHash);
        Assert.All(report.Results, r =>
        {
            Assert.Equal(2, r.InteriorCells);
            Assert.Equal(9, r.BoundaryFaces);
            Assert.Equal(9, r.TerrainSealedFaces);
            Assert.Equal(0, r.ConstructedSealedFaces);
            Assert.Empty(r.Failures);
        });
        Assert.Equal(before, WorldSnapshotCodec.Encode(f.World));
        var reversed = new LocalSpatialSnapshot(f.World.Space.Scope, f.World.Space.Regions.Reverse(), f.World.Space.Actors.Reverse());
        Assert.Equal(report.WorldHash, new ReservoirBoundaryEvaluator().Assess(ReservoirBoundaryFixture.Copy(f.World, reversed, f.World.Reservoirs),
            f.Policy, TestContext.Current.CancellationToken).WorldHash);
    }

    [Theory]
    [InlineData(0, 0, -3, ReservoirBoundaryFailure.UnsealedFloor)]
    [InlineData(-1, 0, -2, ReservoirBoundaryFailure.UnsealedWall)]
    [InlineData(0, 1, -1, ReservoirBoundaryFailure.UnsealedWall)]
    public void ExcavatedFloorOrWallCannotBeInferredFromItsRetainedOriginalMaterial(long x, long y, long z, ReservoirBoundaryFailure reason)
    {
        var f = new ReservoirBoundaryFixture(extraHoles: [new(x, y, z)]);
        var result = f.Assess().Results.Single(r => r.ReservoirId == ReservoirBoundaryFixture.SourceReservoir);
        Assert.False(result.IsContained);
        Assert.Equal(1, result.Failures[reason]);
        Assert.True(result.Completed);
        Assert.Contains(result.Samples, s => s.Failure == reason);
    }

    [Fact]
    public void SteppedFloorsRequireTheRiserWallBelowTheirHigherNeighbor()
    {
        var intact = new ReservoirBoundaryFixture(stepped: true);
        var report = intact.Assess();
        Assert.True(report.IsContained);
        var source = report.Results.Single(r => r.ReservoirId == ReservoirBoundaryFixture.SourceReservoir);
        Assert.Equal(3, source.InteriorCells);
        Assert.Equal(12, source.BoundaryFaces);
        var broken = new ReservoirBoundaryFixture(stepped: true, extraHoles: [new(1, 0, -2)]);
        source = broken.Assess().Results.Single(r => r.ReservoirId == ReservoirBoundaryFixture.SourceReservoir);
        Assert.False(source.IsContained);
        Assert.Contains(source.Samples, s => s.Failure == ReservoirBoundaryFailure.UnsealedWall && s.Surface == ReservoirBoundarySurface.East);
    }

    [Fact]
    public void SolidityUnknownTerrainAndMaterialVersionDoNotStandInForSealing()
    {
        var f = new ReservoirBoundaryFixture();
        var wrongVersion = new ReservoirSealPolicy(ReservoirWorldFixture.Water,
            f.Policy.Rules.Select(r => new ReservoirSealRule(new(r.Material.DefinitionId, ContentHash.Compute("wrong-solid-version"u8)), 1)));
        Assert.False(new ReservoirBoundaryEvaluator().Assess(f.World, wrongVersion, TestContext.Current.CancellationToken).IsContained);
        var blocked = new ReservoirBoundaryFixture(leaveSubstrate: true).Assess();
        Assert.All(blocked.Results, r => Assert.Equal(2, r.Failures[ReservoirBoundaryFailure.InteriorTerrain]));
        var missing = new ReservoirBoundaryFixture(missingWest: true).Assess();
        Assert.Contains(missing.Results, r => r.Failures.ContainsKey(ReservoirBoundaryFailure.MissingTerrain));
        Assert.False(missing.IsContained);
    }

    [Fact]
    public void NominalCapacityCannotIgnoreAnActorsWholeBodyOrTreatAClosedGateAsMaterial()
    {
        var f = new ReservoirBoundaryFixture();
        var actor = f.World.Space.Actors[0];
        // The admitted actor lies in this proposed vessel, although its feet alone are just a boundary point.
        long x = actor.Feet.Cell.X, y = actor.Feet.Cell.Y, bottom = actor.Feet.Cell.Z;
        var source = new ReservoirGeometry(f.World.Space.Scope, ReservoirBoundaryFixture.SourceReservoir, ReservoirWorldFixture.SourceStore,
            bottom + 2, [new(x, y, bottom)]);
        var physical = LiquidReservoirState.Create(f.World.Reservoirs!.Capacity,
            f.World.Reservoirs.Geometries.Select(g => g.ReservoirId == source.ReservoirId ? source : g));
        var report = new ReservoirBoundaryEvaluator().Assess(ReservoirBoundaryFixture.Copy(f.World, f.World.Space, physical), f.Policy,
            TestContext.Current.CancellationToken);
        Assert.Contains(report.Results, r => r.Failures.ContainsKey(ReservoirBoundaryFailure.InteriorActor));
        var gated = new ReservoirTransportWorldFixture(nearby: false).Genesis;
        report = new ReservoirBoundaryEvaluator().Assess(gated, f.Policy, TestContext.Current.CancellationToken);
        Assert.All(report.Results, r => Assert.Equal(1, r.Failures[ReservoirBoundaryFailure.UnprovedGate]));
        Assert.False(report.IsContained);
    }

    [Fact]
    public void WorkAndCoordinateLimitsProduceExplicitIncompleteReports()
    {
        var f = new ReservoirBoundaryFixture();
        foreach (bool enormous in new[] { false, true })
        {
            var source = new ReservoirGeometry(f.World.Space.Scope, ReservoirBoundaryFixture.SourceReservoir,
                ReservoirWorldFixture.SourceStore, enormous ? 1024 : 0,
                enormous ? Enumerable.Range(0, 1024).Select(x => new ReservoirColumn(x, 10, 0)) : [new(long.MaxValue, 10, -2)]);
            var physical = LiquidReservoirState.Create(f.World.Reservoirs!.Capacity,
                f.World.Reservoirs.Geometries.Select(g => g.ReservoirId == source.ReservoirId ? source : g));
            var report = new ReservoirBoundaryEvaluator().Assess(ReservoirBoundaryFixture.Copy(f.World, f.World.Space, physical), f.Policy,
                TestContext.Current.CancellationToken);
            Assert.False(report.Completed);
            Assert.False(report.IsContained);
            Assert.Contains(report.Results, r => r.Failures.ContainsKey(enormous ? ReservoirBoundaryFailure.WorkLimit : ReservoirBoundaryFailure.CoordinateLimit));
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ReservoirBoundaryEvaluator().Assess(f.World, f.Policy, cancellation.Token));
    }

    [Fact]
    public void RecordedReadOnlyAssessmentPinsExactStateAndSurvivesRestartWithoutCreatingWaterOrHistory()
    {
        var f = new ReservoirBoundaryFixture(splitRegions: true);
        using var files = new StorageFiles();
        ulong finalRevision;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, f.World))
        {
            var state = authority.Commit(f.Materials.Actions.Batch(f.World, [], [])).Candidate;
            finalRevision = state.Revision;
            var first = new ReservoirBoundaryEvaluator().Assess(f.World, f.Policy, TestContext.Current.CancellationToken);
            var second = new ReservoirBoundaryEvaluator().Assess(state, f.Policy, TestContext.Current.CancellationToken);
            Assert.True(first.IsContained && second.IsContained);
            Assert.NotEqual(first.WorldHash, second.WorldHash);
            Assert.Equal(first.ReservoirHash, second.ReservoirHash);
        }
        byte[] before = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (ulong revision in new[] { finalRevision, 0UL, finalRevision })
            {
                var recorded = authority.ReadRecordedStateForHost(revision);
                var report = new ReservoirBoundaryEvaluator().Assess(recorded.State, f.Policy, TestContext.Current.CancellationToken);
                Assert.True(report.IsContained);
                Assert.Equal(recorded.StateHash, report.WorldHash);
            }
        }
        Assert.Equal(before, File.ReadAllBytes(files.Path));
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_RESERVOIR_BOUNDARY_EVIDENCE");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            File.Copy(files.Path, Path.Combine(directory, "contained-world.sqlite"), true);
            File.WriteAllBytes(Path.Combine(directory, "seal-policy.bin"), ReservoirSealPolicyCodecV1.Encode(f.Policy));
            File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(f.World));
        }
    }
}

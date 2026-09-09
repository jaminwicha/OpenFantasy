using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Terrain;

public sealed class TerrainEditTests
{
    [Fact]
    public void ExcavationPreservesOriginalLineageAndPriorSnapshotWithoutRegrowingOnLoad()
    {
        var original = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        CellCoordinate target = original.Cells[0].Coordinate;
        TerrainCell before = original.GetCell(target);
        var changed = original.Excavate(0, [target]);
        TerrainCell after = changed.GetCell(target);
        Assert.Equal(0UL, original.Revision);
        Assert.Equal(1UL, changed.Revision);
        Assert.True(before.IsSolid);
        Assert.False(before.IsExcavated);
        Assert.False(after.IsSolid);
        Assert.True(after.IsExcavated);
        Assert.Equal(before.SourceId, after.SourceId);
        Assert.Equal(before.FormationId, after.FormationId);
        Assert.Equal(before.Material, after.Material);
        Assert.Equal(before.Stratum, after.Stratum);
        Assert.NotEqual(original.ComputeContentHash(), changed.ComputeContentHash());
        var loaded = TerrainRegionCodecV1.Decode(TerrainRegionCodecV1.Encode(changed));
        TerrainGenerationTests.AssertSameCell(after, loaded.GetCell(target));
        Assert.Equal(changed.ComputeContentHash(), loaded.ComputeContentHash());
        Assert.Throws<ArgumentException>(() => loaded.Excavate(1, [target]));
    }

    [Fact]
    public void BatchesAndIndependentEditOrdersProduceSameCanonicalFinalOccupancy()
    {
        var original = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        CellCoordinate first = original.Cells[0].Coordinate;
        CellCoordinate second = original.Cells[1].Coordinate;
        var together = original.Excavate(0, [first, second]);
        var reverseTogether = original.Excavate(0, [second, first]);
        Assert.Equal(together.ComputeContentHash(), reverseTogether.ComputeContentHash());
        var sequential = original.Excavate(0, [first]).Excavate(1, [second]);
        var reverseSequential = original.Excavate(0, [second]).Excavate(1, [first]);
        Assert.Equal(sequential.ComputeContentHash(), reverseSequential.ComputeContentHash());
        Assert.NotEqual(together.ComputeContentHash(), sequential.ComputeContentHash());
        for (int index = 0; index < original.Cells.Count; index++)
        {
            TerrainGenerationTests.AssertSameCell(together.Cells[index], sequential.Cells[index]);
        }
    }

    [Fact]
    public void StaleReplayDuplicateEmptyVoidAndOutsideEditsRejectBeforeAnyPublication()
    {
        var original = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        var originalHash = original.ComputeContentHash();
        CellCoordinate solid = original.Cells[0].Coordinate;
        CellCoordinate empty = original.Cells.First(cell => !cell.IsSolid).Coordinate;
        Assert.Throws<InvalidOperationException>(() => original.Excavate(1, [solid]));
        Assert.Throws<ArgumentException>(() => original.Excavate(0, []));
        Assert.Throws<ArgumentException>(() => original.Excavate(0, [solid, solid]));
        Assert.Throws<ArgumentException>(() => original.Excavate(0, [solid, empty]));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.Excavate(0, [solid, new CellCoordinate(999, 0, 0)]));
        Assert.Throws<ArgumentNullException>(() => original.Excavate(0, null!));
        var changed = original.Excavate(0, [solid]);
        Assert.Throws<InvalidOperationException>(() => changed.Excavate(0, [solid]));
        Assert.Throws<ArgumentException>(() => changed.Excavate(1, [solid]));
        Assert.Equal(originalHash, original.ComputeContentHash());
        Assert.True(original.GetCell(solid).IsSolid);
        Assert.Equal(1UL, changed.Revision);
    }

    [Fact]
    public void IndependentStackedTunnelsAndSurfaceUseActualClearanceAndDirectSupport()
    {
        var template = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        var region = new StratifiedTerrainGenerator().Generate(template.Scope, template.Seed, template.Rules,
            new TerrainRegionBounds(new CellCoordinate(-1, -1, -8), 3, 3, 12));
        Assert.Equal(StandingSpace.Supported, region.QueryStanding(new CellCoordinate(0, 0, 1)));
        Assert.Equal(StandingSpace.Blocked, region.QueryStanding(new CellCoordinate(0, 0, -4)));
        var tunnels = region.Excavate(0, [new CellCoordinate(0, 0, -4), new CellCoordinate(0, 0, -6)]);
        Assert.Equal(StandingSpace.Supported, tunnels.QueryStanding(new CellCoordinate(0, 0, -4)));
        Assert.Equal(StandingSpace.Supported, tunnels.QueryStanding(new CellCoordinate(0, 0, -6)));
        Assert.Equal(StandingSpace.Blocked, tunnels.QueryStanding(new CellCoordinate(0, 0, -5)));
        Assert.Equal(StandingSpace.Supported, tunnels.QueryStanding(new CellCoordinate(0, 0, 1)));
        var shaft = tunnels.Excavate(1, [new CellCoordinate(0, 0, -5), new CellCoordinate(0, 0, 0)]);
        Assert.Equal(StandingSpace.Unsupported, shaft.QueryStanding(new CellCoordinate(0, 0, -4)));
        Assert.Equal(StandingSpace.Unsupported, shaft.QueryStanding(new CellCoordinate(0, 0, 1)));
        Assert.Equal(StandingSpace.Supported, shaft.QueryStanding(new CellCoordinate(0, 0, -6)));
    }

    [Fact]
    public void MissingBoundarySupportAndMinimumSignedBelowCoordinateAreUnavailable()
    {
        var original = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        var emptiedBottom = original.Excavate(0, [original.Bounds.Minimum]);
        Assert.Equal(StandingSpace.Unavailable, emptiedBottom.QueryStanding(original.Bounds.Minimum));
        Assert.Equal(StandingSpace.Unavailable, original.QueryStanding(new CellCoordinate(long.MaxValue, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.GetCell(new CellCoordinate(long.MaxValue, 0, 0)));
        var extreme = TerrainFixture.Generate(TerrainFixture.Example("signed-coordinate-extremes"));
        var excavated = extreme.Excavate(0, [extreme.Bounds.Minimum]);
        Assert.Equal(StandingSpace.Unavailable, excavated.QueryStanding(extreme.Bounds.Minimum));
        var boundaryVoid = new StratifiedTerrainGenerator().Generate(original.Scope, original.Seed, original.Rules,
            new TerrainRegionBounds(new CellCoordinate(0, 0, 1), 1, 1, 1));
        Assert.Equal(StandingSpace.Unavailable, boundaryVoid.QueryStanding(new CellCoordinate(0, 0, 1)));
    }

    [Fact]
    public void CallerMutationAndWritableCollectionAccessCannotChangeTerrainSnapshots()
    {
        var original = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        CellCoordinate first = original.Cells[0].Coordinate;
        CellCoordinate second = original.Cells[1].Coordinate;
        CellCoordinate[] requested = [first];
        var changed = original.Excavate(0, requested);
        var hash = changed.ComputeContentHash();
        requested[0] = second;
        Assert.True(changed.GetCell(first).IsExcavated);
        Assert.False(changed.GetCell(second).IsExcavated);
        var list = (IList<TerrainCell>)changed.Cells;
        Assert.Throws<NotSupportedException>(() => list[0] = original.Cells[0]);
        Assert.Equal(hash, changed.ComputeContentHash());
        byte[] bytes = TerrainRegionCodecV1.Encode(changed);
        Array.Fill(bytes, (byte)0);
        Assert.Equal(hash, changed.ComputeContentHash());
    }

    [Fact]
    public void BatchLimitRejectsFirstExcessButDoesNotCapTotalRegionDepletion()
    {
        var template = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        var region = new StratifiedTerrainGenerator().Generate(template.Scope, template.Seed, template.Rules,
            new TerrainRegionBounds(new CellCoordinate(0, 0, -20), 32, 32, 16));
        Assert.Equal(TerrainRegionBounds.MaximumCellCount, region.Cells.Count);
        Assert.Throws<ArgumentException>(() => region.Excavate(0,
            region.Cells.Take(TerrainRegionSnapshot.MaximumExcavationBatch + 1).Select(cell => cell.Coordinate)));
        var depleted = region;
        for (int offset = 0; offset < region.Cells.Count; offset += TerrainRegionSnapshot.MaximumExcavationBatch)
        {
            depleted = depleted.Excavate(depleted.Revision,
                region.Cells.Skip(offset).Take(TerrainRegionSnapshot.MaximumExcavationBatch).Select(cell => cell.Coordinate));
        }
        Assert.Equal(64UL, depleted.Revision);
        Assert.All(depleted.Cells, cell => Assert.True(cell.IsExcavated));
        Assert.All(region.Cells, cell => Assert.True(cell.IsSolid));
        byte[] encoded = TerrainRegionCodecV1.Encode(depleted);
        Assert.True(encoded.Length <= TerrainRegionCodecV1.MaximumEncodedByteLength);
        var loaded = TerrainRegionCodecV1.Decode(encoded);
        Assert.Equal(depleted.ComputeContentHash(), loaded.ComputeContentHash());
        Assert.Equal(StandingSpace.Unsupported, loaded.QueryStanding(new CellCoordinate(0, 0, -19)));
    }

    [Fact]
    public void MaximumPersistedRevisionCannotWrapDuringFurtherExcavation()
    {
        var example = TerrainFixture.Example("flat");
        var maximum = example.GetProperty("states").EnumerateArray()
            .Single(state => state.GetProperty("name").GetString() == "maximum-revision");
        var snapshot = TerrainRegionCodecV1.Decode(Convert.FromHexString(maximum.GetProperty("canonicalHex").GetString()!));
        Assert.Equal(ulong.MaxValue, snapshot.Revision);
        CellCoordinate remaining = snapshot.Cells.First(cell => cell.IsSolid).Coordinate;
        Assert.Throws<OverflowException>(() => snapshot.Excavate(ulong.MaxValue, [remaining]));
        Assert.True(snapshot.GetCell(remaining).IsSolid);
    }
}

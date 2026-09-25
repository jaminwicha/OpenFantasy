using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class LiquidCellFlowArtifactTests
{
    [Fact]
    public void Source_backed_front_and_pool_are_retained_for_independent_host_and_atomicity_checks()
    {
        var fixture = new LiquidCellWorldFixture();
        var states = new List<WorldSnapshot> { fixture.Genesis };
        var batches = new List<WorldStepBatch>();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            for (int step = 0; step < 12; step++)
            {
                WorldSnapshot before = states[^1];
                WorldStepBatch batch = fixture.Materials.Actions.Batch(before, [], []);
                WorldStepDecision decision = authority.Commit(WorldStepCodec.Decode(WorldStepCodec.Encode(batch)));
                WorldSnapshot after = decision.Candidate;
                Assert.Empty(decision.Receipts);
                Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
                Assert.Equal(1350UL, after.LiquidCells!.Portions.Aggregate(0UL, (sum, part) => sum + part.Quantity));
                Assert.Equal(after.AdvancingSteps, after.LiquidCells.Revision);
                Assert.Equal(after.Calendar, after.LiquidCells.Boundary);
                Assert.NotEqual(before.LiquidCells!.ContentHash, after.LiquidCells.ContentHash);
                states.Add(after);
                batches.Add(batch);
            }
        }
        Assert.Equal(200UL, states[1].LiquidCells!.Quantities[LiquidCellWorldFixture.SourceCell(2, -1)]);
        Assert.False(states[1].LiquidCells!.Quantities.ContainsKey(LiquidCellWorldFixture.SourceCell(2, -2)));
        Assert.Equal(200UL, states[2].LiquidCells!.Quantities[LiquidCellWorldFixture.SourceCell(2, -2)]);
        Assert.True(states[^1].LiquidCells!.Quantities[LiquidCellWorldFixture.SourceCell(3, -2)] > 0);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(states[^1].ContentHash, reopened.InspectForHost().State.ContentHash);
            foreach (WorldSnapshot state in states.AsEnumerable().Reverse())
            {
                Assert.Equal(WorldSnapshotCodec.Encode(state),
                    WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(state.Revision).State));
            }
        }
        Retain(fixture, files.Path, states, batches);
    }

    private static void Retain(LiquidCellWorldFixture fixture, string database,
        IReadOnlyList<WorldSnapshot> states, IReadOnlyList<WorldStepBatch> batches)
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_LIQUID_CELL_FIXTURE_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "basis.bin"), WorldSnapshotCodec.Encode(fixture.ContainedBasis));
        File.WriteAllBytes(Path.Combine(directory, "liquid-cells.bin"), LiquidCellStateCodecV1.Encode(states[0].LiquidCells!));
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(states[0]));
        File.WriteAllBytes(Path.Combine(directory, "after.bin"), WorldSnapshotCodec.Encode(states[1]));
        File.WriteAllBytes(Path.Combine(directory, "step.bin"), WorldStepCodec.Encode(batches[0]));
        for (int index = 0; index < states.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"state-{index}.bin"), WorldSnapshotCodec.Encode(states[index]));
        }
        for (int index = 0; index < batches.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"step-{index + 1}.bin"), WorldStepCodec.Encode(batches[index]));
        }
        File.Copy(database, Path.Combine(directory, "world.sqlite"), overwrite: true);
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Source-backed finite terrain channel and receiving pool over twelve committed advancing steps; authored initial holes",
            ProfileVersion = 20, SchemaVersion = 3, SourceId = ReservoirWorldFixture.Source.ToString(),
            SourceReservoir = LiquidCellWorldFixture.SourceReservoir.ToString(),
            ReceiverReservoir = LiquidCellWorldFixture.ReceiverReservoir.ToString(),
            TotalUnits = 1350UL, MicrolitresPerUnit = 1_000_000UL,
            History = states.Select(state => new
            {
                state.Revision, Hash = state.ContentHash.ToString(), CellsHash = state.LiquidCells!.ContentHash.ToString(),
                Cells = state.LiquidCells.DomainCells.Select(cell => new
                {
                    ReservoirId = cell.ReservoirId.ToString(), cell.Coordinate.X, cell.Coordinate.Y, cell.Coordinate.Z,
                    Units = state.LiquidCells.Quantities.GetValueOrDefault(cell)
                }),
                Portions = state.LiquidCells.Portions.Select(part => new
                {
                    ReservoirId = part.Cell.ReservoirId.ToString(), part.Cell.Coordinate.X, part.Cell.Coordinate.Y, part.Cell.Coordinate.Z,
                    Origin = part.Origin.ToString(), OriginId = part.OriginId.ToString(), part.Start, part.End
                })
            })
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}

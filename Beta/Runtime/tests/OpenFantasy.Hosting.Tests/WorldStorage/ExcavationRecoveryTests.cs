using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ExcavationRecoveryTests
{
    [Fact]
    public void Earned_work_completion_receipts_and_out_of_order_history_recover_exactly_without_writes()
    {
        var fixture = new ExcavationWorldFixture();
        WorldSnapshot world = fixture.Genesis;
        var states = new List<StateEvidence> { Inspect(world, fixture) };
        var batches = new List<WorldStepBatch>();
        var receipts = new List<WorldActionReceipt>();
        Assert.Equal(0U, world.Gathering!.GetActorWork(fixture.Player).Effort);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, world);
        try
        {
            for (ulong work = 1; work <= 3; work++)
            {
                uint beforeEarning = world.Gathering!.GetActorWork(fixture.Player).Effort;
                for (int step = 0; step < 40; step++)
                {
                    var batch = fixture.Actions.Batch(world, [], []);
                    var decision = authority.Commit(batch);
                    Assert.Empty(decision.Receipts);
                    world = decision.Candidate;
                    batches.Add(batch);
                    states.Add(Inspect(world, fixture));
                }
                Assert.Equal(beforeEarning + GatherRulesV1.EffortPerSwing, world.Gathering!.GetActorWork(fixture.Player).Effort);
                var action = fixture.Command(world);
                var workBatch = fixture.Actions.Batch(world, [action], []);
                byte[] encoded = WorldStepCodec.Encode(workBatch);
                Assert.Equal(encoded, WorldStepCodec.Encode(WorldStepCodec.Decode(encoded)));
                var worked = authority.Commit(WorldStepCodec.Decode(encoded));
                var receipt = Assert.Single(worked.Receipts);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                receipts.Add(receipt);
                world = worked.Candidate;
                batches.Add(workBatch);
                StateEvidence state = Inspect(world, fixture);
                states.Add(state);
                Assert.Equal(work, state.CompletedWork);
                Assert.Equal((uint)work * GatherRulesV1.EffortPerAdvancingStep, state.Effort);
                Assert.Equal(work == 3, state.IsExcavated);
                Assert.Equal(work == 3 ? fixture.Site.Quantity : 0UL, state.YieldUnits);
                Assert.Equal(states[0].DestinationRevision + (work == 3 ? 1UL : 0UL), state.DestinationRevision);
                if (work < 3)
                {
                    Assert.Equal(states[0].TerrainArchiveHash, state.TerrainArchiveHash);
                }
                if (work == 1)
                {
                    authority.Dispose();
                    authority = SqliteWorldAuthority.OpenExisting(files.Path);
                    world = authority.InspectForHost().State;
                    Assert.Equal(state, Inspect(world, fixture));
                }
            }
        }
        finally
        {
            authority.Dispose();
        }
        Assert.Equal(123UL, world.Revision);
        Assert.Equal(123UL, world.AdvancingSteps);
        Assert.Equal(states[0].SourceItemQuantity + 1, states[^1].SourceItemQuantity);
        int[] selected = [123, 0, 82, 40, 122, 41, 81, 123];
        byte[] database = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(states[^1], Inspect(reopened.InspectForHost().State, fixture));
            foreach (int revision in selected)
            {
                RecordedWorldState recorded = reopened.ReadRecordedStateForHost((ulong)revision);
                Assert.Equal(123UL, recorded.SourceHeadRevision);
                Assert.Equal(states[^1].WorldHash, recorded.SourceHeadHash.Value);
                Assert.Equal(states[revision], Inspect(recorded.State, fixture));
                Assert.Equal(states[revision].WorldHash, ContentHash.Compute(WorldSnapshotCodec.Encode(recorded.State)).Value);
            }
            for (int index = 0; index < receipts.Count; index++)
            {
                WorldStepBatch batch = batches[(index + 1) * 41 - 1];
                WorldActionCommand command = Assert.Single(batch.Actions);
                var delivery = ConstructionUpgradeFixture.Delivery(world, fixture.Player);
                var recovered = Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(command, delivery));
                Assert.Equal(WorldReceiptCodecV1.Encode(receipts[index]), WorldReceiptCodecV1.Encode(recovered));
                Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batch));
                var intent = Assert.IsType<ExcavateIntent>(command.Intent);
                var changed = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
                    command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision,
                    intent with { ExpectedDestinationRevision = intent.ExpectedDestinationRevision + 1 });
                Assert.Throws<AuthorityDispatchException>(() => reopened.FindReceipt(changed, delivery));
            }
            Assert.Equal(states[^1], Inspect(reopened.InspectForHost().State, fixture));
        }
        Assert.Equal(database, File.ReadAllBytes(files.Path));
        Retain(fixture, states, batches, receipts, selected);
    }

    private static StateEvidence Inspect(WorldSnapshot world, ExcavationWorldFixture fixture)
    {
        var region = world.Space.Regions.Single(value => value.Bounds.Contains(ExcavationWorldFixture.Target));
        EntityId custodian = world.GetResident(fixture.Player).InventoryCustodianId;
        return new(world.Revision, world.ContentHash.Value, world.Excavation!.ContentHash.Value,
            ContentHash.Compute(TerrainRegionCodecV1.Encode(region)).Value, world.Excavation.Sites.Single().CompletedWork,
            world.Gathering!.GetActorWork(fixture.Player).Effort, region.GetCell(ExcavationWorldFixture.Target).IsExcavated,
            world.Inventory.Custody.Lots.Where(lot => lot.Source.SourceId == fixture.Site.CountedSourceId)
                .Aggregate(0UL, (sum, lot) => sum + lot.Quantity),
            world.Inventory.Custody.GetStorage(custodian).Revision, world.Inventory.Custody.GetStoredQuantity(custodian));
    }

    private static void Retain(ExcavationWorldFixture fixture, IReadOnlyList<StateEvidence> states,
        IReadOnlyList<WorldStepBatch> batches, IReadOnlyList<WorldActionReceipt> receipts, int[] selected)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_EXCAVATION_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        using (var seed = new StorageFiles())
        {
            using (SqliteWorldAuthority.CreateNew(seed.Path, fixture.Genesis)) { }
            File.Copy(seed.Path, Path.Combine(path, "genesis.sqlite"), overwrite: true);
        }
        for (int index = 0; index < batches.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(path, $"step-{index + 1}.bin"), WorldStepCodec.Encode(batches[index]));
        }
        File.WriteAllText(Path.Combine(path, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Three ordinary excavation actions with 40 earned idle advances before each; exact atomic cell/material extraction and history",
            ProfileVersion = 10,
            Player = fixture.Player.ToString(), SiteId = fixture.Site.SiteId.ToString(),
            DestinationId = fixture.Genesis.GetResident(fixture.Player).InventoryCustodianId.ToString(),
            CountedSourceId = fixture.Site.CountedSourceId.ToString(), Target = ExcavationWorldFixture.Target,
            FinalRevision = 123, CompletionRevision = 123, HistoryRevisions = selected,
            WorldHashes = states.Select(state => state.WorldHash), States = states,
            Receipts = receipts.Select(receipt => new
            {
                Revision = receipt.CommitSequence, CommandId = receipt.CommandId.ToString(),
                ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)).ToLowerInvariant()
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record StateEvidence(ulong Revision, string WorldHash, string ExcavationHash, string TerrainArchiveHash,
        ulong CompletedWork, uint Effort, bool IsExcavated, ulong YieldUnits, ulong DestinationRevision, ulong SourceItemQuantity);
}

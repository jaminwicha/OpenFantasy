using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WatershedNetworkWorldRecoveryTests
{
    [Fact]
    public void NativeContentMovementDialogueAndHistoryKeepAdmittedGeographyAcrossRestart()
    {
        using var files = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot basis = ConstructionFarmAdmission.CreateGenesis(fixture.QuestGenesis());
        WorldSnapshot state = WorldWatershedNetworkAdmission.CreateGenesis(basis, Plan(basis));
        byte[] watershed = WatershedNetworkCodecV1.Encode(state.WatershedNetwork!);
        var snapshots = new List<byte[]> { WorldSnapshotCodec.Encode(state) };
        var batches = new List<WorldStepBatch>();
        EntityId player = state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId wren = state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host).ActorId;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            state = authority.InspectForHost().State;
            Assert.Equal(snapshots[0], WorldSnapshotCodec.Encode(state));
            void Commit(WorldStepBatch batch)
            {
                WorldSnapshot prior = state;
                state = authority.Commit(batch).Candidate;
                Assert.Equal(7, state.ProfileVersion);
                Assert.Same(prior.WatershedNetwork, state.WatershedNetwork);
                batches.Add(batch);
                snapshots.Add(WorldSnapshotCodec.Encode(state));
            }
            // Observe the actual authored positions and physical range; no teleport or manufactured dialogue.
            for (int i = 0; state.Space.QueryInteraction(player, wren) != SpatialInteraction.Reachable; i++)
            {
                Assert.True(i < 128);
                var feet = state.Space.GetActor(player).Feet;
                var target = state.Space.GetActor(wren).Feet;
                long x = Math.Sign(target.X - feet.X) * WorldMovementInput.DisplacementPerStep;
                long y = x == 0 ? Math.Sign(target.Y - feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
                Commit(fixture.Batch(state, [], [new(ConstructionUpgradeFixture.Delivery(state, player), player, x, y)]));
            }
            var delivery = ConstructionUpgradeFixture.Delivery(state, player);
            WorldActionCommand Action(WorldActionIntent intent) => new(fixture.NextIdentity(), delivery.PrincipalId,
                delivery.SessionId, player, state.WorldId, state.BranchId, state.Space.GetActor(player).Revision, intent);
            Commit(fixture.Batch(state, [Action(new SpeakToActorIntent(wren, state.Space.GetActor(wren).Revision))], []));
            Assert.True(state.HasReadingHold);
            var heldCalendar = state.Calendar;
            Commit(fixture.Batch(state, [Action(new CloseDialogueIntent(state.Dialogues.Single().DialogueId))], []));
            Assert.False(state.HasReadingHold);
            Assert.Equal(heldCalendar, state.Calendar);
            Assert.NotEqual(basis.Space.GetActor(player).Feet, state.Space.GetActor(player).Feet);
        }
        byte[] before = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (int i in new[] { snapshots.Count - 1, 0, snapshots.Count - 2, 1 })
            {
                RecordedWorldState old = reopened.ReadRecordedStateForHost((ulong)i);
                Assert.Equal(snapshots[i], WorldSnapshotCodec.Encode(old.State));
                Assert.Equal(watershed, WatershedNetworkCodecV1.Encode(old.State.WatershedNetwork!));
            }
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batches[^1]));
            WorldSnapshot final = reopened.InspectForHost().State;
            Assert.Equal(state.ContentHash, final.ContentHash);
            AssertIdentityCollisions(reopened, fixture, final);
        }
        Assert.Equal(before, File.ReadAllBytes(files.Path));
        RetainOperatorFixture(basis, watershed, snapshots, batches);
    }

    [Fact]
    public void GenesisAdmissionRejectsProgressedOrAlreadyAdmittedWorlds()
    {
        WorldSnapshot basis = ConstructionFarmAdmission.CreateGenesis(new ConstructionUpgradeFixture().QuestGenesis());
        byte[] original = WorldSnapshotCodec.Encode(basis);
        WorldSnapshot admitted = WorldWatershedNetworkAdmission.CreateGenesis(basis, Plan(basis));
        Assert.NotEqual(basis.AdmissionVersion, admitted.AdmissionVersion);
        Assert.Throws<ArgumentException>(() => WorldWatershedNetworkAdmission.CreateGenesis(admitted, Plan(basis)));
        WorldSnapshot progressed = new WorldStepProcessor().Decide(basis,
            new ConstructionUpgradeFixture().Batch(basis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldWatershedNetworkAdmission.CreateGenesis(progressed, Plan(basis)));
        Assert.Equal(original, WorldSnapshotCodec.Encode(basis));
    }

    [Theory]
    [InlineData("world_genesis")]
    [InlineData("world_checkpoints")]
    [InlineData("world_head")]
    public void IndependentlyChangedValidGeographyIsRejectedByReplay(string table)
    {
        using var files = new StorageFiles();
        WorldSnapshot basis = ConstructionFarmAdmission.CreateGenesis(new ConstructionUpgradeFixture().QuestGenesis());
        WorldSnapshot genesis = WorldWatershedNetworkAdmission.CreateGenesis(basis, Plan(basis));
        WorldSnapshot changed = WorldWatershedNetworkAdmission.CreateGenesis(basis, Plan(basis, 99));
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis)) { }
        byte[] replacement = WorldSnapshotCodec.Encode(changed);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = files.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE " + table + " SET state=$state,hash=$hash";
            command.Parameters.AddWithValue("$state", replacement);
            command.Parameters.AddWithValue("$hash", ContentHash.Compute(replacement).ToByteArray());
            Assert.Equal(1, command.ExecuteNonQuery());
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Fact]
    public void MaximumPreparedNetworkFitsTheExistingWorldStorageAndRecoveryEnvelope()
    {
        using var files = new StorageFiles();
        WorldSnapshot basis = ConstructionFarmAdmission.CreateGenesis(new ConstructionUpgradeFixture().QuestGenesis());
        WorldSnapshot genesis = WorldWatershedNetworkAdmission.CreateGenesis(basis, Plan(basis, maximum: true));
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Equal(65_536, authority.InspectForHost().State.WatershedNetwork!.ColumnCount);
            WorldSnapshot step = authority.Commit(new ConstructionUpgradeFixture().Batch(genesis, [], [])).Candidate;
            Assert.Equal(7, step.ProfileVersion);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(genesis.ContentHash, reopened.ReadRecordedStateForHost(0).StateHash);
        Assert.Equal(1UL, reopened.InspectForHost().State.Revision);
        Assert.Equal(genesis.WatershedNetwork!.ComputeContentHash(), reopened.InspectForHost().State.WatershedNetwork!.ComputeContentHash());
    }

    private static PreparedWatershedNetwork Plan(WorldSnapshot basis, ulong seed = 1, bool maximum = false)
    {
        int count = maximum ? 16 : 2;
        int width = maximum ? 64 : 4;
        int depth = maximum ? 64 : 5;
        var basins = new List<PreparedWatershed>();
        for (int i = 0; i < count; i++)
        {
            basins.Add(new WatershedPlanner().Prepare(new WatershedRequest(basis.Space.Scope,
                new EntityId(0x6e6574776f726b, seed), new WatershedBounds(-2 + i * width, -3, width, depth),
                new WatershedRules((count - i) * 20, 2), i == count - 1 ? 0 : width - 1,
                new EntityId(0x6e6574776f726b, (ulong)(100 + i))), TestContext.Current.CancellationToken));
        }
        var closures = basins.Select((b, i) => i + 1 < count
            ? WatershedOutletClosure.ToBasin(b.BasinId, basins[i + 1].BasinId, 0)
            : WatershedOutletClosure.ToTerminal(b.BasinId, count * width * depth));
        return new WatershedNetworkPlanner().Prepare(new EntityId(0x6e6574776f726b, 200), basins, closures,
            TestContext.Current.CancellationToken);
    }

    private static void AssertIdentityCollisions(SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture, WorldSnapshot state)
    {
        PreparedWatershedNetwork network = state.WatershedNetwork!;
        PreparedWatershed plan = network.Basins[0];
        var ids = new[] { network.ProvinceId, network.SharedFaces[0].BoundaryId, plan.BasinId, plan.FormationId,
            plan.Request.ExternalBoundaryId, plan.Columns[0].SubstrateParcelId, plan.Boundaries[0].BoundaryId };
        foreach (EntityId id in ids)
        {
            Assert.True(authority.IsIdentityOccupiedForHost(id));
            var batch = new WorldStepBatch(id, state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], 7);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
        }
        EntityId actor = state.Residents[0].ActorId;
        var delivery = ConstructionUpgradeFixture.Delivery(state, actor);
        var command = new WorldActionCommand(plan.BasinId, delivery.PrincipalId, delivery.SessionId, actor,
            state.WorldId, state.BranchId, state.Space.GetActor(actor).Revision,
            new CloseDialogueIntent(fixture.NextIdentity()));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(state, [command], [])));
    }

    private static void RetainOperatorFixture(WorldSnapshot basis, byte[] watershed,
        IReadOnlyList<byte[]> snapshots, IReadOnlyList<WorldStepBatch> batches)
    {
        string? root = Environment.GetEnvironmentVariable("OPENFANTASY_WATERSHED_NETWORK_WORLD_EVIDENCE");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "basis.bin"), WorldSnapshotCodec.Encode(basis));
        File.WriteAllBytes(Path.Combine(root, "network.bin"), watershed);
        File.WriteAllBytes(Path.Combine(root, "genesis.bin"), snapshots[0]);
        for (int i = 0; i < batches.Count; i++)
        {
            File.WriteAllBytes(Path.Combine(root, $"step-{i + 1}.bin"), WorldStepCodec.Encode(batches[i]));
        }
        File.WriteAllText(Path.Combine(root, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Explicit original farm content with retained macro preparation; no local terrain realization",
            WorldHashes = snapshots.Select(value => ContentHash.Compute(value).Value),
            NetworkHash = ContentHash.Compute(watershed).Value,
            HeldRevision = snapshots.Count - 2, FinalRevision = snapshots.Count - 1
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

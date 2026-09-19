using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.RunoffCommandPolicyTests;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class RunoffCommandRecoveryTests
{
    [Fact]
    public void CapacityOnlyAdmissionsCancellationsAndRejectionsReplayWithExactReceiptsAndSourceHolds()
    {
        var fixture = new ReservoirWorldFixture(spareBudget: true);
        WorldSnapshot state = fixture.Genesis;
        var states = new List<WorldSnapshot> { state };
        var batches = new List<WorldStepBatch>();
        var commands = new List<WorldActionCommand>();
        var receipts = new List<WorldActionReceipt>();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            void Commit(RunoffCommitmentIntent intent, WorldActionRejection expected)
            {
                var action = fixture.Command(state, state.Reservoirs!.Capacity.Domain.OwnerActorId, intent);
                var batch = fixture.Actions.Batch(state, [action], []);
                var encoded = WorldStepCodec.Encode(batch);
                Assert.Equal(encoded, WorldStepCodec.Encode(WorldStepCodec.Decode(encoded)));
                var result = authority.Commit(WorldStepCodec.Decode(encoded));
                var receipt = Assert.Single(result.Receipts);
                Assert.Equal(expected, receipt.Rejection);
                state = result.Candidate;
                Assert.Equal(states[0].Inventory.ContentHash, state.Inventory.ContentHash);
                Assert.Equal(states[0].Production!.ContentHash, state.Production!.ContentHash);
                states.Add(state); batches.Add(batch); commands.Add(action); receipts.Add(receipt);
            }
            Commit(Admit(state), WorldActionRejection.None);
            Commit(Cancel(state), WorldActionRejection.None);
            Commit(new CancelRunoffIntent(states[0].Reservoirs!.ContentHash, 0, ReservoirWorldFixture.SecondReservation),
                WorldActionRejection.RevisionConflict);
            Commit(Cancel(state, ReservoirWorldFixture.SecondReservation), WorldActionRejection.None);
            Commit(Admit(state), WorldActionRejection.Unavailable);
            Commit(Cancel(state), WorldActionRejection.Unavailable);
            Assert.Empty(state.Reservoirs!.Capacity.Pending);
            Assert.Equal(2, state.Reservoirs.Capacity.Closed.Count);
            Assert.Equal(3UL, state.Reservoirs.Capacity.Revision);
            Assert.Equal(1200UL, state.Inventory.Custody.Lots.Where(l => l.ReservedBy == ReservoirWorldFixture.Reservation
                || l.ReservedBy == ReservoirWorldFixture.SecondReservation).Aggregate(0UL, (sum, l) => sum + l.Quantity));
        }
        byte[] database = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (int index in new[] { 6, 0, 3, 1, 2, 4, 5 })
            {
                Assert.Equal(WorldSnapshotCodec.Encode(states[index]), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost((ulong)index).State));
            }
            for (int i = 0; i < commands.Count; i++)
            {
                var receipt = reopened.FindReceipt(commands[i], ConstructionUpgradeFixture.Delivery(state, commands[i].ActorId));
                Assert.NotNull(receipt);
                Assert.Equal(WorldReceiptCodecV1.Encode(receipts[i]), WorldReceiptCodecV1.Encode(receipt));
                Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batches[i]));
            }
        }
        Assert.Equal(database, File.ReadAllBytes(files.Path));
        Retain(fixture, states, batches);
    }

    private static void Retain(ReservoirWorldFixture fixture, IReadOnlyList<WorldSnapshot> states, IReadOnlyList<WorldStepBatch> batches)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RUNOFF_COMMAND_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "basis.bin"), WorldSnapshotCodec.Encode(fixture.Basis));
        File.WriteAllBytes(Path.Combine(path, "reservoirs.bin"), LiquidReservoirCodecV1.Encode(fixture.Reservoirs));
        File.WriteAllBytes(Path.Combine(path, "genesis.bin"), WorldSnapshotCodec.Encode(states[0]));
        for (int i = 0; i < batches.Count; i++) File.WriteAllBytes(Path.Combine(path, $"step-{i + 1}.bin"), WorldStepCodec.Encode(batches[i]));
        File.WriteAllText(Path.Combine(path, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Funded admission/cancellation controls; exact capacity-only state and rejected commands; no liquid movement",
            WorldHashes = states.Select(s => s.ContentHash.Value), ReservoirHashes = states.Select(s => s.Reservoirs!.ContentHash.Value),
            NetworkHash = fixture.Reservoirs.Capacity.Domain.Network.ComputeContentHash().Value,
            HeldRevision = (int?)null, FinalRevision = states.Count - 1, HistoryRevisions = new[] { 6, 0, 3, 1, 2, 4, 5 },
            ReservoirStates = states.Select(s => new
            {
                Revision = s.Reservoirs!.Capacity.Revision, PendingCount = s.Reservoirs.Capacity.Pending.Count,
                ClosedCount = s.Reservoirs.Capacity.Closed.Count,
                ActualMicrolitres = s.Reservoirs.Projections.Aggregate(0UL, (sum, p) => sum + p.ActualFill.Volume.Value),
                ReservedIncomingUnits = s.Reservoirs.Capacity.Receivers.Single().IncomingUnits
            }),
            Actions = batches.Select(b => new
            {
                Kind = b.Actions[0].Intent is AdmitRunoffIntent ? 1 : 2,
                ExpectedHash = ((RunoffCommitmentIntent)b.Actions[0].Intent).ExpectedReservoirHash.Value,
                ExpectedRevision = ((RunoffCommitmentIntent)b.Actions[0].Intent).ExpectedCapacityRevision,
                Reservation = ((RunoffCommitmentIntent)b.Actions[0].Intent).ReservationId.ToString()
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

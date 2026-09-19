using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ReservoirTransportWorldTests
{
    [Fact]
    public void ActualDueTransferSurvivesReadingPauseCommitRecoveryAndExactHistoricalQueries()
    {
        var f = new ReservoirTransportWorldFixture();
        var state = f.Genesis;
        var states = new List<WorldSnapshot> { state };
        var batches = new List<WorldStepBatch>();
        var commands = new List<WorldActionCommand>();
        var receipts = new List<WorldActionReceipt>();
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            void Commit(WorldActionIntent? intent = null, WorldActionRejection expected = WorldActionRejection.None)
            {
                var actions = intent is null ? [] : new[] { f.Materials.Command(state, f.Player, intent) };
                var batch = f.Materials.Actions.Batch(state, actions, []);
                byte[] wire = WorldStepCodec.Encode(batch);
                Assert.Equal(wire, WorldStepCodec.Encode(WorldStepCodec.Decode(wire)));
                var result = authority.Commit(WorldStepCodec.Decode(wire));
                if (actions.Length != 0)
                {
                    var receipt = Assert.Single(result.Receipts);
                    Assert.Equal(expected, receipt.Rejection);
                    commands.Add(actions[0]); receipts.Add(receipt);
                }
                state = result.Candidate;
                states.Add(state); batches.Add(batch);
                Assert.Equal(state.Calendar, state.Transport!.Boundary);
                Assert.Equal(state.Inventory.ContentHash, state.Reservoirs!.Capacity.Inventory.ContentHash);
            }
            Commit(f.Schedule(state));
            Assert.Equal(RunoffTransportStatus.Scheduled, state.Transport!.Stages[0].Status);
            Commit(new SpeakToActorIntent(f.Other, state.Space.GetActor(f.Other).Revision));
            Assert.True(state.HasReadingHold);
            Assert.Equal(states[0].Inventory.ContentHash, state.Inventory.ContentHash);
            Commit(f.Schedule(state), WorldActionRejection.Unavailable);
            Assert.Equal(states[2].Calendar, state.Calendar);
            Commit(new CloseDialogueIntent(state.Dialogues[0].DialogueId));
            Assert.Equal(states[2].Calendar, state.Calendar);
            Assert.False(state.HasReadingHold);
            Commit();
            Assert.Equal(RunoffTransportStatus.Completed, state.Transport!.Stages[0].Status);
            Assert.Equal(650UL, state.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.Receiver));
            Assert.Equal(700UL, state.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.SourceStore));
            Assert.Null(state.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(10)).ReservedBy);
            Commit(RunoffCommandPolicyTests.Cancel(state), WorldActionRejection.Unavailable);
            Commit(f.Schedule(state), WorldActionRejection.Unavailable);
            Commit();
            Assert.Equal(states[5].Inventory.ContentHash, state.Inventory.ContentHash);
        }
        byte[] database = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (int index in new[] { 8, 0, 5, 2, 4, 1, 7, 3, 6 })
            {
                Assert.Equal(WorldSnapshotCodec.Encode(states[index]),
                    WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost((ulong)index).State));
            }
            for (int i = 0; i < commands.Count; i++)
            {
                var receipt = reopened.FindReceipt(commands[i], ConstructionUpgradeFixture.Delivery(state, f.Player));
                Assert.NotNull(receipt);
                Assert.Equal(WorldReceiptCodecV1.Encode(receipts[i]), WorldReceiptCodecV1.Encode(receipt));
            }
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batches[4]));
        }
        Assert.Equal(database, File.ReadAllBytes(files.Path));
        Retain(f, states, batches);
    }

    [Fact]
    public void CancellationAtTheDueBoundaryWinsBeforeAutomaticDeliveryAndRetainsStock()
    {
        var f = new ReservoirTransportWorldFixture(nearby: false);
        var state = f.Step(f.Genesis, f.Schedule(f.Genesis)).Candidate;
        var decision = f.Step(state, RunoffCommandPolicyTests.Cancel(state));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(RunoffTransportStatus.Canceled, decision.Candidate.Transport!.Stages[0].Status);
        Assert.Equal(f.Genesis.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Equal(ReservoirWorldFixture.Reservation, decision.Candidate.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(10)).ReservedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentMaterialActionsOrOutletSillsCanBlockAStageWithoutLosingItsPromise(bool sill)
    {
        var f = new ReservoirTransportWorldFixture(incomingSupply: sill ? 0UL : 1000UL, sill: sill ? 1 : 0);
        var state = f.Step(f.Genesis, f.Schedule(f.Genesis)).Candidate;
        if (!sill)
        {
            var lot = state.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(13));
            var give = new GiveItemIntent(f.Player, state.Space.GetActor(f.Player).Revision, lot.LotId, lot.Revision,
                lot.CustodianId, state.Inventory.Custody.GetStorage(lot.CustodianId).Revision, 1000);
            var decision = f.Step(state, give, f.Other);
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            state = decision.Candidate;
        }
        else state = f.Step(state).Candidate;
        Assert.Equal(RunoffTransportStatus.BlockedByHead, state.Transport!.Stages[0].Status);
        Assert.Equal(1300UL, state.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.SourceStore));
        Assert.Single(state.Reservoirs!.Capacity.Pending);
        Assert.Equal(600UL, state.Reservoirs.Capacity.Receivers[0].IncomingUnits);
        Assert.Equal(ReservoirWorldFixture.Reservation, state.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(10)).ReservedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnauthorizedAndStaleScheduleRequestsCannotCreateStages(bool stale)
    {
        var f = new ReservoirTransportWorldFixture(nearby: false);
        var genesis = f.Genesis;
        var prior = f.Schedule(genesis);
        var state = stale ? f.Step(genesis).Candidate : genesis;
        var decision = f.Step(state, prior, stale ? f.Player : f.Other);
        Assert.Equal(stale ? WorldActionRejection.RevisionConflict : WorldActionRejection.NotAuthorized,
            Assert.Single(decision.Receipts).Rejection);
        Assert.Empty(decision.Candidate.Transport!.Stages);
        Assert.Equal(genesis.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
    }

    [Fact]
    public void FreshProfileAdmissionRejectsForeignHistoryDoubleOwnershipAndClockMismatch()
    {
        var f = new ReservoirTransportWorldFixture(nearby: false);
        var first = f.Transport.PlanSchedule(ReservoirTransportTests.Stage, ReservoirWorldFixture.Reservation,
            f.Basis.Calendar.AddMilliseconds(1800), f.Schedule(f.Genesis).Routes).ApplyTo(f.Transport);
        var completed = first.PlanAdvance(f.Basis.Calendar.AddMilliseconds(1800)).ApplyTo(first);
        Assert.Throws<ArgumentException>(() => WorldReservoirTransportAdmission.CreateGenesis(f.Basis, completed));
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(completed.Reservoirs.Capacity.Inventory, f.Basis.Space, f.Basis.Residents,
            f.Basis.AdmissionVersion, completed.Boundary, 0, 0, gathering: f.Basis.Gathering, production: f.Basis.Production,
            progression: f.Basis.Progression, quests: f.Basis.Quests, construction: f.Basis.Construction, transport: completed));
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(f.Basis.Inventory, f.Basis.Space, f.Basis.Residents,
            f.Basis.AdmissionVersion, f.Basis.Calendar, 0, 0, gathering: f.Basis.Gathering, production: f.Basis.Production,
            progression: f.Basis.Progression, quests: f.Basis.Quests, construction: f.Basis.Construction,
            reservoirs: f.Materials.Reservoirs, transport: f.Transport));
        var wrongTime = ReservoirTransportState.Create(f.Materials.Reservoirs, f.Transport.Connections, f.Basis.Calendar.AddMilliseconds(1));
        Assert.Throws<ArgumentException>(() => WorldReservoirTransportAdmission.CreateGenesis(f.Basis, wrongTime));
    }

    private static void Retain(ReservoirTransportWorldFixture fixture, IReadOnlyList<WorldSnapshot> states, IReadOnlyList<WorldStepBatch> batches)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RESERVOIR_TRANSPORT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "basis.bin"), WorldSnapshotCodec.Encode(fixture.Basis));
        File.WriteAllBytes(Path.Combine(path, "transport.bin"), ReservoirTransportCodecV1.Encode(fixture.Transport));
        File.WriteAllBytes(Path.Combine(path, "genesis.bin"), WorldSnapshotCodec.Encode(states[0]));
        for (int i = 0; i < batches.Count; i++) File.WriteAllBytes(Path.Combine(path, $"step-{i + 1}.bin"), WorldStepCodec.Encode(batches[i]));
        File.WriteAllText(Path.Combine(path, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Actual gated water transfer after a persisted reading pause; completion and rejected retries remain exact",
            WorldHashes = states.Select(s => s.ContentHash.Value), ReservoirHashes = states.Select(s => s.Reservoirs!.ContentHash.Value),
            TransportHashes = states.Select(s => s.Transport!.ContentHash.Value), NetworkHash = states[0].WatershedNetwork!.ComputeContentHash().Value,
            HeldRevision = 2, HeldRevisions = new[] { 2, 3 }, FinalRevision = states.Count - 1, CrashRevision = 5,
            HistoryRevisions = new[] { 8, 0, 5, 2, 4, 1, 7, 3, 6 },
            ReservoirStates = states.Select(s => new { Revision = s.Reservoirs!.Capacity.Revision,
                PendingCount = s.Reservoirs.Capacity.Pending.Count, ClosedCount = s.Reservoirs.Capacity.Closed.Count,
                ActualMicrolitres = s.Reservoirs.Projections.Aggregate(0UL, (sum, p) => sum + p.ActualFill.Volume.Value),
                ReservedIncomingUnits = s.Reservoirs.Capacity.Receivers.Single().IncomingUnits }),
            TransportStates = states.Select(s => new { Revision = s.Transport!.Revision, s.Transport.Boundary,
                StageCount = s.Transport.Stages.Count, PendingCount = s.Transport.Stages.Count(t => t.IsPending),
                CompletedCount = s.Transport.Stages.Count(t => t.Status == RunoffTransportStatus.Completed) }),
            SourceCustodian = ReservoirWorldFixture.SourceStore.ToString(), ReceiverCustodian = ReservoirWorldFixture.Receiver.ToString(),
            SourceInventory = states.Select(s => s.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.SourceStore)),
            ReceiverInventory = states.Select(s => s.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.Receiver))
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

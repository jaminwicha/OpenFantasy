using Microsoft.Data.Sqlite;
using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class EncounterPacingWorldTests
{
    [Fact]
    public void Complete_wet_world_retains_one_queue_receipt_and_advances_processes_only_after_36_tactical_ticks()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterPacingWorldFixture();
        WorldSnapshot genesis = fixture.Genesis;
        byte[] beforeCells = LiquidCellStateCodecV2.Encode(genesis.LiquidCells!);
        var selected = new List<WorldSnapshot> { genesis };
        var queued = new List<WorldActionCommand>();
        var evidence = new EncounterPacingEvidence(genesis);
        WorldSnapshot final;
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            for (int tick = 1; tick <= 38; tick++)
            {
                WorldSnapshot state = authority.ReadStateForHost();
                var timing = state.Pacing!.Encounter!;
                if (timing.DueActors.Count != 0)
                {
                    WorldActionCommand[] choices = timing.DueActors.Select(actor => fixture.Wait(state, actor.ActorId)).ToArray();
                    WorldStepBatch queueBatch = fixture.Batch(state, WorldBatchKind.EncounterDecision, choices);
                    WorldStepDecision decision = authority.Commit(queueBatch);
                    evidence.Record(decision.Candidate, queueBatch, decision.Receipts);
                    Assert.Equal(choices.Length, decision.Receipts.Count);
                    Assert.All(decision.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
                    Assert.Equal(state.Calendar, decision.Candidate.Calendar);
                    Assert.Equal(state.LiquidCells!.ContentHash, decision.Candidate.LiquidCells!.ContentHash);
                    queued.AddRange(choices);
                    if (tick == 1) { selected.Add(decision.Candidate); }
                    while (authority.ReadStateForHost().Pacing!.Encounter!.DueActors.Count != 0)
                    {
                        state = authority.ReadStateForHost();
                        WorldStepBatch resolutionBatch = fixture.Batch(state, WorldBatchKind.EncounterResolution);
                        var resolution = authority.Commit(resolutionBatch);
                        evidence.Record(resolution.Candidate, resolutionBatch, resolution.Receipts);
                        Assert.Empty(resolution.Receipts);
                        Assert.Equal(state.Calendar, resolution.Candidate.Calendar);
                    }
                }
                state = authority.ReadStateForHost();
                if (tick == 36)
                {
                    authority.Dispose();
                    evidence.BeforeQuantum(files.Path, state);
                    authority = SqliteWorldAuthority.OpenExisting(files.Path);
                    Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
                    Assert.Equal(1_750UL, authority.ReadStateForHost().Pacing!.Progress.Remainder);
                }
                WorldStepBatch advanceBatch = fixture.Batch(state, WorldBatchKind.TacticalAdvance);
                WorldStepDecision advanced = authority.Commit(advanceBatch);
                evidence.Record(advanced.Candidate, advanceBatch, advanced.Receipts);
                Assert.Empty(advanced.Receipts);
                Assert.Equal(genesis.Calendar.AddMilliseconds(tick * 50), advanced.Candidate.Calendar);
                Assert.Equal((ulong)tick * 5, advanced.Candidate.Pacing!.Encounter!.ElapsedTacticalUnits);
                Assert.Equal((ulong)(tick / 36), advanced.Candidate.Pacing.ProcessIntervals);
                if (tick < 36)
                {
                    Assert.Equal(beforeCells, LiquidCellStateCodecV2.Encode(advanced.Candidate.LiquidCells!));
                    Assert.Equal(genesis.Gathering!.ContentHash, advanced.Candidate.Gathering!.ContentHash);
                }
                if (tick is 1 or 35 or 36) { selected.Add(advanced.Candidate); }
                if (tick == 36) { evidence.Quantum(advanceBatch, advanced.Candidate); }
            }
            final = authority.ReadStateForHost();
            var expectedCells = new LiquidCellPlanner().PlanAdvance(genesis.LiquidCells!, genesis.Calendar.AddMilliseconds(1800))
                .ApplyTo(genesis.LiquidCells!);
            Assert.Equal(expectedCells.ContentHash, final.LiquidCells!.ContentHash);
            Assert.NotEqual(genesis.LiquidCells!.ContentHash, final.LiquidCells.ContentHash);
            Assert.Contains(final.LiquidCells.Quantities,
                pair => pair.Key.Coordinate == new OpenFantasy.Core.Terrain.CellCoordinate(117, 329, -1) && pair.Value > 0);
            Assert.Equal(1UL, final.LiquidCells.Revision);
            Assert.All(final.Gathering!.Actors, actor => Assert.Equal(980U, actor.Effort));
            Assert.Equal(genesis.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(genesis.Production!.ContentHash, final.Production!.ContentHash);
            Assert.Equal(genesis.Excavation!.ContentHash, final.Excavation!.ContentHash);
            Assert.Equal(genesis.Construction!.ContentHash, final.Construction!.ContentHash);
            Assert.Equal((ulong)queued.Count, authority.InspectForHost().Journal.CommandOutcomeCount);
            foreach (var command in queued)
            {
                var binding = final.Inventory.Controls.Single(control => control.ActorId == command.ActorId);
                Assert.NotNull(authority.FindReceipt(command, new(binding.PrincipalId, binding.SessionId)));
            }
            byte[] expected = WorldSnapshotCodec.Encode(final);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(genesis, WorldBatchKind.TacticalAdvance)));
            Assert.Equal(expected, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        }
        finally { authority.Dispose(); }
        evidence.Complete(files.Path, fixture, final);
        byte[] closed = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            foreach (WorldSnapshot state in selected.AsEnumerable().Reverse())
            {
                var recorded = reopened.ReadRecordedStateForHost(state.Revision);
                Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(recorded.State));
                Assert.Equal(final.Revision, recorded.SourceHeadRevision);
                Assert.Equal(final.ContentHash, recorded.SourceHeadHash);
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void A_canonical_root_with_unearned_queued_choices_cannot_be_used_as_database_genesis()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterPacingWorldFixture();
        WorldSnapshot world = fixture.Genesis;
        var timing = world.Pacing!.Encounter!;
        WorldActionCommand command = fixture.Wait(world, fixture.Player);
        var retained = new EncounterTimingSnapshot(timing.Scope, 0, 0, timing.Actors,
            [new EncounterDecision(command.ActorId, command.CommandId, EncounterDecisionKind.Wait, command.ContentHash)]);
        var pacing = new WorldPacingState(world.WorldId, world.BranchId, world.Pacing.OriginCalendar,
            world.Pacing.Progress.Capture(), retained, LocalExposureDuration.Zero);
        var forgedGenesis = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, 0, 0, gathering: world.Gathering, production: world.Production, progression: world.Progression,
            quests: world.Quests, construction: world.Construction, reservoirs: world.Reservoirs,
            excavation: world.Excavation, reservoirSeals: world.ReservoirSeals, liquidCells: world.LiquidCells, pacing: pacing);
        // Canonical component/root shape is deliberately insufficient evidence of an accepted queue command.
        Assert.Equal(WorldSnapshotCodec.Encode(forgedGenesis), WorldSnapshotCodec.Encode(
            WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(forgedGenesis))));
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateNew(files.Path, forgedGenesis));
        Assert.False(File.Exists(files.Path));
    }

    [Fact]
    public void Constructor_and_archive_reject_resident_exposure_that_disagrees_with_the_owned_clock()
    {
        var fixture = new EncounterPacingWorldFixture();
        WorldSnapshot world = fixture.Genesis;
        WorldResident retained = world.Residents[0];
        var forged = new WorldResident(retained.ActorId, retained.InventoryCustodianId, retained.DisplayName,
            retained.ControllerKind, retained.HomeFeet, retained.WanderRadiusCells, retained.Comprehension,
            LocalExposureDuration.FromMilliseconds(1), retained.OpeningUtterance);
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(world.Inventory, world.Space,
            world.Residents.Select(resident => resident.ActorId == forged.ActorId ? forged : resident),
            world.AdmissionVersion, world.Calendar, 0, 0, gathering: world.Gathering, production: world.Production,
            progression: world.Progression, quests: world.Quests, construction: world.Construction,
            reservoirs: world.Reservoirs, excavation: world.Excavation, reservoirSeals: world.ReservoirSeals,
            liquidCells: world.LiquidCells, pacing: world.Pacing));

        byte[] bytes = WorldSnapshotCodec.Encode(world);
        int residents = FieldOffset(bytes, 0, 4);
        int firstResident = FieldOffset(bytes, residents, 1);
        int exposureMilliseconds = FieldOffset(bytes, firstResident, 11);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(exposureMilliseconds, 8), 1);
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes));
    }

    [Fact]
    public void Inactive_profile22_keeps_the_complete_profile21_exploration_effects()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterPacingWorldFixture(active: false);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var candidate = authority.Commit(fixture.Batch(fixture.Genesis, WorldBatchKind.AdvancingStep)).Candidate;
        var baseline = new WorldStepProcessor().Decide(fixture.Basis,
            fixture.Batch(fixture.Basis, WorldBatchKind.AdvancingStep)).Candidate;
        Assert.Equal(22, candidate.ProfileVersion);
        Assert.Null(candidate.Pacing!.Encounter);
        Assert.Equal(baseline.Calendar, candidate.Calendar);
        Assert.Equal(baseline.AdvancingSteps, candidate.AdvancingSteps);
        Assert.Equal(baseline.Inventory.ContentHash, candidate.Inventory.ContentHash);
        Assert.Equal(baseline.Gathering!.ContentHash, candidate.Gathering!.ContentHash);
        Assert.Equal(baseline.LiquidCells!.ContentHash, candidate.LiquidCells!.ContentHash);
        Assert.Equal(baseline.Excavation!.ContentHash, candidate.Excavation!.ContentHash);
        Assert.Equal(baseline.Residents.Select(resident => resident.ExperiencedTime),
            candidate.Residents.Select(resident => resident.ExperiencedTime));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Schema5_is_exclusive_and_refuses_consequence_kind4(bool alterSchema)
    {
        using var files = new StorageFiles();
        var fixture = new EncounterPacingWorldFixture();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var command = fixture.Wait(fixture.Genesis, fixture.Player);
            authority.Commit(fixture.Batch(fixture.Genesis, WorldBatchKind.EncounterDecision, [command]));
        }
        Assert.Throws<SqliteException>(() => files.Execute("UPDATE world_commits SET kind=4"));
        if (alterSchema)
        {
            files.Execute("PRAGMA user_version=3");
        }
        else
        {
            files.Execute("UPDATE world_commits SET kind=1");
        }
        byte[] corrupted = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Equal(corrupted, File.ReadAllBytes(files.Path));
    }

    private static int FieldOffset(byte[] bytes, int frame, int index)
    {
        int offset = frame + 12;
        for (int field = 0; field < index; field++)
        {
            offset = checked(offset + 4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
        }
        return offset + 4;
    }
}

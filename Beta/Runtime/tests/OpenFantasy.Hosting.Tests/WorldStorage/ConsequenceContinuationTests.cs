using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConsequenceContinuationTests
{
    [Fact]
    public void Funded_excavation_resumes_real_work_after_reopen_and_publishes_the_atomic_settlement_once()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch trigger = fixture.NextExcavation(before);
        WorldActionCommand command = Assert.Single(trigger.Actions);
        WorldSnapshot immediate = ImmediateResult(before, trigger).Candidate;
        var history = new List<WorldSnapshot> { fixture.Genesis, before };
        var continuations = new List<WorldStepBatch>();
        RetainBefore(files.Path, fixture, trigger);

        WorldSnapshot state;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepDecision started = authority.Commit(RoundTrip(trigger));
            state = started.Candidate;
            Assert.Empty(started.Receipts);
            Assert.Equal(new[] { WorldCommittedIdentityKind.CommitCause, WorldCommittedIdentityKind.Command },
                started.Identities.Select(value => value.Kind).Order());
            Assert.Null(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)));
        }
        PendingExcavation initial = Assert.IsType<PendingExcavation>(state.PendingExcavation);
        Assert.Equal(before.Revision, initial.BasisRevision);
        Assert.Equal(before.ContentHash, initial.BasisHash);
        Assert.Equal(trigger.CauseId, initial.TriggerCauseId);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(initial.Command));
        Assert.Equal(0UL, initial.ContinuationCount);
        Assert.Equal(0UL, initial.Resolution.CompletedWorkUnits);
        Assert.False(initial.Resolution.IsComplete);
        AssertUnchangedPending(before, state);
        history.Add(state);
        CopyEvidence(files.Path, "pending.sqlite");

        WorldActionReceipt? terminal = null;
        for (int count = 0; state.PendingExcavation is not null; count++)
        {
            Assert.True(count <= ConstructionSettlementResolution.MaximumWorkUnits + 1,
                "The retained geometry must terminate within its declared work bound.");
            CopyEvidence(files.Path, $"pending-{count}.sqlite");
            PendingExcavation previous = state.PendingExcavation!;
            using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            WorldStepBatch batch = Continue(fixture, state);
            continuations.Add(batch);
            WorldStepDecision advanced = authority.Commit(RoundTrip(batch));
            state = advanced.Candidate;
            history.Add(state);
            if (state.PendingExcavation is { } pending)
            {
                Assert.Empty(advanced.Receipts);
                Assert.Equal(previous.ContinuationCount + 1, pending.ContinuationCount);
                Assert.Equal(previous.Resolution.CompletedWorkUnits + 1, pending.Resolution.CompletedWorkUnits);
                Assert.Equal(initial.BasisHash, pending.BasisHash);
                Assert.Equal(initial.ResultReservations, pending.ResultReservations);
                AssertUnchangedPending(before, state);
                Assert.Equal(WorldCommittedIdentityKind.CommitCause, Assert.Single(advanced.Identities).Kind);
            }
            else
            {
                terminal = Assert.Single(advanced.Receipts);
                Assert.Equal(WorldActionRejection.None, terminal.Rejection);
                Assert.Equal(command.CommandId, terminal.CommandId);
                Assert.Equal(state.Revision, terminal.CommitSequence);
                Assert.Contains(terminal.ResultId, initial.ResultReservations);
                Assert.Equal(new[] { initial.ReservedSourceId }, advanced.Identities
                    .Where(value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity).Select(value => value.Identity));
            }
        }
        Assert.True(continuations.Count > 1, "This fixture must retain actual intermediate geometric work.");
        WorldActionReceipt receipt = Assert.IsType<WorldActionReceipt>(terminal);
        AssertTerminalEquivalent(immediate, state);
        AssertSingleInterval(before, state);
        RetainAfter(files.Path, fixture, before, state, trigger, continuations, receipt, history);

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
                authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)))));
            foreach (WorldSnapshot expected in history.AsEnumerable().Reverse())
            {
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(
                    authority.ReadRecordedStateForHost(expected.Revision).State));
            }
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(trigger));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(continuations[^1]));
            Assert.Equal(state.ContentHash, authority.InspectForHost().State.ContentHash);
        }
    }

    [Fact]
    public void A_deferred_collision_refusal_charges_one_interval_without_work_or_source_issuance()
    {
        var fixture = new StructuralSettlementWorldFixture(overhangCollision: true, consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldStepBatch trigger = fixture.NextExcavation(before);
        WorldStepDecision immediate = ImmediateResult(before, trigger);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(immediate.Receipts).Rejection);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldStepDecision started = authority.Commit(trigger);
        Assert.Empty(started.Receipts);
        AssertUnchangedPending(before, started.Candidate);
        WorldStepDecision finished = Finish(authority, fixture, before, started.Candidate);

        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(finished.Receipts).Rejection);
        AssertTerminalEquivalent(immediate.Candidate, finished.Candidate);
        AssertSingleInterval(before, finished.Candidate);
        Assert.Equal(before.Inventory.ContentHash, finished.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, finished.Candidate.Excavation!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, finished.Candidate.ConstructionPhysical!.ContentHash);
        Assert.DoesNotContain(finished.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Equal(before.Gathering!.AdvanceStep(before.AdvancingSteps + 1).ContentHash,
            finished.Candidate.Gathering!.ContentHash);
    }

    [Fact]
    public void Pending_authority_accepts_only_empty_continuations_and_keeps_collision_reservations_exclusive()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Continue(fixture, before)));
        WorldSnapshot pending = authority.Commit(fixture.NextExcavation(before)).Candidate;
        PendingExcavation job = Assert.IsType<PendingExcavation>(pending.PendingExcavation);
        var movement = new WorldMovementInput(ConstructionUpgradeFixture.Delivery(pending, fixture.Player), fixture.Player, 32, 0);
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Actions.Batch(pending, [], [])));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Actions.Batch(pending, [], [movement])));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.NextExcavation(pending)));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(fixture.Actions.NextIdentity(), pending.WorldId,
            pending.BranchId, pending.Revision, pending.AdvancingSteps, WorldBatchKind.ConsequenceContinuation,
            [movement], [], pending.ProfileVersion));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(fixture.Actions.NextIdentity(), pending.WorldId,
            pending.BranchId, pending.Revision, pending.AdvancingSteps, WorldBatchKind.ConsequenceContinuation,
            [], [job.Command], pending.ProfileVersion));

        EntityId[] reservations = job.ResultReservations.ToArray();
        Assert.InRange(reservations.Length, 2, 2 * (ConstructionSettlementResolution.MaximumWorkUnits + 1));
        Assert.Equal(reservations.Length, reservations.Distinct().Count());
        IEnumerable<EntityId> attacks = reservations.Take(2).Concat(reservations.Skip(reservations.Length / 2).Take(2))
            .Concat(reservations.TakeLast(2)).Append(job.ReservedSourceId).Append(job.Command.CommandId).Append(job.TriggerCauseId);
        foreach (EntityId cause in attacks.Distinct())
        {
            Assert.True(pending.IdentityIndex.Contains(cause));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Continue(fixture, pending, cause)));
            Assert.Equal(pending.ContentHash, authority.InspectForHost().State.ContentHash);
        }
        WorldStepDecision finished = Finish(authority, fixture, before, pending);
        WorldActionReceipt receipt = Assert.Single(finished.Receipts);
        EntityId unused = reservations.First(value => value != receipt.ResultId);
        Assert.False(finished.Candidate.IdentityIndex.Contains(unused));
        WorldStepDecision next = authority.Commit(new(unused, pending.WorldId, pending.BranchId,
            finished.Candidate.Revision, finished.Candidate.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], pending.ProfileVersion));
        Assert.Equal(finished.Candidate.Revision + 1, next.Candidate.Revision);
    }

    [Fact]
    public void Historical_prospective_result_or_source_collisions_refuse_the_trigger_without_accepting_a_job()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot state = fixture.PrepareBeforeExcavation(files.Path);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        foreach (int role in new[] { 0, 1, 2 })
        {
            WorldActionCommand command = Assert.Single(fixture.NextExcavation(state).Actions);
            // This ordinary commit becomes the predecessor of the trigger, hence +3 is that
            // trigger's earliest possible terminal sequence. No hash preimage is needed.
            EntityId occupied = role == 2 ? EntityId.DeriveSpawned(command.CommandId, 0)
                : ProspectiveReceipt(command, state.Revision + 3,
                    role == 0 ? WorldActionRejection.None : WorldActionRejection.Unavailable).ResultId;
            state = authority.Commit(new(occupied, state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion)).Candidate;
            ContentHash original = state.ContentHash;
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Actions.Batch(state, [command], [])));
            Assert.Equal(original, authority.InspectForHost().State.ContentHash);
            Assert.Null(authority.InspectForHost().State.PendingExcavation);
            Assert.Null(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Player)));
        }
    }

    [Fact]
    public void Canonical_pending_archives_reject_forged_basis_command_and_geometric_work()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldSnapshot pending = authority.Commit(fixture.NextExcavation(before)).Candidate;
        byte[] original = WorldSnapshotCodec.Encode(pending);
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
        foreach (string fault in new[] { "basis_revision", "basis_hash", "command_revision", "cause_alias", "count", "work", "input_pin", "status" })
        {
            byte[][] root = Fields(original);
            Assert.Equal(22, root.Length);
            byte[][] job = Fields(root[21]);
            Assert.Equal(7, job.Length);
            switch (fault)
            {
                case "basis_revision": job[3] = Number(before.Revision + 1); break;
                case "basis_hash": job[4][0] ^= 1; break;
                case "command_revision":
                    WorldActionCommand old = pending.PendingExcavation!.Command;
                    job[1] = WorldActionCodec.Encode(new(old.CommandId, old.PrincipalId, old.OriginSessionId,
                        old.ActorId, old.WorldId, old.BranchId, old.ExpectedActorRevision + 1, old.Intent));
                    break;
                case "cause_alias": job[2] = pending.PendingExcavation!.Command.CommandId.ToByteArray(); break;
                case "count": job[5] = Number(1); break;
                default:
                    byte[][] resolution = Fields(job[6]);
                    Assert.Equal(4, resolution.Length);
                    if (fault == "work") { resolution[2] = Number(1); }
                    else if (fault == "input_pin") { resolution[1][0] ^= 1; }
                    else { resolution[3] = Number(1); }
                    job[6] = Frame(resolution);
                    break;
            }
            root[21] = Frame(job);
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(Frame(root)));
        }
        Assert.Equal(original, WorldSnapshotCodec.Encode(authority.InspectForHost().State));
    }

    private static WorldStepDecision Finish(SqliteWorldAuthority authority, StructuralSettlementWorldFixture fixture,
        WorldSnapshot basis, WorldSnapshot state)
    {
        for (int count = 0; count <= ConstructionSettlementResolution.MaximumWorkUnits + 1; count++)
        {
            WorldStepDecision decision = authority.Commit(Continue(fixture, state));
            state = decision.Candidate;
            if (state.PendingExcavation is null) { return decision; }
            Assert.Empty(decision.Receipts);
            AssertUnchangedPending(basis, state);
        }
        throw new InvalidOperationException("The finite consequence did not reach a terminal result.");
    }

    private static WorldStepBatch Continue(StructuralSettlementWorldFixture fixture, WorldSnapshot state, EntityId? cause = null) =>
        new(cause ?? fixture.Actions.NextIdentity(), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ConsequenceContinuation, [], [], state.ProfileVersion);

    private static WorldStepBatch RoundTrip(WorldStepBatch batch)
    {
        byte[] bytes = WorldStepCodec.Encode(batch);
        WorldStepBatch decoded = WorldStepCodec.Decode(bytes);
        Assert.Equal(bytes, WorldStepCodec.Encode(decoded));
        return decoded;
    }

    private static WorldStepDecision ImmediateResult(WorldSnapshot basis, WorldStepBatch trigger) =>
        new WorldStepProcessor().Decide(AsImmediateProfile(basis, basis.Revision), new(trigger.CauseId,
            trigger.WorldId, trigger.BranchId, trigger.ExpectedRevision, trigger.ExpectedAdvancingSteps,
            trigger.Kind, trigger.Movement, trigger.Actions, 15));

    // A read-only counterfactual strips only the new scheduling owner and normalizes commit revision.
    // It compares every profile15 field without constructing a replacement durable history.
    private static WorldSnapshot AsImmediateProfile(WorldSnapshot state, ulong revision) => new(
        state.Inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar, revision,
        state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production, state.Progression, state.Quests,
        state.Construction, excavation: state.Excavation, constructionPhysical: state.ConstructionPhysical,
        structuralPolicy: state.StructuralPolicy, structuralSettlementPolicy: state.StructuralSettlementPolicy);

    private static void AssertUnchangedPending(WorldSnapshot basis, WorldSnapshot pending)
    {
        Assert.NotNull(pending.PendingExcavation);
        Assert.Equal((ushort)16, pending.ProfileVersion);
        Assert.Equal(basis.ConsequencePolicy!.ContentHash, pending.ConsequencePolicy!.ContentHash);
        Assert.Equal(WorldSnapshotCodec.Encode(AsImmediateProfile(basis, basis.Revision)),
            WorldSnapshotCodec.Encode(AsImmediateProfile(pending, basis.Revision)));
    }

    private static void AssertTerminalEquivalent(WorldSnapshot immediate, WorldSnapshot actual)
    {
        Assert.Null(actual.PendingExcavation);
        Assert.Equal((ushort)16, actual.ProfileVersion);
        Assert.Equal(WorldSnapshotCodec.Encode(immediate), WorldSnapshotCodec.Encode(AsImmediateProfile(actual, immediate.Revision)));
    }

    private static void AssertSingleInterval(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
        Assert.All(before.Residents, resident => Assert.Equal(
            resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)), after.GetResident(resident.ActorId).ExperiencedTime));
    }

    private static WorldActionReceipt ProspectiveReceipt(WorldActionCommand command, ulong sequence, WorldActionRejection rejection) =>
        WorldReceiptCodecV1.Decode(Frame([Encoding.UTF8.GetBytes("world.action-receipt"), Number(1), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            command.ContentHash.ToByteArray(), Number(sequence),
            Number((ulong)(rejection == WorldActionRejection.None ? CommandReceiptStatus.Applied : CommandReceiptStatus.Rejected)),
            Number((ulong)rejection)]));

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        byte[][] fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] bytes = new byte[checked(12 + fields.Sum(value => checked(4 + value.Length)))];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            offset += 4;
            field.CopyTo(bytes, offset);
            offset += field.Length;
        }
        return bytes;
    }

    private static void RetainBefore(string path, StructuralSettlementWorldFixture fixture, WorldStepBatch trigger)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        File.WriteAllBytes(Path.Combine(directory, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "trigger.bin"), WorldStepCodec.Encode(trigger));
        CopyEvidence(path, "before.sqlite");
    }

    private static void RetainAfter(string path, StructuralSettlementWorldFixture fixture,
        WorldSnapshot before, WorldSnapshot after, WorldStepBatch trigger,
        IReadOnlyList<WorldStepBatch> continuations, WorldActionReceipt receipt, IReadOnlyList<WorldSnapshot> history)
    {
        string? directory = EvidenceDirectory();
        if (directory is null) { return; }
        CopyEvidence(path, "after.sqlite");
        for (int index = 0; index < continuations.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"continuation-{index + 1:D3}.bin"), WorldStepCodec.Encode(continuations[index]));
        }
        File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Funded intact settlement with a reserved interval and durable geometric continuations",
            after.ProfileVersion, WorldId = after.WorldId.ToString(), BranchId = after.BranchId.ToString(),
            SiteId = fixture.Site.SiteId.ToString(), SourceId = fixture.Site.CountedSourceId.ToString(),
            TerrainSourceId = fixture.Site.TerrainSourceId.ToString(),
            Player = fixture.Player.ToString(), Builder = fixture.Builder.ToString(),
            PreRevision = before.Revision, PreHash = before.ContentHash.Value,
            PostRevision = after.Revision, PostHash = after.ContentHash.Value,
            BeforeRevision = before.Revision, BeforeHash = before.ContentHash.Value,
            PendingRevision = history[2].Revision, PendingHash = history[2].ContentHash.Value,
            AfterRevision = after.Revision, AfterHash = after.ContentHash.Value,
            TriggerCauseId = trigger.CauseId.ToString(), CommandId = receipt.CommandId.ToString(),
            ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)), Continuations = continuations.Count,
            TriggerFile = "trigger.bin", BeforeFile = "before.sqlite", AfterFile = "after.sqlite",
            ContinuationFiles = Enumerable.Range(1, continuations.Count).Select(index => $"continuation-{index:D3}.bin"),
            PendingFiles = Enumerable.Range(0, continuations.Count).Select(index => $"pending-{index}.sqlite"),
            BeforeSteps = before.AdvancingSteps, AfterSteps = after.AdvancingSteps,
            SourceLotId = EntityId.DeriveSpawned(receipt.CommandId, 0).ToString(),
            BeforeInventoryHash = before.Inventory.ContentHash.Value, AfterInventoryHash = after.Inventory.ContentHash.Value,
            BeforePhysicalHash = before.ConstructionPhysical!.ContentHash.Value, AfterPhysicalHash = after.ConstructionPhysical!.ContentHash.Value,
            Actors = after.Space.Actors.Select(actor => new { ActorId = actor.ActorId.ToString(), actor.Feet.X, actor.Feet.Y, actor.Feet.Z, actor.Revision }),
            History = history.Select(world => new { world.Revision, Hash = world.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyEvidence(string source, string name)
    {
        string? directory = EvidenceDirectory();
        if (directory is not null) { File.Copy(source, Path.Combine(directory, name), true); }
    }

    private static string? EvidenceDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_CONSEQUENCE_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) { return null; }
        Directory.CreateDirectory(directory);
        return directory;
    }
}

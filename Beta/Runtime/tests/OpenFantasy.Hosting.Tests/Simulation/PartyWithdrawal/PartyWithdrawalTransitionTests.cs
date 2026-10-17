using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalTransitionTests
{
    private static EntityId Id(ulong value) => new(0x5749544855504752, value);
    private static WorldStepBatch Step(WorldSnapshot state, ulong id) => new(Id(id), state.WorldId,
        state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion);

    private static WorldSnapshot Prepare(string path)
    {
        using var authority = SqliteWorldAuthority.CreateNew(path, WithdrawalWorldFixture.Legacy);
        var state = authority.Commit(Step(authority.ReadStateForHost(), 1)).Candidate;
        var actor = state.Controllers!.Assignments[0];
        var input = new WorldControllerEvent(actor.ActorId, WorldControllerEventKind.Disconnected,
            actor.Revision, reason: WorldControllerDisconnectReason.TransportLost);
        state = authority.Commit(new WorldStepBatch(Id(2), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input)).Candidate;
        var segment = authority.BeginControllerRecoverySegment();
        authority.CheckpointControllerAllowances(segment.Generation,
            segment.Allowances.Select(row => row with { ElapsedHostMilliseconds = 12_345 }).ToArray());
        return state;
    }

    [Fact]
    public void Fresh_collective_world_reopens_in_schema_twelve_without_a_migration_entry()
    {
        using var files = new StorageFiles();
        var genesis = WorldPartyWithdrawalAdmission.CreateFresh(WithdrawalWorldFixture.Legacy);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.True(authority.SupportsControllerRecovery);
            final = authority.Commit(Step(genesis, 1)).Candidate;
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis)))
        {
            Assert.Equal(final.ContentHash, reopened.ReadStateForHost().ContentHash);
        }
        using var database = WorldDatabase.Open(files.Path, false);
        Assert.Equal(12, database.SchemaVersion);
        Assert.DoesNotContain(database.ReadJournal(), row => row.Kind == WorldCommitKind.ProfileMigration);
    }

    [Fact]
    public void Transition_preserves_every_other_facet_and_operational_allowance_through_replay()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot before = Prepare(source.Path);
        byte[] original = File.ReadAllBytes(source.Path);
        var request = WorldPartyWithdrawalTransition.ForHead(before, Id(3));
        WorldSnapshot after;
        using (var upgraded = SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path, request))
        {
            after = upgraded.ReadStateForHost();
            AssertFacts(before, after);
            Assert.Equal(12_345UL, Assert.Single(upgraded.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
            Assert.Equal(before.ContentHash, upgraded.ReadRecordedStateForHost(before.Revision).State.ContentHash);
            Assert.Equal(WithdrawalWorldFixture.Legacy.ContentHash, upgraded.ReadRecordedStateForHost(0).State.ContentHash);
            Assert.Equal(0UL, upgraded.InspectForHost().Journal.MaterialCareArchiveCount);
            after = upgraded.Commit(Step(after, 4)).Candidate;
        }
        Assert.Equal(original, File.ReadAllBytes(source.Path));
        using var reopened = SqliteWorldAuthority.OpenExisting(destination.Path,
            WorldSnapshotCodec.Encode(WithdrawalWorldFixture.Legacy));
        Assert.Equal(after.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.Equal(12_345UL, Assert.Single(reopened.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
        Assert.Throws<InvalidOperationException>(() => new WorldPartyWithdrawalTransitionProcessor().Decide(after, request));
        Assert.Throws<ArgumentException>(() => WorldPartyWithdrawalAdmission.CreateFresh(before));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Interrupted_publication_preserves_source_and_exposes_only_a_fully_verified_destination(int point)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var before = Prepare(source.Path);
        byte[] bytes = File.ReadAllBytes(source.Path);
        var request = WorldPartyWithdrawalTransition.ForHead(before, Id(3));
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path,
            request, boundary => { if ((int)boundary == point) throw new IOException("Injected publication interruption"); }));
        Assert.Equal(bytes, File.ReadAllBytes(source.Path));
        Assert.Equal(point == (int)WorldPartyWithdrawalTransitionBoundary.AfterPublish, File.Exists(destination.Path));
        if (File.Exists(destination.Path))
        {
            using var recovered = SqliteWorldAuthority.OpenExisting(destination.Path);
            AssertFacts(before, recovered.ReadStateForHost());
        }
        using var unchanged = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(before.ContentHash, unchanged.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Stale_head_foreign_scope_admission_anchor_and_occupied_cause_are_refused()
    {
        using var source = new StorageFiles();
        var head = Prepare(source.Path);
        var valid = WorldPartyWithdrawalTransition.ForHead(head, Id(3));
        var processor = new WorldPartyWithdrawalTransitionProcessor();
        WorldPartyWithdrawalTransition[] invalid =
        [
            new(Id(3), head.WorldId, head.BranchId, head.Revision - 1, head.AdvancingSteps,
                head.ContentHash, head.AdmissionVersion, null),
            new(Id(3), head.WorldId, head.BranchId, head.Revision, 0,
                head.ContentHash, head.AdmissionVersion, null),
            new(Id(3), head.BranchId, head.WorldId, head.Revision, head.AdvancingSteps,
                head.ContentHash, head.AdmissionVersion, null),
            new(Id(3), head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps,
                head.AdmissionVersion, head.AdmissionVersion, null),
            new(Id(3), head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps,
                head.ContentHash, head.ContentHash, null),
            new(Id(3), head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps,
                head.ContentHash, head.AdmissionVersion, head.ContentHash)
        ];
        Assert.All(invalid, request => Assert.Throws<InvalidOperationException>(() => processor.Decide(head, request)));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(head, valid, identity => identity == Id(3)));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(head,
            WorldPartyWithdrawalTransition.ForHead(head, head.Residents[0].ActorId)));
        Assert.Equal(processor.Decide(head, valid).Candidate.ContentHash, processor.Decide(head, valid).Candidate.ContentHash);
    }

    [Fact]
    public void Request_codec_is_canonical_and_rejects_other_interpretation_pairs_and_extra_bytes()
    {
        var head = WithdrawalWorldFixture.Legacy;
        var request = WorldPartyWithdrawalTransition.ForHead(head, Id(3));
        byte[] bytes = WorldPartyWithdrawalTransitionCodecV1.Encode(request);
        Assert.Equal(bytes, WorldPartyWithdrawalTransitionCodecV1.Encode(WorldPartyWithdrawalTransitionCodecV1.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldPartyWithdrawalTransitionCodecV1.Decode([.. bytes, 0]));
        byte[] wrong = (byte[])bytes.Clone();
        int offset = 12;
        for (int field = 0; field < 2; field++)
        {
            int length = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(wrong.AsSpan(offset)));
            offset += 4 + length;
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(wrong.AsSpan(offset + 4), 39);
        Assert.Throws<FormatException>(() => WorldPartyWithdrawalTransitionCodecV1.Decode(wrong));
        Assert.Throws<FormatException>(() => WorldPartyWithdrawalTransitionCodecV1.Decode(bytes.AsSpan(0, bytes.Length - 1)));
    }

    [Fact]
    public void Tampered_migration_receipt_is_rejected_and_existing_destination_is_never_overwritten()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var before = Prepare(source.Path);
        var request = WorldPartyWithdrawalTransition.ForHead(before, Id(3));
        using (SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path, request)) { }
        byte[] bytes = File.ReadAllBytes(destination.Path);
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path, request));
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, source.Path, request));
        Assert.Equal(bytes, File.ReadAllBytes(destination.Path));
        destination.Execute("UPDATE world_commits SET outcomes=x'00' WHERE kind=3");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(destination.Path));
    }

    [Fact]
    public void A_real_active_legacy_encounter_cannot_be_cleared_to_make_transition_eligible()
    {
        var source = WithdrawalWorldFixture.Active();
        var state = WithdrawalWorldFixture.Legacy;
        var occupied = new HashSet<EntityId>();
        foreach (var input in source.Batches)
        {
            var batch = new WorldStepBatch(input.CauseId, state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, input.Kind, input.Movement, input.Actions, state.ProfileVersion);
            var decision = new WorldStepProcessor().Decide(state, batch, occupied.Contains);
            occupied.UnionWith(decision.Identities.Select(row => row.Identity));
            state = decision.Candidate;
        }
        Assert.Equal(OpenFantasy.Core.Encounters.EncounterLifecyclePhase.Active, state.EncounterLifecycle!.Phase);
        var request = WorldPartyWithdrawalTransition.ForHead(state, Id(3));
        Assert.Throws<InvalidOperationException>(() => new WorldPartyWithdrawalTransitionProcessor().Decide(state, request));
    }

    [Fact]
    public void Earned_two_person_adventure_keeps_original_genesis_receipts_and_completed_construction()
    {
        string retained = FindRetainedAdventure();
        byte[] bytes = File.ReadAllBytes(retained);
        Assert.Equal("cf21a8be1d99dc39aa2b856e8be0857c0c7b01b210a45911dd9fbab93e2c7a21",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        File.WriteAllBytes(source.Path, bytes);
        WorldSnapshot before;
        using (var original = SqliteWorldAuthority.OpenExisting(source.Path)) before = original.ReadStateForHost();
        Assert.Equal(915UL, before.Revision);
        Assert.Equal("f0781754a2a2dea1304ab1ea594ba53e7d6291820f29d61154a13508b08f33e5", before.ContentHash.Value);
        Assert.True(before.Production!.Batches.Count > 0);
        Assert.True(before.Construction!.Projects.Single().Progress.CompletedUnits > 0);
        StoredWorldCommandOutcome[] gifts;
        using (var database = WorldDatabase.Open(source.Path, false))
        {
            gifts = database.ReadCommandOutcomes()
                .Where(row => WorldActionCodec.Decode(row.Command).Intent is MaterialProposalIntent).ToArray();
        }
        Assert.NotEmpty(gifts);
        using (var upgraded = SqliteWorldAuthority.UpgradePartyWithdrawal(source.Path, destination.Path,
            WorldPartyWithdrawalTransition.ForHead(before, Id(9000))))
        {
            AssertFacts(before, upgraded.ReadStateForHost());
            Assert.Equal(before.ContentHash, upgraded.ReadRecordedStateForHost(915).State.ContentHash);
            ContentHash head = upgraded.ReadStateForHost().ContentHash;
            foreach (var gift in gifts)
            {
                var command = WorldActionCodec.Decode(gift.Command);
                var receipt = upgraded.SubmitMaterialProposalCommand(command,
                    new(command.PrincipalId, command.OriginSessionId), Id(9001));
                Assert.Equal(gift.Receipt, WorldReceiptCodecV1.Encode(receipt));
            }
            Assert.Equal(head, upgraded.ReadStateForHost().ContentHash);
        }
        using var oldDb = WorldDatabase.Open(source.Path, false);
        using var newDb = WorldDatabase.Open(destination.Path, false);
        Assert.Equal(oldDb.ReadGenesis(), newDb.ReadGenesis());
        Assert.Equal(oldDb.ReadJournal().Select(EncodeRow), newDb.ReadJournal().Take(915).Select(EncodeRow));
        Assert.Equal(oldDb.ReadCommandOutcomes().Select(EncodeRow), newDb.ReadCommandOutcomes().Select(EncodeRow));
        Assert.Equal(oldDb.ReadIdentities(), newDb.ReadIdentities().Where(row => row.Sequence <= 915));
        Assert.Equal(bytes, File.ReadAllBytes(retained));
    }

    private static string EncodeRow<T>(T value) => JsonSerializer.Serialize(value);

    private static string FacetIdentity(object? value) =>
        value?.GetType().GetProperty("ContentHash")?.GetValue(value) is ContentHash hash
            ? hash.Value : JsonSerializer.Serialize(value);

    private static void AssertFacts(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal((ushort)41, after.ProfileVersion);
        Assert.Equal(before.Revision + 1, after.Revision);
        string[] changed = [nameof(WorldSnapshot.ProfileVersion), nameof(WorldSnapshot.Revision), nameof(WorldSnapshot.ContentHash),
            nameof(WorldSnapshot.EncounterLifecycle), nameof(WorldSnapshot.Retreat), nameof(WorldSnapshot.IdentityIndex)];
        foreach (var property in typeof(WorldSnapshot).GetProperties().Where(property => !changed.Contains(property.Name)))
        {
            Assert.True(FacetIdentity(property.GetValue(before)) == FacetIdentity(property.GetValue(after)),
                $"Transition changed owned facet {property.Name}");
        }
        Assert.Equal(before.EncounterLifecycle!.Revision + 1, after.EncounterLifecycle!.Revision);
        Assert.Equal(before.EncounterLifecycle.Debts, after.EncounterLifecycle.Debts);
        Assert.Equal((before.EncounterLifecycle.Phase, before.EncounterLifecycle.EncounterCount,
            before.EncounterLifecycle.ExplorationSteps, before.EncounterLifecycle.TacticalTicks),
            (after.EncounterLifecycle.Phase, after.EncounterLifecycle.EncounterCount,
            after.EncounterLifecycle.ExplorationSteps, after.EncounterLifecycle.TacticalTicks));
        Assert.Equal(before.Retreat!.AcceptedCount, after.Retreat!.AcceptedCount);
        Assert.Equal(before.Retreat.Last, after.Retreat.Last);
        Assert.Empty(after.Retreat.Withdrawal!.Parties);
        Assert.Equal(0UL, after.Retreat.Withdrawal.Revision);
    }

    private static string FindRetainedAdventure()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName,
                "Beta/implementation/p02/shared-adventure/attempt-06/shared-adventure/world.sqlite3");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("The retained earned shared adventure is required; no fresh substitute.");
    }
}

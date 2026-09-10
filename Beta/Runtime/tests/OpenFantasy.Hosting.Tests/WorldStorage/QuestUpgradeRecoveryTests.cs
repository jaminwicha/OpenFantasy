using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.QuestUpgradeFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class QuestUpgradeRecoveryTests
{
    [Theory]
    [InlineData("unknown-tag")]
    [InlineData("unsupported-version")]
    [InlineData("wrong-migration-family")]
    [InlineData("outcome")]
    [InlineData("missing-entity")]
    [InlineData("wrong-entity-role")]
    [InlineData("missing-definition")]
    [InlineData("missing-old-receipt")]
    [InlineData("unscheduled-checkpoint")]
    public void EditedMigrationOrEarlierHistoryCannotPassCanonicalRecovery(string mutation)
    {
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        QuestUpgradeHistory history = CreateCraftedHistory(gathering.Path, source.Path);
        WorldQuestMigration request = Request(history.State);
        using (SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path, request)) { }
        string latest = "(SELECT MAX(sequence) FROM world_commits)";
        string sql;
        if (mutation is "unknown-tag" or "unsupported-version" or "wrong-migration-family")
        {
            byte[] bytes = WorldQuestMigrationCodecV1.Encode(request);
            if (mutation == "unknown-tag") bytes[16] = (byte)'x';
            if (mutation == "unsupported-version")
            {
                int versionOffset = checked(20 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(12)));
                BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(versionOffset), 2);
            }
            if (mutation == "wrong-migration-family")
            {
                using WorldDatabase prior = WorldDatabase.Open(source.Path, create: false);
                bytes = prior.ReadJournal().Single(row => row.Kind == WorldCommitKind.ProfileMigration).AcceptedBatch;
            }
            sql = "UPDATE world_commits SET accepted_batch=x'" + Convert.ToHexString(bytes) + "' WHERE sequence=" + latest;
        }
        else
        {
            sql = mutation switch
            {
                "outcome" => "UPDATE world_commits SET outcomes=x'00' WHERE sequence=" + latest,
                "missing-entity" => "DELETE FROM world_occupied_identity WHERE kind=4 AND sequence=" + latest,
                "wrong-entity-role" => "UPDATE world_occupied_identity SET kind=5 WHERE kind=4 AND sequence=" + latest,
                "missing-definition" => "DELETE FROM world_occupied_identity WHERE kind=5 AND sequence=" + latest,
                "missing-old-receipt" => "DELETE FROM world_command_outcomes WHERE sequence=(SELECT MIN(sequence) FROM world_command_outcomes)",
                "unscheduled-checkpoint" => "INSERT INTO world_checkpoints SELECT sequence,advancing_steps,state,hash FROM world_head",
                _ => throw new ArgumentOutOfRangeException(nameof(mutation))
            };
        }
        destination.Execute(sql);

        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(destination.Path));
        using SqliteWorldAuthority original = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(history.State.ContentHash, original.InspectForHost().State.ContentHash);
    }

    [Fact]
    public void DataOnlyMigrationRollsBackHeadIdentitiesAndJournalWhileForeignKeysStayEnabled()
    {
        using var gathering = new StorageFiles();
        using var files = new StorageFiles();
        WorldSnapshot state = CreateCraftedHistory(gathering.Path, files.Path).State;
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            StoredWorldIdentity[] identities = database.ReadIdentities().ToArray();
            int commitCount = database.ReadJournal().Count();
            PreparedWorldQuestMigration prepared = new WorldQuestMigrationPreparation().Prepare(state,
                WorldSnapshotCodec.Decode(database.ReadGenesis()).IdentityIndex, state.ContentHash,
                Request(state), identity => database.ContainsIdentity(identity));
            Assert.Throws<IOException>(() => database.AppendQuestMigration(prepared, false, at =>
            {
                Assert.Equal(3, database.SchemaVersion);
                if (at == WorldQuestUpgradeBoundary.BeforeCommit) throw new IOException("rollback quest admission");
            }));
            Assert.Equal(identities, database.ReadIdentities().ToArray());
            Assert.Equal(commitCount, database.ReadJournal().Count());
            Assert.Equal(state.Revision, database.ReadHead().Sequence);
            Assert.Equal(3, database.SchemaVersion);
            Assert.Equal(1, database.Inspect().ForeignKeys);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(state.ContentHash, reopened.InspectForHost().State.ContentHash);
    }

    [Fact]
    public void StaleHeadHistoricalCauseAndDestinationConflictsLeaveOriginalProgressIntact()
    {
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        QuestUpgradeHistory history = CreateCraftedHistory(gathering.Path, source.Path);
        WorldSnapshot state = history.State;
        byte[] before = File.ReadAllBytes(source.Path);
        WorldQuestMigration request = Request(state);
        var stale = new WorldQuestMigration(request.CauseId, request.WorldId, request.BranchId,
            request.ExpectedRevision, request.ExpectedAdvancingSteps, ContentHash.Compute("wrong-retained-head"u8),
            request.SourceAdmissionVersion, request.TargetAdmissionVersion, request.Admission);

        Assert.Throws<InvalidOperationException>(() => SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path, stale));
        Assert.Throws<InvalidOperationException>(() => SqliteWorldAuthority.UpgradeProductionToQuests(source.Path,
            destination.Path, Request(state, history.Commands[0].CommandId)));
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, source.Path, request));
        Assert.False(File.Exists(destination.Path));
        File.WriteAllBytes(destination.Path, "existing user save"u8.ToArray());
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path, request));
        Assert.Equal("existing user save"u8.ToArray(), File.ReadAllBytes(destination.Path));
        Assert.Equal(before, File.ReadAllBytes(source.Path));
    }

    [Fact]
    public void UnfundedAcceptanceIsAStableRejectedReceiptWithoutUnpaidParticipation()
    {
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = CreateCraftedHistory(gathering.Path, source.Path).State;
        WorldActionCommand command;
        byte[] receipt;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeProductionToQuests(source.Path,
            destination.Path, Request(state, funds: 199)))
        {
            WorldSnapshot admitted = authority.InspectForHost().State;
            command = Accept(admitted);
            WorldStepDecision decision = authority.Commit(Batch(admitted, command));
            Assert.Equal(WorldActionRejection.Unavailable, decision.Receipts.Single().Rejection);
            Assert.Empty(decision.Candidate.Quests!.Participations);
            Assert.Equal(199UL, decision.Candidate.Progression!.Currency.GetAccount(Reserve).Balance);
            Assert.Equal(0UL, decision.Candidate.Progression.Currency.GetActorAccount(Id(10)).Balance);
            receipt = WorldReceiptCodecV1.Encode(decision.Receipts.Single());
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(destination.Path);
        Assert.Equal(receipt, WorldReceiptCodecV1.Encode(reopened.FindReceipt(command, Delivery)!));
        Assert.Empty(reopened.InspectForHost().State.Quests!.Participations);
    }

    [Theory]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void InterruptedCompletionRecoversEitherWholeRewardOrNoReward(int value)
    {
        var boundary = (AuthorityCommitBoundary)value;
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = CreateCraftedHistory(gathering.Path, source.Path).State;
        WorldActionCommand completion;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path, Request(state)))
        {
            state = authority.InspectForHost().State;
            state = authority.Commit(Batch(state, Accept(state))).Candidate;
            completion = Complete(state);
            Assert.Throws<IOException>(() => authority.Commit(Batch(state, completion), at =>
            {
                if (at == boundary) throw new IOException("injected completion interruption");
            }));
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(destination.Path);
        WorldSnapshot recovered = reopened.InspectForHost().State;
        bool committed = boundary == AuthorityCommitBoundary.AfterCommit;
        Assert.Equal(committed ? 200UL : 0UL, recovered.Progression!.Currency.GetActorAccount(Id(10)).Balance);
        Assert.Equal(committed ? 0UL : 200UL, recovered.Progression.Currency.GetAccount(Reserve).Balance);
        Assert.Equal(committed ? 6UL : 0UL, recovered.Inventory.Custody.Lots.Where(lot => lot.CustodianId == Project)
            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(committed, reopened.FindReceipt(completion, Delivery) is not null);
        Assert.Equal(state.AdvancingSteps, recovered.AdvancingSteps);
        Assert.Equal(state.Calendar, recovered.Calendar);
    }
}

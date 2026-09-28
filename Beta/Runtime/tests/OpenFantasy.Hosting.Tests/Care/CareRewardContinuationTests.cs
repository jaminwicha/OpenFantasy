using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CareRewardContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_care_victory_funded_reward_and_later_attack_preserve_the_complete_earned_trace(bool crafted)
    {
        var journey = new CareRewardJourney(crafted);
        using var files = new StorageFiles();
        using var pendingSave = new StorageFiles();
        using var rewardedSave = new StorageFiles();
        WorldSnapshot defeated, pending, treated, beforeReward, rewarded, later;
        WorldStepBatch acknowledgmentBatch, treatmentBatch, rewardBatch;
        WorldActionReceipt acknowledgmentReceipt, treatmentReceipt, rewardReceipt;
        WorldActionCommand acknowledgment, treatment, reward;

        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, journey.Genesis))
        {
            defeated = journey.PrepareDefeat(new(authority));
            Assert.NotEmpty(defeated.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>());
            Assert.Equal(0U, defeated.EncounterActions!.LastResolution!.TargetHpAfter);
            acknowledgment = journey.Acknowledge(defeated);
            acknowledgmentBatch = journey.Batch(defeated, WorldBatchKind.Care, [acknowledgment]);
            var result = CareRewardJourney.Commit(new(authority), acknowledgmentBatch);
            acknowledgmentReceipt = Assert.Single(result.Receipts);
            pending = result.Candidate;
            Assert.Equal(defeated.Progression!.ContentHash, pending.Progression!.ContentHash);
            Assert.Equal(defeated.Inventory.ContentHash, pending.Inventory.ContentHash);
            Assert.Equal(defeated.ResourceHistory.ContentHash, pending.ResourceHistory!.ContentHash);
            Assert.Equal(defeated.Calendar, pending.Calendar);
            Assert.Equal(acknowledgment.CommandId, pending.Care!.Pending!.CommandId);
            authority.BackupForHost(pendingSave.Path);
            if (!crafted) ExportPending(authority, journey, pending);
            RoundTrip(pending);
        }

        using (var authority = OpenExact(files.Path, pending))
        {
            AssertDuplicateUnchanged(authority, journey, acknowledgmentBatch, acknowledgment, acknowledgmentReceipt);
            treatment = journey.Treat(pending);
            treatmentBatch = journey.Batch(pending, WorldBatchKind.Care, [treatment]);
            var result = CareRewardJourney.Commit(new(authority), treatmentBatch);
            treatmentReceipt = Assert.Single(result.Receipts);
            treated = result.Candidate;
            var care = Assert.Single(treated.Care!.Treatments);
            Assert.Equal(treatment.CommandId, care.CommandId);
            Assert.Equal(pending.Care!.Pending!.AcknowledgmentId, care.AcknowledgmentId);
            Assert.Equal(40, treated.Progression!.GetActor(journey.Player).CurrentHp);
            Assert.Equal(pending.Calendar, treated.Calendar);
            Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle!.Phase);
            Assert.True(treated.Pacing!.Encounter!.GetActor(journey.Helper).RemainingDelay > 0);
            Assert.Equivalent(defeated.EncounterActions!.LastResolution, treated.EncounterActions!.LastResolution, strict: true);
            AssertDose(treated, crafted, treatment.CommandId);
            var remedy = treated.Care.GetRemedy(treated.Care.Agreements.Single().RemedyDefinitionId);
            EntityId helperStorage = treated.GetResident(journey.Helper).InventoryCustodianId;
            ulong rawDoses = treated.Inventory.Custody.Lots.Where(lot => lot.CustodianId == helperStorage
                && lot.Definition == remedy.Item).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
            Assert.Equal(crafted ? 0UL : 1UL, rawDoses);
            Assert.DoesNotContain(treated.Production!.Lots, lot => lot.Definition == remedy.Item);
            Assert.IsType<WorldTreatmentResourceEvidence>(treated.ResourceHistory!.Evidence[^1]);
            RoundTrip(treated);
        }

        using (var authority = OpenExact(files.Path, treated))
        {
            AssertDuplicateUnchanged(authority, journey, treatmentBatch, treatment, treatmentReceipt);
            var session = new CareWorldSession(authority);
            var released = journey.WinAndRelease(session);
            Assert.Equal(EncounterLifecyclePhase.Exploration, released.EncounterLifecycle!.Phase);
            Assert.Equal(0, released.Progression!.GetActor(journey.First.TargetActorId).CurrentHp);
            Assert.InRange(released.Progression.GetActor(journey.Player).CurrentHp, 1, 40);
            reward = journey.PrepareReward(session);
            beforeReward = authority.ReadStateForHost();
            rewardBatch = journey.Batch(beforeReward, WorldBatchKind.HeldInteraction, [reward]);
            var result = CareRewardJourney.Commit(session, rewardBatch);
            rewardReceipt = Assert.Single(result.Receipts);
            rewarded = result.Candidate;
            AssertReward(journey, beforeReward, rewarded, reward.CommandId);
            AssertDose(rewarded, crafted, treatment.CommandId);
            Assert.Equal(treated.Care!.ContentHash, rewarded.Care!.ContentHash);
            Assert.Equivalent(released.EncounterLifecycle!.Actions!.LastResolution, rewarded.EncounterLifecycle!.Actions!.LastResolution, strict: true);
            authority.BackupForHost(rewardedSave.Path);
            RoundTrip(rewarded);
        }

        using (var authority = OpenExact(files.Path, rewarded))
        {
            AssertDuplicateUnchanged(authority, journey, acknowledgmentBatch, acknowledgment, acknowledgmentReceipt);
            AssertDuplicateUnchanged(authority, journey, treatmentBatch, treatment, treatmentReceipt);
            AssertDuplicateUnchanged(authority, journey, rewardBatch, reward, rewardReceipt);
            later = journey.LaterPlayerAttack(new(authority));
            var earned = rewarded.Progression!.GetActor(journey.Player);
            var attack = Assert.Single(later.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>(),
                value => value.ScenarioId == journey.Second.DefinitionId && value.Request.ActorId == journey.Player);
            var ability = later.EncounterLifecycle!.Abilities.Single(value => value.DefinitionId == attack.Resolution.AbilityDefinitionId);
            Assert.Equal(ability.Delay(checked((uint)earned.Stats.Speed * 10)), attack.Resolution.Delay);
            var target = rewarded.Progression.GetActor(journey.Second.TargetActorId);
            uint expectedDamage = EncounterActionRules.ComputePhysicalDamage(earned.Stats.Attack, target.Stats.Defense,
                ability.PowerTenths, attack.Resolution.VariationPermille);
            Assert.Equal(expectedDamage, attack.Resolution.Damage);
            uint unearnedDamage = EncounterActionRules.ComputePhysicalDamage(journey.Genesis.Progression!.GetActor(journey.Player).Stats.Attack,
                target.Stats.Defense, ability.PowerTenths, attack.Resolution.VariationPermille);
            Assert.True(expectedDamage > unearnedDamage, "This actual attack must use earned rather than genesis attack stats.");
            Assert.Equal(rewarded.Progression.Currency.GetActorAccount(journey.Player).Balance,
                later.Progression!.Currency.GetActorAccount(journey.Player).Balance);
            Assert.Equal(rewarded.Quests!.ContentHash, later.Quests!.ContentHash);
            AssertDose(later, crafted, treatment.CommandId);
            Assert.Single(later.Care!.Acknowledgments);
            Assert.Single(later.Care.Treatments);
            Assert.Single(later.ResourceHistory.Evidence.OfType<WorldRewardResourceEvidence>());
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            RoundTrip(later);
            foreach (var expected in new[] { defeated, pending, treated, rewarded })
            {
                RecordedWorldState recorded = authority.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
                Assert.Equal(expected.ContentHash, recorded.StateHash);
                Assert.Equal(later.Revision, recorded.SourceHeadRevision);
                Assert.Equal(later.ContentHash, recorded.SourceHeadHash);
            }
        }

        byte[] closed = File.ReadAllBytes(files.Path);
        using (var authority = OpenExact(files.Path, later))
        {
            AssertDuplicateUnchanged(authority, journey, rewardBatch, reward, rewardReceipt);
            AssertDuplicateUnchanged(authority, journey, treatmentBatch, treatment, treatmentReceipt);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        if (!crafted)
        {
            AssertPendingCounterexamples(pendingSave.Path, pending, journey.BeforeDefeat!);
            AssertConservedCurrencyCounterexample(rewardedSave.Path, rewarded, journey.Player);
            Assert.Equal(closed, File.ReadAllBytes(files.Path));
        }
    }

    private static void AssertReward(CareRewardJourney journey, WorldSnapshot before, WorldSnapshot after, EntityId command)
    {
        var prior = before.Progression!.GetActor(journey.Player);
        var current = after.Progression!.GetActor(journey.Player);
        var definition = after.Quests!.Definitions.Single();
        Assert.Equal(420UL, definition.Reward.Experience);
        Assert.True(current.Level > prior.Level);
        ulong spentOnLevels = Enumerable.Range(prior.Level, current.Level - prior.Level)
            .Aggregate(0UL, (total, level) => checked(total + prior.Curve.RequiredExperience(level)));
        Assert.Equal(prior.Experience + 420, current.Experience + spentOnLevels);
        Assert.True(prior.CurrentHp < prior.Stats.MaxHp);
        Assert.Equal(current.Stats.MaxHp, current.CurrentHp);
        Assert.Equal(current.Stats.MaxMp, current.CurrentMp);
        Assert.True(current.CurrentHp > prior.CurrentHp);
        Assert.True(current.CurrentMp > prior.CurrentMp); // Original Strike costs zero; level growth expands the MP pool.
        var batch = after.ResourceHistory!.History.Batches[^1];
        Assert.Equal(command, batch.CommandId);
        var change = Assert.Single(batch.Changes);
        Assert.Equal(ActorResourceOperation.ExperienceReward, change.Operation);
        Assert.Equal(420UL, change.RequestedAmount);
        Assert.Equal(prior.Revision + 1, change.After.Revision);
        Assert.Equal(command, Assert.IsType<WorldRewardResourceEvidence>(after.ResourceHistory.Evidence[^1]).CommandId);
        Assert.Equal(command, Assert.Single(after.Quests.Participations).CompletionCommandId);
        EntityId reserve = definition.Reward.ReserveAccountId;
        Assert.Equal(before.Progression.Currency.GetAccount(reserve).Balance - definition.Reward.CurrencyAmount,
            after.Progression.Currency.GetAccount(reserve).Balance);
        Assert.Equal(before.Progression.Currency.GetActorAccount(journey.Player).Balance + definition.Reward.CurrencyAmount,
            after.Progression.Currency.GetActorAccount(journey.Player).Balance);
        Assert.Equal(before.Progression.Currency.AdmittedSupply, after.Progression.Currency.AdmittedSupply);
        Assert.Equal(6UL, after.Inventory.Custody.GetStoredQuantity(after.Construction!.Projects.Single().ProjectCustodianId));
        Assert.Equal(before.Calendar, after.Calendar);
    }

    private static void AssertDose(WorldSnapshot world, bool crafted, EntityId command)
    {
        var dose = Assert.Single(world.Production!.ConsumedItems);
        Assert.Equal(command, dose.CommandId);
        Assert.Equal(1UL, dose.Ingredient.Quantity);
        Assert.Equal(crafted ? IngredientLotKind.Transformed : IngredientLotKind.Source, dose.Ingredient.Kind);
        Assert.Equal(crafted ? 1 : 0, world.Production.Batches.Count);
    }

    private static SqliteWorldAuthority OpenExact(string path, WorldSnapshot expected)
    {
        var authority = SqliteWorldAuthority.OpenExisting(path);
        try
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            return authority;
        }
        catch { authority.Dispose(); throw; }
    }

    private static void AssertDuplicateUnchanged(SqliteWorldAuthority authority, CareRewardJourney journey,
        WorldStepBatch batch, WorldActionCommand command, WorldActionReceipt expected)
    {
        var before = authority.InspectForHost();
        for (int retry = 0; retry < 2; retry++)
        {
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, journey.Delivery(command)));
            Assert.Equal(WorldReceiptCodecV1.Encode(expected), WorldReceiptCodecV1.Encode(receipt));
        }
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
        Assert.Equal(before.State.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(before.Journal.CommandOutcomeCount, authority.InspectForHost().Journal.CommandOutcomeCount);
    }

    private static void AssertPendingCounterexamples(string path, WorldSnapshot pending, WorldSnapshot beforeDefeat)
    {
        byte[] goodFile = File.ReadAllBytes(path);
        var old = pending.Care!;
        var actual = old.Pending!;
        var wrongBoundary = new CareAcknowledgment(actual.AcknowledgmentId, actual.CommandId, actual.AgreementId,
            actual.EncounterId, actual.WorldSequence, beforeDefeat.EncounterActions!.ContentHash,
            beforeDefeat.Pacing!.Encounter!.ContentHash, actual.Defeat);
        var changed = new CareSnapshot(old.WorldId, old.BranchId, old.Revision, old.Remedies, old.Agreements,
            [wrongBoundary], old.Treatments);
        byte[] component = CareCodecV1.Encode(changed);
        Assert.Equal(component, CareCodecV1.Encode(CareCodecV1.Decode(component)));
        byte[] wrongRoot = ReplaceField(pending, 22, component);
        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(wrongRoot));
        Assert.Contains("acknowledged incapacitation", Assert.IsType<ArgumentException>(error.InnerException).Message);
        AssertStoredRewriteRejected(path, pending, wrongRoot, rewriteCommitHash: false);

        // Removing the acknowledgment produces a canonical internally valid complete world: HP
        // and resources never changed at acknowledgment. Only accepted-command replay can prove
        // that this omitted obligation really exists. Update checksums as well to exercise that gate.
        var omitted = new CareSnapshot(old.WorldId, old.BranchId, 0, old.Remedies, old.Agreements, [], []);
        byte[] omittedRoot = ReplaceField(pending, 22, CareCodecV1.Encode(omitted));
        var structurallyValid = WorldSnapshotCodec.Decode(omittedRoot);
        Assert.Null(structurallyValid.Care!.Pending);
        Assert.Equal(pending.Progression!.ContentHash, structurallyValid.Progression!.ContentHash);
        Assert.Equal(omittedRoot, WorldSnapshotCodec.Encode(structurallyValid));
        AssertStoredRewriteRejected(path, pending, omittedRoot, rewriteCommitHash: true);
        Assert.Equal(goodFile, File.ReadAllBytes(path));
    }

    private static void AssertConservedCurrencyCounterexample(string path, WorldSnapshot rewarded, EntityId player)
    {
        byte[] goodFile = File.ReadAllBytes(path);
        var progression = rewarded.Progression!;
        var currency = progression.Currency;
        EntityId recipient = currency.GetActorAccount(player).AccountId;
        EntityId reserve = rewarded.Quests!.Definitions.Single().Reward.ReserveAccountId;
        var redirected = new CurrencyLedger(currency.WorldId, currency.BranchId, currency.Revision,
            currency.Currency, currency.AdmittedSupply, currency.Accounts.Select(account => new CurrencyAccount(
                account.AccountId, account.Kind, account.ActorId,
                account.AccountId == recipient ? checked(account.Balance - 1)
                    : account.AccountId == reserve ? checked(account.Balance + 1) : account.Balance, account.Revision)));
        var conserved = new ProgressionSnapshot(progression.WorldId, progression.BranchId, progression.Revision,
            progression.Profiles, progression.Curves, progression.Actors, redirected);
        byte[] component = ProgressionCodecV1.Encode(conserved);
        Assert.Equal(component, ProgressionCodecV1.Encode(ProgressionCodecV1.Decode(component)));
        rewarded.ResourceHistory!.History.ValidateCurrent(conserved, rewarded.Revision);
        byte[] wrongRoot = ReplaceField(rewarded, 13, component);
        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(wrongRoot));
        Assert.Contains("funded currency", Assert.IsType<ArgumentException>(error.InnerException).Message);
        AssertStoredRewriteRejected(path, rewarded, wrongRoot, rewriteCommitHash: false);
        Assert.Equal(goodFile, File.ReadAllBytes(path));
    }

    private static byte[] ReplaceField(WorldSnapshot original, int index, byte[] replacement)
    {
        var fields = ReadFrame(WorldSnapshotCodec.Encode(original));
        fields[index] = replacement;
        return WriteFrame(fields);
    }

    private static void AssertStoredRewriteRejected(string goodPath, WorldSnapshot original, byte[] changed, bool rewriteCommitHash)
    {
        using var copy = new StorageFiles();
        File.Copy(goodPath, copy.Path);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = copy.Path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE world_head SET state=$state,hash=$hash WHERE singleton=1";
            command.Parameters.AddWithValue("$state", changed);
            command.Parameters.AddWithValue("$hash", ContentHash.Compute(changed).ToByteArray());
            Assert.Equal(1, command.ExecuteNonQuery());
            if (rewriteCommitHash)
            {
                command.CommandText = "UPDATE world_commits SET after_hash=$hash WHERE sequence=$sequence";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$hash", ContentHash.Compute(changed).ToByteArray());
                byte[] sequence = new byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(sequence, original.Revision);
                command.Parameters.AddWithValue("$sequence", sequence);
                Assert.Equal(1, command.ExecuteNonQuery());
            }
            transaction.Commit();
        }
        byte[] corrupted = File.ReadAllBytes(copy.Path);
        var failure = Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(copy.Path));
        if (rewriteCommitHash) Assert.Contains("deterministic replay", failure.Message);
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(copy.Path, 0));
        Assert.Equal(corrupted, File.ReadAllBytes(copy.Path));
    }

    private static void ExportPending(SqliteWorldAuthority authority, CareRewardJourney journey, WorldSnapshot pending)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_CARE_CONTINUATION_EVIDENCE") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        authority.BackupForHost(Path.Combine(directory, "pending.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "pending.ofc"), WorldSnapshotCodec.Encode(pending));
        var acknowledgment = pending.Care!.Pending!;
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            fixture = "production-pending-care",
            seed = EncounterJourneyWorldFixture.SeedText,
            playerName = EncounterJourneyWorldFixture.PlayerName,
            worldId = pending.WorldId.ToString(), branchId = pending.BranchId.ToString(),
            playerActorId = journey.Player.ToString(), helperActorId = journey.Helper.ToString(),
            encounterId = acknowledgment.EncounterId.ToString(), acknowledgmentId = acknowledgment.AcknowledgmentId.ToString(),
            acknowledgmentCommandId = acknowledgment.CommandId.ToString(),
            headRevision = pending.Revision, headHash = pending.ContentHash.Value,
            admissionHash = pending.AdmissionVersion.Value, actionHash = acknowledgment.ActionHash.Value,
            timingHash = acknowledgment.TimingHash.Value, worldRootHash = pending.ContentHash.Value,
            playerHp = pending.Progression!.GetActor(journey.Player).CurrentHp,
            treatments = pending.Care.Treatments.Count, consumedDoses = pending.Production!.ConsumedItems.Count
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

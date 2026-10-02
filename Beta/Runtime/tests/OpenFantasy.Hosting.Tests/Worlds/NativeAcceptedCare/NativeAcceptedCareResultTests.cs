using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.NativeAcceptedCare;

/// <summary>Authenticates the entire unplayed-genesis journey, including the helper's separate command and exact dose.</summary>
public sealed class NativeAcceptedCareResultTests
{
    [Fact]
    public void Full_native_care_journal_proves_separate_treatment_exact_dose_and_same_episode_recovery()
    {
        string path = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ACCEPTED_CARE_RESULTS")
            ?? throw new InvalidOperationException("An explicit native result manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement result = Assert.Single(manifest.RootElement.GetProperty("results").EnumerateArray());
        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-accepted-care-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string source = result.GetProperty("save").GetString()!;
            Assert.False(File.Exists(source + "-wal") || File.Exists(source + "-shm"));
            string save = Path.Combine(scratch, "save.sqlite");
            File.Copy(source, save);
            byte[] bytes = File.ReadAllBytes(result.GetProperty("genesis").GetString()!);
            Assert.Equal(WorldSnapshotCodec.Encode(CargoOutcomeWorldAdmission.CreateEmbedded(
                NativeAcceptedCareFixtureTests.Seed, NativeAcceptedCareFixtureTests.PlayerName)), bytes);
            Assert.Equal(bytes, File.ReadAllBytes(result.GetProperty("before").GetString()!));
            WorldSnapshot genesis = WorldSnapshotCodec.Decode(bytes);
            EntityId player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            var playerControl = genesis.Inventory.Controls.Single(value => value.ActorId == player);
            var agreement = Assert.Single(genesis.Care!.Agreements, value => value.PatientActorId == player);
            EntityId helper = agreement.HelperActorId;
            Assert.Equal(genesis.Progression!.GetActor(player).Stats.MaxHp, genesis.Progression.GetActor(player).CurrentHp);
            Assert.Equal(0UL, genesis.EncounterLifecycle!.EncounterCount);
            using var document = JsonDocument.Parse(File.ReadAllText(result.GetProperty("report").GetString()!));
            JsonElement report = document.RootElement;
            Assert.Equal(("care-accepted", "passed"), (report.GetProperty("Mode").GetString(), report.GetProperty("Status").GetString()));
            Assert.Equal(helper.ToString(), report.GetProperty("AcceptedCare").GetProperty("HelperActorId").GetString());
            Assert.Equal(player.ToString(), report.GetProperty("AcceptedCare").GetProperty("PatientActorId").GetString());
            WorldSnapshot final;
            using (var authority = SqliteWorldAuthority.OpenExisting(save, bytes)) final = authority.ReadStateForHost();
            List<StoredWorldCommit> journal;
            List<StoredWorldCommandOutcome> outcomes;
            using (var database = WorldDatabase.Open(save, create: false))
            {
                journal = database.ReadJournal(genesis.Revision).ToList();
                outcomes = database.ReadCommandOutcomes(genesis.Revision).ToList();
            }
            var processor = new WorldStepProcessor();
            WorldSnapshot state = genesis;
            var batches = new Dictionary<ulong, WorldStepBatch>();
            var identities = new HashSet<EntityId>();
            int movements = 0, entries = 0, defeats = 0, acknowledgments = 0, treatments = 0, readings = 0, completions = 0;
            EntityId? episode = null;
            foreach (StoredWorldCommit commit in journal)
            {
                WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
                batches.Add(commit.Sequence, batch);
                WorldSnapshot next = processor.Decide(state, batch, _ => false).Candidate;
                Assert.Equal(commit.AfterHash, next.ContentHash);
                foreach (WorldMovementInput movement in batch.Movement.Where(value => value.ActorId == player))
                {
                    Assert.Equal(0, entries); // All native travel precedes combat; no fixture teleport or post-defeat walk.
                    Assert.Equal(playerControl.PrincipalId, movement.Delivery.PrincipalId);
                    Assert.Equal(playerControl.SessionId, movement.Delivery.SessionId);
                    if (state.Space.GetActor(player).Feet != next.Space.GetActor(player).Feet) movements++;
                }
                if (state.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active
                    && next.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome)
                {
                    defeats++;
                    Assert.Equal(player, next.EncounterActions!.LastResolution!.TargetActorId);
                    Assert.Equal(0, next.Progression!.GetActor(player).CurrentHp);
                    Assert.Equal(EncounterActionResolutionStatus.Damaged, next.EncounterActions.LastResolution.Status);
                }
                foreach (WorldActionCommand command in batch.Actions)
                {
                    Assert.True(identities.Add(command.CommandId));
                    var control = state.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
                    Assert.Equal((control.PrincipalId, control.SessionId), (command.PrincipalId, command.OriginSessionId));
                    StoredWorldCommandOutcome stored = Assert.Single(outcomes, value => value.CommandId == command.CommandId);
                    Assert.Equal(WorldActionCodec.Encode(command), stored.Command);
                    var receipt = WorldReceiptCodecV1.Decode(stored.Receipt);
                    Assert.Equal((CommandReceiptStatus.Applied, WorldActionRejection.None, commit.Sequence),
                        (receipt.Status, receipt.Rejection, receipt.CommitSequence));
                    switch (command.Intent)
                    {
                        case EnterEncounterIntent:
                            Assert.Equal(player, command.ActorId);
                            Assert.True(movements > 0);
                            Assert.Equal(genesis.Progression.GetActor(player).CurrentHp, state.Progression!.GetActor(player).CurrentHp);
                            Assert.Empty(state.Care!.Ordinary!.Uses);
                            episode = next.EncounterActions!.Scope.EncounterId;
                            entries++;
                            break;
                        case AcknowledgeCareIntent intent:
                            Assert.Equal(player, command.ActorId);
                            Assert.Equal(1, defeats);
                            Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateAcknowledgment(state, player, intent));
                            Assert.NotNull(next.Care!.Pending);
                            Assert.Equal(0, next.Progression!.GetActor(player).CurrentHp);
                            Assert.Equal(state.Production!.ContentHash, next.Production!.ContentHash);
                            Assert.Equal(EncounterLifecyclePhase.Outcome, next.EncounterLifecycle!.Phase);
                            acknowledgments++;
                            break;
                        case AdministerRemedyIntent remedy:
                            Assert.Equal(1, acknowledgments);
                            ValidateTreatment(state, next, command, helper, player);
                            // These keep actual post-state/receipt identities but corrupt either the acting helper or dose.
                            Assert.Throws<InvalidDataException>(() => ValidateTreatment(state, next,
                                Copy(command, player, remedy), helper, player));
                            var wrongDose = new AdministerRemedyIntent(remedy.AcknowledgmentId, remedy.ExpectedCareRevision,
                                remedy.ExpectedPatientPresenceRevision, remedy.ExpectedPatientProgressionRevision,
                                remedy.ExpectedStorageRevision, remedy.ExpectedProductionRevision,
                                new IngredientRequest(remedy.Ingredient.Kind, player, remedy.Ingredient.ExpectedRevision, remedy.Ingredient.Quantity));
                            Assert.Throws<InvalidDataException>(() => ValidateTreatment(state, next,
                                Copy(command, command.ActorId, wrongDose), helper, player));
                            Assert.Throws<InvalidDataException>(() => ValidateTreatment(state, state, command, helper, player));
                            Assert.Equal(report.GetProperty("AcceptedCare").GetProperty("HealedHp").GetInt32(), next.Progression!.GetActor(player).CurrentHp);
                            Assert.Equal(episode, next.EncounterActions!.Scope.EncounterId);
                            Assert.Equal(EncounterLifecyclePhase.Active, next.EncounterLifecycle!.Phase);
                            Assert.Empty(next.EncounterOutcome!.Acknowledgments);
                            Assert.Equal(0UL, next.EncounterOutcome.FinalizedCount);
                            treatments++;
                            break;
                        case AcknowledgeEncounterOutcomeIntent:
                            Assert.Equal((2, 1, player), (defeats, treatments, command.ActorId));
                            Assert.Null(state.Care!.Pending);
                            readings++;
                            break;
                    }
                }
                if (next.EncounterOutcome!.FinalizedCount > state.EncounterOutcome!.FinalizedCount)
                {
                    Assert.Equal((2, 1, 1), (defeats, treatments, readings));
                    Assert.Empty(batch.Actions);
                    Assert.Equal(state.Progression!.ContentHash, next.Progression!.ContentHash);
                    Assert.Equal(state.Production!.ContentHash, next.Production!.ContentHash);
                    completions++;
                }
                state = next;
            }
            Assert.Equal((1, 2, 1, 1, 1, 1), (entries, defeats, acknowledgments, treatments, readings, completions));
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(state));
            Assert.Equal(1UL, final.EncounterOutcome!.FinalizedCount);
            Assert.Equal(EncounterLifecyclePhase.Exploration, final.EncounterLifecycle!.Phase);
            Assert.Equal(0, final.Progression!.GetActor(player).CurrentHp);
            Assert.Null(final.Care!.Pending);
            Assert.Single(final.Care.Treatments);
            Assert.Empty(final.Care.Ordinary!.Uses);
            AuthenticateSubmissions(report, outcomes, batches, playerControl.PrincipalId);
            using var reopened = JsonDocument.Parse(File.ReadAllText(manifest.RootElement.GetProperty("reopen").GetProperty("report").GetString()!));
            Assert.Empty(reopened.RootElement.GetProperty("Submissions").EnumerateArray());
            Assert.Equal(report.GetProperty("CompletedAtSequence").GetUInt64(), reopened.RootElement.GetProperty("CompletedAtSequence").GetUInt64());
            Assert.Equal(final.EncounterOutcome.Last!.WorldSequence, report.GetProperty("CompletedAtSequence").GetUInt64());
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    private static WorldActionCommand Copy(WorldActionCommand command, EntityId actor, WorldActionIntent intent) =>
        new(command.CommandId, command.PrincipalId, command.OriginSessionId, actor, command.WorldId, command.BranchId,
            command.ExpectedActorRevision, intent);

    private static void ValidateTreatment(WorldSnapshot before, WorldSnapshot after, WorldActionCommand command, EntityId helper, EntityId patient)
    {
        Check(command.ActorId == helper && command.Intent is AdministerRemedyIntent, "Treatment is not the helper's separate action.");
        var intent = (AdministerRemedyIntent)command.Intent;
        var control = before.Inventory.Controls.Single(value => value.ActorId == helper);
        Check(command.PrincipalId == control.PrincipalId && command.OriginSessionId == control.SessionId, "Wrong helper control.");
        Check(before.Care!.Pending is { } pending && intent.AcknowledgmentId == pending.AcknowledgmentId
            && before.Progression!.GetActor(patient).CurrentHp == 0, "Treatment lacks a real acknowledged defeat.");
        var treatment = after.Care!.Treatments.SingleOrDefault(value => value.CommandId == command.CommandId);
        Check(treatment is not null && treatment.WorldSequence > before.Care.Pending!.WorldSequence, "Acknowledgment alone is not treatment.");
        var claim = after.Production!.ConsumedItems.Single(value => value.CommandId == command.CommandId);
        Check(claim.ActorId == helper && claim.RecipientActorId == patient && claim.ConsumptionId == treatment!.ConsumptionId,
            "Treatment is not bound to the helper's actual patient consumption.");
        var ingredient = claim.Ingredient;
        Check(ingredient.Kind == IngredientLotKind.Source && ingredient.LotId == intent.Ingredient.LotId
            && ingredient.Revision == intent.Ingredient.ExpectedRevision && ingredient.Quantity == 1 && intent.Ingredient.Quantity == 1,
            "The committed source dose differs from the selected exact lot.");
        var lot = before.Inventory.Custody.Lots.Single(value => value.LotId == ingredient.LotId);
        Check(lot.CustodianId == before.GetResident(helper).InventoryCustodianId && lot.Quantity == 1
            && lot.Source == ingredient.Source && lot.Definition == ingredient.Definition, "Dose provenance does not match owned stock.");
        Check(!after.Inventory.Custody.Lots.Any(value => value.Source.SourceId == lot.Source.SourceId
            && value.Source.Start < lot.Source.End && lot.Source.Start < value.Source.End), "The retired source interval remains spendable.");
        Check(after.Progression!.GetActor(patient).CurrentHp == treatment!.HpAfter && treatment.HpAfter > 0
            && after.Care.Pending is null, "No separately committed physiological restoration.");
    }

    private static void AuthenticateSubmissions(JsonElement report, IReadOnlyList<StoredWorldCommandOutcome> outcomes,
        IReadOnlyDictionary<ulong, WorldStepBatch> batches, EntityId principal)
    {
        var captured = report.GetProperty("Submissions").EnumerateArray().ToArray();
        var selected = report.GetProperty("Selections").EnumerateArray().ToArray();
        var own = outcomes.Where(value => value.PrincipalId == principal).OrderBy(value => value.Sequence).ToArray();
        Assert.Equal(own.Length, captured.Length);
        Assert.Equal(own.Length, selected.Length);
        for (int i = 0; i < own.Length; i++)
        {
            var command = WorldActionCodec.Decode(own[i].Command);
            var row = captured[i];
            var selection = selected[i];
            Assert.False(row.GetProperty("IsRetry").GetBoolean());
            Assert.Equal(command.CommandId.ToString(), row.GetProperty("CommandId").GetString());
            Assert.Equal(command.CommandId.ToString(), selection.GetProperty("CommandId").GetString());
            Assert.Equal(Convert.ToHexString(own[i].Command), row.GetProperty("CommandBytes").GetString());
            Assert.Equal(Convert.ToHexString(own[i].Command), selection.GetProperty("ExpectedCommandBytes").GetString());
            Assert.Equal(Convert.ToHexString(own[i].Receipt), row.GetProperty("Receipt").GetProperty("Bytes").GetString());
            Assert.Equal(own[i].Sequence, row.GetProperty("Receipt").GetProperty("CommitSequence").GetUInt64());
            Assert.Equal(command.Intent.GetType().Name, row.GetProperty("IntentType").GetString());
            var scope = row.GetProperty("Scope");
            Assert.Equal(command.ActorId.ToString(), scope.GetProperty("ActorId").GetString());
            Assert.Equal(command.PrincipalId.ToString(), scope.GetProperty("PrincipalId").GetString());
            Assert.Equal(command.OriginSessionId.ToString(), scope.GetProperty("SessionId").GetString());
            Assert.Equal(command.WorldId.ToString(), scope.GetProperty("WorldId").GetString());
            Assert.Equal(command.BranchId.ToString(), scope.GetProperty("BranchId").GetString());
            Assert.Equal(command.ExpectedActorRevision, scope.GetProperty("ExpectedActorRevision").GetUInt64());
            Assert.Equal(own[i].Command, WorldActionCodec.Encode(Assert.Single(batches[own[i].Sequence].Actions,
                value => value.CommandId == command.CommandId)));
        }
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}

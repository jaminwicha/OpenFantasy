using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Recovery;

public sealed class RecoveryJournalTests
{
    [Fact]
    public void Another_encounter_and_real_archive_can_precede_a_finite_helper_recovery_and_exact_reopen()
    {
        using var files = new StorageFiles();
        var fixture = new RecoveryJournalFixture();
        WorldSnapshot genesis = fixture.Genesis, final;
        WorldActionCommand treatment;
        WorldActionReceipt receipt;
        ulong completedAt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var defeated = fixture.Defeat(authority);
            var body = defeated.Space.GetActor(fixture.Patient);
            var completed = fixture.DisconnectAndComplete(authority);
            completedAt = completed.Revision;
            var completedView = LocalEncounterOutcomeView.Project(completed, fixture.Patient);
            Assert.NotNull(completedView);
            Assert.True(completedView.AllowsAssistedRecovery);
            Assert.False(completedView.IsTerminal);
            Assert.Equal(defeated.Progression!.ContentHash, completed.Progression!.ContentHash);
            Assert.Equal(defeated.Inventory.ContentHash, completed.Inventory.ContentHash);
            Assert.Equal(1UL, completed.EncounterOutcome!.FinalizedCount);

            var archived = fixture.ArchiveBeforeRescue(authority);
            Assert.Equal(0, archived.Progression!.GetActor(fixture.Patient).CurrentHp);
            Assert.Equal(fixture.Permission.Version, archived.Care!.Ordinary!.GetPermission(fixture.Permission.PermissionId).Version);
            Assert.Equal(completed.Controllers!.ContentHash, archived.Controllers!.ContentHash);
            var later = archived.EncounterLifecycle!.GetScenario(fixture.LaterScenario);
            Assert.Equal((ushort)0, later.Combatants.Single(value => value.ActorId == fixture.Patient).Side);
            fixture.WalkTo(authority, fixture.Helper, fixture.Beside(archived, fixture.Helper, later.TargetActorId));
            var laterOutcome = fixture.EnterAndFight(authority, fixture.LaterScenario, patientWaits: false);
            Assert.Empty(WorldEncounterOutcomes.RequiredHumans(laterOutcome, laterOutcome.EncounterActions!));
            Assert.NotEqual(defeated.EncounterActions!.Scope.EncounterId, laterOutcome.EncounterActions!.Scope.EncounterId);
            Assert.Equal(0, laterOutcome.Progression!.GetActor(fixture.Patient).CurrentHp);
            Assert.Equal(0, laterOutcome.Progression.GetActor(later.TargetActorId).CurrentHp);
            var afterSecond = fixture.Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
            Assert.Equal(2UL, afterSecond.EncounterOutcome!.FinalizedCount);
            Assert.Equal(0, afterSecond.Progression!.GetActor(fixture.Patient).CurrentHp);
            Assert.Empty(afterSecond.Care!.Ordinary!.Uses);
            // The helper's real finishing attack creates debt. Completion does not erase it or grant immediate aid.
            Assert.True(afterSecond.EncounterLifecycle!.GetDebt(fixture.Helper).RemainingDelay > 0);
            var exhausted = WorldRestorativeActions.PrepareAvailability(afterSecond, fixture.Helper, fixture.Patient,
                fixture.Definition.DefinitionId, fixture.Permission.PermissionId);
            Assert.Equal(RestorativeAvailabilityReason.ActionDebt, exhausted.Reason);
            Assert.Null(exhausted.Intent);
            EntityId exhaustedBag = afterSecond.GetResident(fixture.Helper).InventoryCustodianId;
            var availableDose = afterSecond.Inventory.Custody.Lots.Single(value => value.CustodianId == exhaustedBag
                && value.Definition == afterSecond.Care.Remedies.Single().Item);
            var exhaustedIntent = new UseRestorativeItemIntent(fixture.Patient, fixture.Definition.DefinitionId,
                fixture.Definition.Version, fixture.Permission.PermissionId, fixture.Permission.GrantVersion,
                fixture.Permission.Revision, afterSecond.Space.GetActor(fixture.Patient).Revision,
                afterSecond.Progression.GetActor(fixture.Patient).Revision,
                afterSecond.Inventory.Custody.GetStorage(exhaustedBag).Revision, afterSecond.Production!.Revision,
                new(IngredientLotKind.Source, availableDose.LotId, availableDose.Revision, 1));
            var exhaustedCommand = fixture.Commands.Command(afterSecond, fixture.Helper, exhaustedIntent);
            var refused = authority.Commit(fixture.Commands.Batch(afterSecond, WorldBatchKind.AdvancingStep, [exhaustedCommand]));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
            Assert.Equal(afterSecond.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
            Assert.Equal(afterSecond.Progression.ContentHash, refused.Candidate.Progression!.ContentHash);
            Assert.Empty(refused.Candidate.Care!.Ordinary!.Uses);

            var helperBefore = afterSecond.Space.GetActor(fixture.Helper).Feet;
            fixture.WalkTo(authority, fixture.Helper, fixture.Beside(afterSecond, fixture.Helper, fixture.Patient));
            var ready = authority.ReadStateForHost();
            for (int step = 0; !WorldActorAvailability.CanAct(ready, fixture.Helper); step++)
            {
                Assert.True(step < 64);
                ready = fixture.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
            }
            Assert.NotEqual(helperBefore, ready.Space.GetActor(fixture.Helper).Feet);
            var supplied = fixture.CraftDose(authority);
            var suppliedDose = Assert.Single(supplied.Production!.Lots, value => value.Definition == supplied.Care!.Remedies.Single().Item);
            var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(supplied,
                fixture.Helper, fixture.Patient, fixture.Definition.DefinitionId, fixture.Permission.PermissionId));
            // Select this actual crafted dose explicitly; selection cannot substitute a remaining source potion.
            var intent = Copy(prepared, ingredient: new(IngredientLotKind.Transformed, suppliedDose.LotId, suppliedDose.Revision, 1));
            treatment = fixture.Commands.Command(supplied, fixture.Helper, intent);
            var decision = fixture.Commit(authority, WorldBatchKind.AdvancingStep, [treatment]);
            receipt = Assert.Single(decision.Receipts);
            final = decision.Candidate;
            var use = Assert.Single(final.Care!.Ordinary!.Uses);
            var consumed = Assert.Single(final.Production!.ConsumedItems);
            Assert.Equal(treatment.CommandId, use.CommandId);
            Assert.Equal((fixture.Helper, fixture.Patient, fixture.Permission.PermissionId),
                (use.ActorId, use.RecipientActorId, use.PermissionId!.Value));
            Assert.Equal((0, Math.Min((int)final.Care.Remedies.Single().RestoredHp, use.MaximumHp)), (use.HpBefore, use.HpAfter));
            Assert.Equal(use.HpAfter, final.Progression!.GetActor(fixture.Patient).CurrentHp);
            Assert.Equal(use.ConsumptionId, consumed.ConsumptionId);
            Assert.Equal(treatment.CommandId, consumed.CommandId);
            Assert.Equal(suppliedDose.LotId, consumed.Ingredient.LotId);
            Assert.Equal(IngredientLotKind.Transformed, consumed.Ingredient.Kind);
            Assert.Equal(1UL, consumed.Ingredient.Quantity);
            Assert.Equal(1_800UL, use.ExplorationMilliseconds);
            Assert.Equal(supplied.Calendar.AddMilliseconds(1_800), final.Calendar);
            Assert.Equal(supplied.AdvancingSteps + 1, final.AdvancingSteps);
            Assert.Equal(supplied.Progression!.GetActor(fixture.Patient).Revision + 1, use.PatientRevisionAfter);
            Assert.Equal(supplied.Progression.GetActor(fixture.Helper), final.Progression.GetActor(fixture.Helper));
            Assert.Equal(supplied.Inventory.Controls, final.Inventory.Controls);
            Assert.Equal(supplied.Inventory.Sources, final.Inventory.Sources);
            Assert.Equal(supplied.Inventory.Custody.Lots, final.Inventory.Custody.Lots);
            EntityId helperBag = supplied.GetResident(fixture.Helper).InventoryCustodianId;
            Assert.Equal(supplied.Inventory.Custody.GetStorage(helperBag).Revision + 1,
                final.Inventory.Custody.GetStorage(helperBag).Revision);
            Assert.Equal(supplied.EncounterLifecycle!.AdvanceExploration().Debts, final.EncounterLifecycle!.Debts);
            Assert.Equal(supplied.Space.GetActor(fixture.Helper).Feet, final.Space.GetActor(fixture.Helper).Feet);
            Assert.Equal(body.Binding, final.Space.GetActor(fixture.Patient).Binding);
            Assert.Equal(body.Condition, final.Space.GetActor(fixture.Patient).Condition);
            Assert.Equal(body.Feet, final.Space.GetActor(fixture.Patient).Feet);
            Assert.Equal(supplied.Controllers!.ContentHash, final.Controllers!.ContentHash);
            Assert.Equal(genesis.Inventory.Controls, final.Inventory.Controls);
            Assert.Null(final.Care.Pending);
            Assert.Empty(final.Care.Treatments);
            Assert.NotNull(final.Coordination!.Anchor);
            Assert.True(authority.IsIdentityOccupiedForHost(treatment.CommandId));
            Assert.Equal(WorldActionCodec.Encode(treatment), WorldActionCodec.Encode(WorldActionCodec.Decode(WorldActionCodec.Encode(treatment))));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        var recorded = reopened.ReadRecordedStateForHost(completedAt).State;
        Assert.Equal(0, recorded.Progression!.GetActor(fixture.Patient).CurrentHp);
        Assert.Equal(EncounterLifecyclePhase.Exploration, recorded.EncounterLifecycle!.Phase);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            reopened.FindReceipt(treatment, ConstructionUpgradeFixture.Delivery(final, fixture.Helper)))));
        Assert.NotNull(reopened.ReadMaterialCareArchiveForHost(0).Segment.Continued.Recovery);
    }

    [Theory]
    [InlineData("wrong-permission", WorldActionRejection.NotAuthorized)]
    [InlineData("revoked-permission", WorldActionRejection.NotAuthorized)]
    [InlineData("stale-resource", WorldActionRejection.RevisionConflict)]
    [InlineData("stale-dose", WorldActionRejection.Unavailable)]
    [InlineData("out-of-reach", WorldActionRejection.OutOfReach)]
    [InlineData("unsupported-form", WorldActionRejection.Unavailable)]
    public void Refused_recovery_keeps_the_incapacitated_patient_and_finite_dose(string contrast, WorldActionRejection expected)
    {
        using var files = new StorageFiles();
        var fixture = new RecoveryJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        if (contrast == "revoked-permission")
            fixture.Act(authority, fixture.Patient, new RevokeCarePermissionIntent(fixture.Permission.PermissionId,
                fixture.Permission.Revision, fixture.Permission.GrantVersion));
        fixture.Defeat(authority);
        fixture.DisconnectAndComplete(authority);
        var state = authority.ReadStateForHost();
        if (contrast != "out-of-reach") fixture.WalkTo(authority, fixture.Helper, fixture.Beside(state, fixture.Helper, fixture.Patient));
        state = authority.ReadStateForHost();
        for (int step = 0; !WorldActorAvailability.CanAct(state, fixture.Helper); step++)
        {
            Assert.True(step < 64);
            state = fixture.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        EntityId storage = state.GetResident(fixture.Helper).InventoryCustodianId;
        var dose = state.Inventory.Custody.Lots.First(value => value.CustodianId == storage && value.Definition == state.Care!.Remedies.Single().Item);
        var permission = contrast == "unsupported-form" ? fixture.UnsupportedPermission : contrast == "wrong-permission"
            ? state.Care!.Ordinary!.Permissions.First(value => value.PermissionId != fixture.Permission.PermissionId)
            : state.Care!.Ordinary!.GetPermission(fixture.Permission.PermissionId);
        var definition = contrast == "unsupported-form" ? fixture.UnsupportedDefinition : fixture.Definition;
        var intent = new UseRestorativeItemIntent(fixture.Patient, definition.DefinitionId, definition.Version,
            permission.PermissionId, permission.GrantVersion, permission.Revision, state.Space.GetActor(fixture.Patient).Revision,
            state.Progression!.GetActor(fixture.Patient).Revision + (contrast == "stale-resource" ? 1UL : 0UL),
            state.Inventory.Custody.GetStorage(storage).Revision, state.Production!.Revision,
            new(IngredientLotKind.Source, dose.LotId, dose.Revision + (contrast == "stale-dose" ? 1UL : 0UL), 1));
        Assert.Equal(expected, WorldRestorativeActions.Assess(state, fixture.Helper, intent).Rejection);
        if (contrast == "unsupported-form")
            Assert.Equal(RestorativeAvailabilityReason.UnsupportedForm, WorldRestorativeActions.Assess(state, fixture.Helper, intent).Reason);
        var command = fixture.Commands.Command(state, fixture.Helper, intent);
        var decision = authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.AdvancingStep, [command]));
        Assert.Equal(expected, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(state.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Equal(state.Production.ContentHash, decision.Candidate.Production!.ContentHash);
        Assert.Equal(state.Progression.ContentHash, decision.Candidate.Progression!.ContentHash);
        Assert.Equal(0, decision.Candidate.Progression.GetActor(fixture.Patient).CurrentHp);
        Assert.Empty(decision.Candidate.Care!.Ordinary!.Uses);
        Assert.Equal(state.Calendar.AddMilliseconds(1_800), decision.Candidate.Calendar);
        Assert.NotNull(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(state, fixture.Helper)));
    }

    [Fact]
    public void Legacy_world_is_not_reinterpreted_as_recovery_and_a_lived_world_cannot_be_freshly_admitted()
    {
        var fixture = new RecoveryJournalFixture();
        byte[] legacy = WorldSnapshotCodec.Encode(fixture.Legacy);
        Assert.Equal((ushort)36, fixture.Legacy.ProfileVersion);
        Assert.Null(fixture.Legacy.Recovery);
        Assert.DoesNotContain(fixture.Legacy.Care!.Ordinary!.Definitions,
            value => value.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance);
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV36.Decode(WorldSnapshotCodec.Encode(fixture.Genesis)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV37.Decode(legacy));
        Assert.Equal(legacy, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(legacy)));
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Legacy);
        var lived = fixture.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        Assert.Throws<ArgumentException>(() => WorldRecoveryAdmission.CreateFresh(lived, [fixture.Definition], [fixture.Permission]));
    }

    private static UseRestorativeItemIntent Copy(UseRestorativeItemIntent value, IngredientRequest ingredient) =>
        new(value.RecipientActorId, value.UseDefinitionId, value.UseDefinitionVersion, value.PermissionId,
            value.PermissionGrantVersion, value.ExpectedPermissionRevision, value.ExpectedPatientPresenceRevision,
            value.ExpectedPatientProgressionRevision, value.ExpectedStorageRevision, value.ExpectedProductionRevision, ingredient);
}

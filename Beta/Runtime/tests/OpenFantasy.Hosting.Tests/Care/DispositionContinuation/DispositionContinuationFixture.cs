using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.DispositionContinuation;

/// <summary>A fresh, finite calibration earns archive pressure through 255 actual one-HP bites once.</summary>
public sealed class DispositionContinuationFixture : IDisposable
{
    private readonly StorageFiles _pending = new();

    public DispositionContinuationFixture() : this(humanHelper: false) { }

    /// <summary>Human control is admitted only in the original fresh calibration, before any earned history.</summary>
    internal DispositionContinuationFixture(bool humanHelper)
    {
        Journey = new RecoveryJournalFixture();
        Genesis = CreateGenesis(Journey, humanHelper);
        using var authority = SqliteWorldAuthority.CreateNew(_pending.Path, Genesis);
        // The ordinary-care genesis splits its two source doses between patient and helper. Earn a
        // second helper dose now so a later request can lawfully be acknowledged after the first use.
        WorldSnapshot state = Journey.CraftDose(authority);
        EntityId helperStorage = state.GetResident(Journey.Helper).InventoryCustodianId;
        var remedyItem = Assert.Single(state.Care!.Remedies).Item;
        Assert.Equal(1UL, state.Inventory.Custody.Lots.Single(value => value.CustodianId == helperStorage
            && value.Definition == remedyItem).Quantity);
        Assert.Equal(1UL, Assert.Single(state.Production!.Lots, value => value.CustodianId == helperStorage
            && value.Definition == remedyItem).Quantity);
        Assert.Equal(1UL, state.Production.TotalBatchCount);
        var scenario = state.EncounterLifecycle!.GetScenario(Journey.FirstScenario);
        Journey.WalkTo(authority, Journey.Patient, Journey.Beside(state, Journey.Patient, scenario.TargetActorId));
        state = authority.ReadStateForHost();
        Journey.WalkTo(authority, Journey.Helper, Journey.Beside(state, Journey.Helper, Journey.Patient));
        state = authority.ReadStateForHost();
        Journey.Commit(authority, WorldBatchKind.EncounterLifecycle, [Journey.Commands.Enter(state, Journey.FirstScenario)]);
        for (int stage = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; stage++)
        {
            Assert.True(stage < 3);
            Journey.Commit(authority, WorldBatchKind.EncounterLifecycle);
        }
        state = FightToDefeat(authority);
        Assert.Equal(255, state.ResourceHistory!.History.Batches.Count);
        Assert.Equal(255UL, state.Progression!.Revision);
        Assert.All(state.ResourceHistory.Evidence, evidence =>
        {
            var attack = Assert.IsType<WorldEncounterResourceEvidence>(evidence);
            Assert.Equal(Journey.Patient, attack.Resolution.TargetActorId);
            Assert.Equal(1U, attack.Resolution.Damage);
        });
        Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        var request = Acknowledge(authority);
        RequestCommand = request.Command;
        RequestReceipt = request.Receipt;
        Pending = authority.ReadStateForHost();
        Request = Assert.IsType<CareAcknowledgment>(Pending.Care!.Pending);
        Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
    }

    internal RecoveryJournalFixture Journey { get; }
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Pending { get; }
    internal CareAcknowledgment Request { get; }
    internal WorldActionCommand RequestCommand { get; }
    internal WorldActionReceipt RequestReceipt { get; }

    internal StorageFiles CopyPending()
    {
        var files = new StorageFiles();
        File.Copy(_pending.Path, files.Path);
        return files;
    }

    internal (WorldActionCommand Command, WorldActionReceipt Receipt) Acknowledge(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        var agreement = Assert.Single(state.Care!.Agreements);
        var intent = new AcknowledgeCareIntent(agreement.AgreementId, state.EncounterActions!.Scope.EncounterId,
            state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash);
        Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateAcknowledgment(state, Journey.Patient, intent));
        var command = Journey.Commands.Command(state, Journey.Patient, intent);
        var decision = Journey.Commit(authority, WorldBatchKind.Care, [command]);
        return (command, Assert.Single(decision.Receipts));
    }

    internal WorldStepDecision CutPendingWithRefusedRemedy(SqliteWorldAuthority authority,
        out WorldActionCommand command)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        var intent = Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(state));
        var prepared = Journey.Commands.Command(state, Journey.Helper, intent);
        command = new WorldActionCommand(prepared.CommandId, prepared.PrincipalId, prepared.OriginSessionId,
            prepared.ActorId, prepared.WorldId, prepared.BranchId, prepared.ExpectedActorRevision + 1, prepared.Intent);
        var result = authority.Commit(Journey.Commands.Batch(state, WorldBatchKind.Care, [command]));
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(result.Receipts).Rejection);
        var archive = Assert.IsType<WorldMaterialCareArchiveSegment>(result.SealedMaterialCareSegment);
        Assert.Equal(WorldSnapshotCodec.Encode(state), archive.Encode());
        Assert.Equal(state.Revision, archive.Reference.BoundarySequence);
        Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.Equivalent(state.Care!.Pending, result.Candidate.Care!.Pending, strict: true);
        Assert.Equal(state.Care.Revision, result.Candidate.Care.Revision);
        Assert.Equal(state.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(state.Progression!.ContentHash, result.Candidate.Progression!.ContentHash);
        Assert.Equal(state.Production!.TotalConsumedItemCount, result.Candidate.Production!.TotalConsumedItemCount);
        Assert.Equal(state.Production.TotalBatchCount, result.Candidate.Production.TotalBatchCount);
        Assert.Equal(state.Pacing!.ContentHash, result.Candidate.Pacing!.ContentHash);
        Assert.Equal(state.Calendar, result.Candidate.Calendar);
        Assert.Equal(state.AdvancingSteps, result.Candidate.AdvancingSteps);
        Assert.Equal(state.EncounterLifecycle!.Debts, result.Candidate.EncounterLifecycle!.Debts);
        Assert.Empty(result.Candidate.ResourceHistory!.History.Batches);
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.True(authority.IsIdentityOccupiedForHost(Assert.Single(result.Receipts).ResultId));
        return result;
    }

    internal WorldSnapshot FightToDefeat(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        var automatic = new LocalEncounterController();
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1_600, "The calibrated 255-bite journey retains a finite 1,600-boundary limit.");
            var choices = automatic.SelectChoices(state, Journey.Commands.NextId).ToList();
            if (WorldEncounterActions.HasRequiredDecisions(state)) choices.Add(Journey.Commands.Wait(state, Journey.Patient));
            WorldBatchKind kind = choices.Count != 0 ? WorldBatchKind.EncounterDecision
                : WorldEncounterActions.DueActors(state).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            state = Journey.Commit(authority, kind, choices).Candidate;
        }
        Assert.Equal(0, state.Progression!.GetActor(Journey.Patient).CurrentHp);
        return state;
    }

    internal WorldSnapshot CompleteAndRecut(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        Journey.Act(authority, Journey.Patient, new AcknowledgeEncounterOutcomeIntent(
            state.EncounterActions!.Scope.EncounterId, state.EncounterActions.ContentHash), WorldBatchKind.EncounterLifecycle);
        state = Journey.Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
        ulong count = authority.InspectForHost().Journal.MaterialCareArchiveCount;
        state = Journey.ArchiveBeforeRescue(authority);
        Assert.Equal(count + 1, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        return state;
    }

    /// <summary>Pure admission builder, also used to authenticate exported native fixtures without replaying their preparation.</summary>
    internal static WorldSnapshot CreateGenesis(RecoveryJournalFixture journey, bool humanHelper)
    {
        WorldSnapshot basis = journey.Legacy;
        var profiles = basis.Progression!.Actors.Select((actor, index) => new ActorStatProfileDefinition(
            new EntityId(0xcade380000000000, (ulong)(100 + index)), "disposition_archive_actor_" + index,
            "Disposition archive actor " + index, ActorStatProfileKind.Class,
            new ActorStatValues(actor.ActorId == journey.Patient ? 255 : 100_000, 4096, 1, 1, 100, 10_000),
            ActorStatModifiers.Zero, ActorStatModifiers.Zero)).ToArray();
        var actors = basis.Progression.Actors.Select((actor, index) => new ActorProgressionState(actor.ActorId, 0,
            profiles[index], actor.Curve, 1, 0, profiles[index].BaseStats.MaxHp, profiles[index].BaseStats.MaxMp));
        var progression = new ProgressionSnapshot(basis.WorldId, basis.BranchId, 0, profiles,
            basis.Progression.Curves, actors, basis.Progression.Currency);
        ContentHash admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(progression.ContentHash.ToByteArray()).ToArray());
        // Only this unplayed calibration changes statistics. Every wound, request and archive is then journaled.
        var retreat = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(progression, 0, []), []),
            coordination: basis.Coordination, retreat: basis.Retreat);
        var outcome = humanHelper
            ? WorldEncounterOutcomeAdmission.CreateCalibratedFresh(retreat,
                new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [journey.Helper]))
            : WorldEncounterOutcomeAdmission.CreateFresh(retreat);
        var controllers = WorldControllerAdmission.CreateFresh(outcome);
        var definitions = journey.Genesis.Care!.Ordinary!.Definitions
            .Where(value => value.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance).ToArray();
        var permissions = journey.Genesis.Care.Ordinary.Permissions
            .Where(value => definitions.Any(definition => definition.DefinitionId == value.UseDefinitionId));
        return WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, definitions, permissions));
    }

    public void Dispose() => _pending.Dispose();
}

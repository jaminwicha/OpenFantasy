using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.DispositionCapacity;

/// <summary>
/// An explicit fresh one-HP patient and 129 finite helper doses make repeated lawful dispositions
/// bounded. Every defeat, request, closure and recovery after admission is an actual command.
/// </summary>
internal sealed class DispositionPressureJourney
{
    private readonly RecoveryJournalFixture _journey = new();

    internal DispositionPressureJourney()
    {
        Genesis = CreateGenesis();
    }

    internal WorldSnapshot Genesis { get; }
    internal EntityId Patient => _journey.Patient;
    internal EntityId Helper => _journey.Helper;
    internal EntityId ExtraDoseLot => Id(2);
    internal WorldActionCommand? FirstRequest { get; private set; }
    internal WorldActionCommand? FirstDisposition { get; private set; }
    internal WorldActionReceipt? FirstRequestReceipt { get; private set; }
    internal WorldActionReceipt? FirstDispositionReceipt { get; private set; }
    internal CareAcknowledgment? LatestRequest { get; private set; }
    internal CareDisposition? LatestDisposition { get; private set; }

    internal void Approach(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.GetScenario(_journey.FirstScenario);
        _journey.WalkTo(authority, Patient, _journey.Beside(state, Patient, scenario.TargetActorId));
        state = authority.ReadStateForHost();
        _journey.WalkTo(authority, Helper, _journey.Beside(state, Helper, Patient));
    }

    internal WorldSnapshot DefeatAndClose(SqliteWorldAuthority authority, bool decline)
    {
        var state = WaitForHelper(authority);
        Assert.Equal(1, state.Progression!.GetActor(Patient).CurrentHp);
        for (int wait = 0; !WorldActorAvailability.CanAct(state, Patient)
            || !WorldActorAvailability.CanAct(state, state.EncounterLifecycle!.GetScenario(_journey.FirstScenario).TargetActorId); wait++)
        {
            Assert.True(wait < 64);
            state = _journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        _journey.Commit(authority, WorldBatchKind.EncounterLifecycle, [_journey.Commands.Enter(state, _journey.FirstScenario)]);
        for (int stage = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; stage++)
        {
            Assert.True(stage < 3);
            _journey.Commit(authority, WorldBatchKind.EncounterLifecycle);
        }
        state = authority.ReadStateForHost();
        var automatic = new LocalEncounterController();
        ulong priorResourceRevision = state.Progression!.Revision;
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 32, "The one-HP patient must suffer one real lethal bite within 32 encounter boundaries.");
            var choices = automatic.SelectChoices(state, _journey.Commands.NextId).ToList();
            if (WorldEncounterActions.HasRequiredDecisions(state)) choices.Add(_journey.Commands.Wait(state, Patient));
            WorldBatchKind kind = choices.Count != 0 ? WorldBatchKind.EncounterDecision
                : WorldEncounterActions.DueActors(state).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            state = _journey.Commit(authority, kind, choices).Candidate;
        }
        Assert.Equal(priorResourceRevision + 1, state.Progression!.Revision);
        Assert.Equal(0, state.Progression.GetActor(Patient).CurrentHp);
        Assert.Equal(1U, state.EncounterActions!.LastResolution!.Damage);
        var agreement = Assert.Single(state.Care!.Agreements);
        var request = _journey.Commands.Command(state, Patient, new AcknowledgeCareIntent(agreement.AgreementId,
            state.EncounterActions.Scope.EncounterId, state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash));
        var requested = _journey.Commit(authority, WorldBatchKind.Care, [request]);
        state = requested.Candidate;
        LatestRequest = Assert.IsType<CareAcknowledgment>(state.Care!.Pending);
        PendingCareDispositionIntent intent = decline
            ? new DeclinePendingCareIntent(LatestRequest.AcknowledgmentId, LatestRequest.EncounterId, state.Care.Revision)
            : new CancelPendingCareIntent(LatestRequest.AcknowledgmentId, LatestRequest.EncounterId, state.Care.Revision);
        var command = _journey.Commands.Command(state, decline ? Helper : Patient, intent);
        var decision = _journey.Commit(authority, WorldBatchKind.Care, [command]);
        LatestDisposition = Assert.Single(decision.Candidate.Care!.Dispositions,
            value => value.CommandId == command.CommandId);
        Assert.Equal(state.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Equal(state.Progression!.ContentHash, decision.Candidate.Progression!.ContentHash);
        Assert.Equal(state.Production!.ContentHash, decision.Candidate.Production!.ContentHash);
        if (FirstRequest is null)
        {
            FirstRequest = request;
            FirstDisposition = command;
            FirstRequestReceipt = Assert.Single(requested.Receipts);
            FirstDispositionReceipt = Assert.Single(decision.Receipts);
        }
        state = decision.Candidate;
        _journey.Act(authority, Patient, new AcknowledgeEncounterOutcomeIntent(state.EncounterActions!.Scope.EncounterId,
            state.EncounterActions.ContentHash), WorldBatchKind.EncounterLifecycle);
        state = _journey.Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
        return state;
    }

    internal WorldStepBatch PrepareRecovery(SqliteWorldAuthority authority)
    {
        var state = WaitForHelper(authority);
        var intent = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state, Helper, Patient,
            _journey.Definition.DefinitionId, _journey.Permission.PermissionId));
        return _journey.Commands.Batch(state, WorldBatchKind.AdvancingStep, [_journey.Commands.Command(state, Helper, intent)]);
    }

    internal ulong HelperDoses(WorldSnapshot state)
    {
        EntityId bag = state.GetResident(Helper).InventoryCustodianId;
        var item = Assert.Single(state.Care!.Remedies).Item;
        return state.Inventory.Custody.Lots.Where(value => value.CustodianId == bag && value.Definition == item)
            .Aggregate(0UL, (total, lot) => checked(total + lot.Quantity));
    }

    private WorldSnapshot WaitForHelper(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        for (int wait = 0; !WorldActorAvailability.CanAct(state, Helper); wait++)
        {
            Assert.True(wait < 64);
            state = _journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        return state;
    }

    private WorldSnapshot CreateGenesis()
    {
        var basis = RestorativeJourneyAdmission.CreateEmbedded("general-recovery-journal", "Traveler");
        EntityId bag = basis.GetResident(Helper).InventoryCustodianId;
        var potion = Assert.Single(basis.Care!.Remedies).Item;
        const ulong additionalDoses = 128;
        var inventory = new CountedAuthorityState(new CustodySnapshot(basis.WorldId, basis.BranchId, 0,
            basis.Inventory.Custody.Storages.Select(value => value.CustodianId == bag
                ? new CustodianStorage(bag, checked(value.UnitCapacity + additionalDoses), 0) : value),
            basis.Inventory.Custody.Lots.Append(new CountedSourceLot(ExtraDoseLot, bag, potion,
                new SourceInterval(Id(1), 0, additionalDoses), 0))),
            checked(basis.Inventory.AdmittedUnitCapacity + additionalDoses), basis.Inventory.Actors, basis.Inventory.Controls,
            basis.Inventory.Policies, basis.Inventory.Sources.Append(new AdmittedCountedSource(Id(1), potion, additionalDoses)),
            basis.Inventory.ReservationIds);
        var profiles = basis.Progression!.Actors.Select((actor, index) => new ActorStatProfileDefinition(Id((ulong)(100 + index)),
            "disposition_pressure_actor_" + index, "Disposition pressure actor " + index, ActorStatProfileKind.Class,
            new ActorStatValues(actor.ActorId == Patient ? 1 : 100_000, 4096, 1, 1, 100, 10_000),
            ActorStatModifiers.Zero, ActorStatModifiers.Zero)).ToArray();
        var actors = basis.Progression.Actors.Select((actor, index) => new ActorProgressionState(actor.ActorId, 0,
            profiles[index], actor.Curve, 1, 0, profiles[index].BaseStats.MaxHp, profiles[index].BaseStats.MaxMp));
        var progression = new ProgressionSnapshot(basis.WorldId, basis.BranchId, 0, profiles, basis.Progression.Curves,
            actors, basis.Progression.Currency);
        var admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(inventory.ContentHash.ToByteArray())
            .Concat(progression.ContentHash.ToByteArray()).ToArray());
        // Fresh added sources require the same dry-domain re-preparation used by cargo admission;
        // the water quantities, closed budget, geometry and terrain remain their admitted values.
        var (reservoirs, cells) = CargoWaterRebinding.Rebind(basis, inventory, basis.Space);
        Assert.Equal(inventory.ContentHash, reservoirs.Capacity.Inventory.ContentHash);
        Assert.Equal(basis.Reservoirs!.Capacity.Closed, reservoirs.Capacity.Closed);
        Assert.Empty(reservoirs.Capacity.Pending);
        Assert.Equal(basis.LiquidCells!.Portions, cells.Portions);
        var ordinary = new WorldSnapshot(inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, progression, basis.Quests, basis.Construction,
            reservoirs: reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: cells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(progression, 0, []), []));
        var retreat = RoadThreatWorldAdmission.Compose(ordinary, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded());
        var controllers = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateFresh(retreat));
        var recovery = WorldRecoveryAdmission.CreateFresh(controllers, [_journey.Definition], [_journey.Permission]);
        return WorldCareDispositionAdmission.CreateFresh(recovery);
    }

    private static EntityId Id(ulong value) => new(0xcac1380000000000, value);
}

using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Recovery;

/// <summary>Declared fresh calibration; every later wound, move, cut and dose is an ordinary journal operation.</summary>
internal sealed class RecoveryJournalFixture(bool humanObserver = false)
{
    private static readonly Lazy<(WorldSnapshot Legacy, WorldSnapshot Recovery)> Worlds = new(() => Create(false));
    private static readonly Lazy<(WorldSnapshot Legacy, WorldSnapshot Recovery)> ObserverWorlds = new(() => Create(true));
    internal readonly EncounterJourneyWorldFixture Commands = new();
    internal WorldSnapshot Genesis => (humanObserver ? ObserverWorlds : Worlds).Value.Recovery;
    internal WorldSnapshot Legacy => (humanObserver ? ObserverWorlds : Worlds).Value.Legacy;
    internal EntityId Patient => Genesis.EncounterLifecycle!.GetScenario(FirstScenario).InitiatingActorId;
    internal EntityId Observer => Genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
    internal EntityId Helper => Genesis.Care!.Agreements.Single().HelperActorId;
    internal EntityId FirstScenario => Genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).DefinitionId;
    internal EntityId LaterScenario => Id(1);
    internal RestorativeUseDefinition Definition => Genesis.Care!.Ordinary!.GetDefinition(Id(2));
    internal CarePermission Permission => Genesis.Care!.Ordinary!.GetPermission(Id(3));
    internal RestorativeUseDefinition UnsupportedDefinition => Genesis.Care!.Ordinary!.GetDefinition(Id(4));
    internal CarePermission UnsupportedPermission => Genesis.Care!.Ordinary!.GetPermission(Id(5));
    internal static EntityId Id(ulong value) => new(0xea77000000000000, value);

    private static (WorldSnapshot, WorldSnapshot) Create(bool humanObserver)
    {
        var basis = RoadThreatWorldAdmission.CreateEmbedded("general-recovery-journal", "Traveler");
        EntityId patient = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId helper = basis.Care!.Agreements.Single().HelperActorId;
        var lifecycle = basis.EncounterLifecycle!;
        var second = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey);
        var patientGrants = second.Combatants.Single(value => value.ActorId == patient).AbilityIds;
        var later = new EncounterScenarioDefinition(Id(1), "recovery-helper-encounter", "Helper's later encounter",
            helper, second.TargetActorId, second.EntryRangeSubunits, second.Combatants.Select(value =>
                value.ActorId == helper ? new EncounterCombatant(helper, 1, patientGrants)
                : value.ActorId == second.TargetActorId ? new EncounterCombatant(value.ActorId, 2, value.AbilityIds)
                : new EncounterCombatant(value.ActorId, 0, [])));
        var catalog = EncounterLifecycleSnapshot.BeginFresh(basis.WorldId, basis.BranchId, lifecycle.Seed,
            lifecycle.Abilities, lifecycle.Scenarios.Append(later), basis.Residents.Select(value => value.ActorId), lifecycle.CatalogVersion);
        // Fixed-width source and catalog hashes make this fresh calibration's concatenation unambiguous.
        var admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(catalog.ContentHash.ToByteArray()).ToArray());
        var authored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: catalog, care: basis.Care,
            resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(basis.Progression!, 0, []), []),
            coordination: basis.Coordination, retreat: basis.Retreat);
        // Small positive wounds belong to the fresh fixture's admission; no living state is replaced later.
        var outcome = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(authored,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [patient] = 1, [second.TargetActorId] = 1 },
                humanObserver ? [second.TargetActorId] : []));
        var legacy = WorldControllerAdmission.CreateFresh(outcome);
        var living = legacy.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var definition = RestorativeUseDefinition.CreateIncapacitatedAssistance(Id(2), living.RemedyDefinitionId,
            living.RemedyVersion, living.SupportedForms);
        var permission = new CarePermission(Id(3), patient, helper, definition.DefinitionId, definition.Version);
        var wolfForm = legacy.Space.GetActor(second.TargetActorId).Binding;
        var unsupported = RestorativeUseDefinition.CreateIncapacitatedAssistance(Id(4), living.RemedyDefinitionId,
            living.RemedyVersion, [new(wolfForm.DefinitionId, wolfForm.DefinitionVersion)]);
        var unsupportedPermission = new CarePermission(Id(5), patient, helper, unsupported.DefinitionId, unsupported.Version);
        return (legacy, WorldRecoveryAdmission.CreateFresh(legacy, [definition, unsupported], [permission, unsupportedPermission]));
    }

    internal WorldStepDecision Commit(SqliteWorldAuthority authority, WorldBatchKind kind,
        IEnumerable<WorldActionCommand>? actions = null)
    {
        var state = authority.ReadStateForHost();
        var result = authority.Commit(Commands.Batch(state, kind, actions));
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        return result;
    }

    internal WorldSnapshot Act(SqliteWorldAuthority authority, EntityId actor, WorldActionIntent intent,
        WorldBatchKind kind = WorldBatchKind.AdvancingStep) =>
        Commit(authority, kind, [Commands.Command(authority.ReadStateForHost(), actor, intent)]).Candidate;

    internal WorldSnapshot WalkTo(SqliteWorldAuthority authority, EntityId actor, SubunitPosition goal)
    {
        for (int step = 0; step < 512; step++)
        {
            var state = authority.ReadStateForHost();
            var feet = state.Space.GetActor(actor).Feet;
            if (feet == goal) return state;
            var route = state.Space.QueryGroundRoute(actor, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            Assert.NotNull(route.NextWaypoint);
            var next = route.NextWaypoint!.Value;
            long dx = Math.Clamp(next.X - feet.X, -WorldMovementInput.DisplacementPerStep, WorldMovementInput.DisplacementPerStep);
            long dy = dx == 0 ? Math.Clamp(next.Y - feet.Y, -WorldMovementInput.DisplacementPerStep, WorldMovementInput.DisplacementPerStep) : 0;
            var movement = new WorldMovementInput(ConstructionUpgradeFixture.Delivery(state, actor), actor, dx, dy);
            var batch = new WorldStepBatch(Commands.NextId(), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, WorldBatchKind.AdvancingStep, [movement], [], state.ProfileVersion);
            var moved = authority.Commit(batch).Candidate;
            // Readiness can retain a movement sample; its ordinary advancing boundary must pay that debt.
            Assert.True(moved.Space.GetActor(actor).Feet != feet || state.EncounterLifecycle!.GetDebt(actor).RemainingDelay > 0);
        }
        throw new InvalidOperationException("The actual helper/patient route exceeded 512 movement boundaries.");
    }

    internal SubunitPosition Beside(WorldSnapshot state, EntityId moving, EntityId target, bool farFromPatient = false)
    {
        var at = state.Space.GetActor(target).Feet;
        var patient = state.Space.GetActor(Patient).Feet;
        return new[] { at.Offset(256, 0, 0), at.Offset(-256, 0, 0), at.Offset(0, 256, 0), at.Offset(0, -256, 0) }
            .Where(goal => state.Space.QueryGroundRoute(moving, goal).Status == GroundRouteStatus.Reachable)
            .OrderByDescending(goal => farFromPatient ? Math.Abs(goal.X - patient.X) + Math.Abs(goal.Y - patient.Y) : 0)
            .First();
    }

    internal WorldSnapshot Defeat(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var later = state.EncounterLifecycle!.GetScenario(LaterScenario);
        // Real travel removes bedside aftermath care, so completion cannot silently spend a dose.
        WalkTo(authority, Helper, Beside(state, Helper, later.TargetActorId, farFromPatient: true));
        state = authority.ReadStateForHost();
        var first = state.EncounterLifecycle!.GetScenario(FirstScenario);
        WalkTo(authority, Patient, Beside(state, Patient, first.TargetActorId));
        state = EnterAndFight(authority, FirstScenario, patientWaits: true);
        Assert.Equal(0, state.Progression!.GetActor(Patient).CurrentHp);
        Assert.Equal(Patient, state.EncounterActions!.LastResolution!.TargetActorId);
        Assert.Equal(0U, state.EncounterActions.LastResolution.TargetHpAfter);
        Assert.NotEqual(SpatialInteraction.Reachable, state.Space.QueryInteraction(Helper, Patient,
            checked((long)state.Care!.Remedies.Single().RangeSubunits)));
        return state;
    }

    internal WorldSnapshot EnterAndFight(SqliteWorldAuthority authority, EntityId scenarioId, bool patientWaits)
    {
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.GetScenario(scenarioId);
        for (int step = 0; scenario.Combatants.Where(value => value.Side != 0)
            .Any(value => !WorldActorAvailability.CanCooperate(state, value.ActorId)); step++)
        {
            Assert.True(step < 64);
            state = Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        state = Commit(authority, WorldBatchKind.EncounterLifecycle, [Commands.Enter(state, scenarioId)]).Candidate;
        for (int stage = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; stage++)
        {
            Assert.True(stage < 3);
            state = Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
        }
        var automatic = new LocalEncounterController();
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 800, "Actual combat must finish within its declared boundary budget.");
            var choices = automatic.SelectChoices(state, Commands.NextId).ToList();
            if (patientWaits && WorldEncounterActions.HasRequiredDecisions(state)) choices.Add(Commands.Wait(state, Patient));
            WorldBatchKind kind = choices.Count != 0 ? WorldBatchKind.EncounterDecision
                : WorldEncounterActions.DueActors(state).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            state = Commit(authority, kind, choices).Candidate;
        }
        return state;
    }

    internal WorldSnapshot DisconnectAndComplete(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var before = state;
        void Event(WorldControllerEvent input) => state = authority.Commit(new WorldStepBatch(Commands.NextId(),
            state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.ControllerLifecycle,
            [], [], state.ProfileVersion, input)).Candidate;
        Event(new(Patient, WorldControllerEventKind.Disconnected, state.Controllers!.Get(Patient).Revision,
            reason: WorldControllerDisconnectReason.TransportLost));
        Event(new(Patient, WorldControllerEventKind.AllowanceExpired, state.Controllers!.Get(Patient).Revision,
            state.Controllers.Get(Patient).DisconnectId, WorldControllerState.ReconnectAllowanceMilliseconds));
        Assert.Equal(before.Calendar, state.Calendar);
        Assert.Equal(before.Progression!.ContentHash, state.Progression!.ContentHash);
        Assert.Equal(before.Space.Actors, state.Space.Actors);
        var acknowledgment = Assert.IsType<WorldActionCommand>(new LocalAutonomousAftermathController()
            .SelectChoice(state, authority.IsIdentityOccupiedForHost));
        Assert.IsType<AcknowledgeEncounterOutcomeIntent>(acknowledgment.Intent);
        Commit(authority, WorldBatchKind.EncounterLifecycle, [acknowledgment]);
        state = Commit(authority, WorldBatchKind.EncounterLifecycle).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
        Assert.Equal(0, state.Progression!.GetActor(Patient).CurrentHp);
        Assert.Empty(state.Care!.Treatments);
        Assert.Empty(state.Care.Ordinary!.Uses);
        Assert.Empty(state.Production!.ConsumedItems);
        Assert.Equal(before.Calendar, state.Calendar);
        Assert.Equal(before.Space.Actors, state.Space.Actors);
        Assert.Equal(before.EncounterLifecycle!.Debts, state.EncounterLifecycle.Debts);
        return state;
    }

    internal WorldSnapshot ArchiveBeforeRescue(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        for (int step = 0; !WorldActorAvailability.CanAct(state, Helper); step++)
        {
            Assert.True(step < 64);
            state = Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        }
        var lot = state.Inventory.Custody.Lots.First(value => value.CustodianId == state.GetResident(Helper).InventoryCustodianId);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, Helper, Patient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(Patient).InventoryCustodianId, 1, lot.Revision);
        ulong beforeCount = authority.InspectForHost().Journal.MaterialCareArchiveCount;
        for (int offer = 0; authority.InspectForHost().Journal.MaterialCareArchiveCount == beforeCount; offer++)
        {
            Assert.True(offer < WorldMaterialProposalBook.MaximumOutcomes);
            state = Act(authority, Helper, new ProposeMaterialIntent(terms, state.AdvancingSteps + 32));
            state = Act(authority, Helper, new WithdrawMaterialProposalIntent(Assert.Single(state.Coordination!.Proposals!.Open).ProposalId));
            Assert.Equal(0, state.Progression!.GetActor(Patient).CurrentHp);
        }
        Assert.Equal(beforeCount + 1, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.NotNull(state.Coordination!.Anchor);
        return state;
    }

    internal WorldSnapshot CraftDose(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var input = Assert.Single(recipe.Inputs);
        EntityId bag = state.GetResident(Helper).InventoryCustodianId;
        var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == recipe.ItemFor(input));
        return Act(authority, Helper, new CraftIntent(new(recipe.DefinitionId, recipe.Version, state.Production.Revision,
            bag, state.Inventory.Custody.GetStorage(bag).Revision, [new(IngredientLotKind.Source, lot.LotId, lot.Revision, input.Count)])));
    }
}

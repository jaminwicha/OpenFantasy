using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Actual source-stat combat, finite care and Wren's original funded reward in one lived world.</summary>
internal sealed class CareRewardJourney
{
    private ulong _identity = 1;
    private readonly bool _crafted;

    internal CareRewardJourney(bool crafted, WorldSnapshot? genesis = null)
    {
        _crafted = crafted;
        // Raw is the exact production selection. The existing crafted fixture authors only fresh
        // finite berry stock/recipe calibration; it grants no earned Potion, HP, level or reward.
        Genesis = genesis ?? (crafted ? new CareWorldFixture(craftedDose: true).Genesis
            : CareJourneyAdmission.CreateEmbedded(EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName));
        Player = Genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Helper = Genesis.Quests!.Definitions.Single().GiverActorId;
        First = Genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        Second = Genesis.EncounterLifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey);
        Strike = Genesis.EncounterLifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
        Bite = Genesis.EncounterLifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.WolfBiteKey).DefinitionId;
        Assert.Equal(420UL, Genesis.Quests.Definitions.Single().Reward.Experience);
        Assert.Equal(60, Genesis.Progression!.GetActor(Player).CurrentHp);
        Assert.Equal(10, Genesis.Progression.GetActor(Player).CurrentMp);
    }

    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Helper { get; }
    internal EncounterScenarioDefinition First { get; }
    internal EncounterScenarioDefinition Second { get; }
    internal EntityId Strike { get; }
    internal EntityId Bite { get; }
    internal WorldSnapshot? BeforeDefeat { get; private set; }

    internal WorldSnapshot PrepareDefeat(CareWorldSession session)
    {
        Approach(session);
        var state = session.ReadStateForHost();
        if (_crafted)
        {
            var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
            var input = Assert.Single(recipe.Inputs);
            var storageId = state.GetResident(Helper).InventoryCustodianId;
            var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == storageId
                && value.Definition == recipe.ItemFor(input));
            state = Commit(session, Batch(state, WorldBatchKind.AdvancingStep,
                [Command(state, Helper, new CraftIntent(new(recipe.DefinitionId, recipe.Version, state.Production.Revision,
                    storageId, state.Inventory.Custody.GetStorage(storageId).Revision,
                    [new(IngredientLotKind.Source, lot.LotId, lot.Revision, input.Count)])))])).Candidate;
            Assert.Equal(1UL, Assert.Single(state.Production!.Lots).Quantity);
        }
        state = Enter(session, First);
        // Win some ground through real strikes before the deliberate Wait choices. The wolf
        // survives the defeat, allowing one source 40-HP dose to finish this same live encounter.
        for (int boundary = 0; state.Progression!.GetActor(First.TargetActorId).CurrentHp > 24; boundary++)
        {
            Assert.True(boundary < 1_000, "Source-stat preparation exceeded the encounter boundary budget.");
            Assert.True(state.Progression.GetActor(Player).CurrentHp > 0, "The source-stat player fell before weakening the wolf.");
            state = NextCombatBoundary(session, First.TargetActorId, playerAttacks: true);
        }
        Assert.InRange(state.Progression!.GetActor(First.TargetActorId).CurrentHp, 1, 24);
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1_000, "Actual Wait/bite defeat exceeded the encounter boundary budget.");
            BeforeDefeat = state;
            state = NextCombatBoundary(session, First.TargetActorId, playerAttacks: false);
        }
        Assert.Equal(0, state.Progression!.GetActor(Player).CurrentHp);
        Assert.InRange(state.Progression.GetActor(First.TargetActorId).CurrentHp, 1, 24);
        return state;
    }

    internal WorldSnapshot WinAndRelease(CareWorldSession session)
    {
        var state = session.ReadStateForHost();
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1_000, "Resumed source-stat victory exceeded the encounter boundary budget.");
            state = NextCombatBoundary(session, First.TargetActorId, playerAttacks: true);
        }
        Assert.True(state.Progression!.GetActor(Player).CurrentHp > 0, "One actual dose must support this earned victory.");
        Assert.Equal(0, state.Progression.GetActor(First.TargetActorId).CurrentHp);
        return Commit(session, Batch(state, WorldBatchKind.EncounterLifecycle,
            [Command(state, Player, new ReleaseEncounterIntent(state.EncounterActions!.Scope.EncounterId,
                state.EncounterActions.ContentHash))])).Candidate;
    }

    internal WorldActionCommand PrepareReward(CareWorldSession session)
    {
        var state = session.ReadStateForHost();
        for (int step = 0; !WorldActorAvailability.CanAct(state, Player) || !WorldActorAvailability.CanAct(state, Helper); step++)
        {
            Assert.True(step < 32, "Actual exploration steps must retire the retained encounter debts.");
            state = Commit(session, Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        state = Commit(session, Batch(state, WorldBatchKind.AdvancingStep,
            [Command(state, Player, new SpeakToActorIntent(Helper, state.Space.GetActor(Helper).Revision))])).Candidate;
        var quest = Assert.Single(state.Quests!.Definitions);
        state = Commit(session, Batch(state, WorldBatchKind.HeldInteraction,
            [Command(state, Player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
                state.Space.GetActor(Helper).Revision, 0, state.Progression!.GetActor(Player).Revision))])).Candidate;
        EntityId bag = state.GetResident(Player).InventoryCustodianId;
        var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == quest.Item);
        return Command(state, Player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            state.Space.GetActor(Helper).Revision, 1, state.Progression!.GetActor(Player).Revision,
            state.Inventory.Custody.GetStorage(bag).Revision, [new(lot.LotId, lot.Revision, quest.Quantity)]));
    }

    internal WorldSnapshot LaterPlayerAttack(CareWorldSession session)
    {
        var state = session.ReadStateForHost();
        state = Commit(session, Batch(state, WorldBatchKind.HeldInteraction,
            [Command(state, Player, new CloseDialogueIntent(state.Dialogues.Single().DialogueId))])).Candidate;
        state = Enter(session, Second);
        for (int boundary = 0; !state.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>()
            .Any(value => value.ScenarioId == Second.DefinitionId && value.Request.ActorId == Player); boundary++)
        {
            Assert.True(boundary < 128, "The earned-level attack must resolve within the original encounter readiness bound.");
            state = NextCombatBoundary(session, Second.TargetActorId, playerAttacks: true);
        }
        return state;
    }

    internal WorldActionCommand Acknowledge(WorldSnapshot state) => Command(state, Player,
        new AcknowledgeCareIntent(state.Care!.Agreements.Single().AgreementId, state.EncounterActions!.Scope.EncounterId,
            state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash));

    internal WorldActionCommand Treat(WorldSnapshot state) => Command(state, Helper,
        WorldCareActions.PrepareRemedy(state) ?? throw new InvalidOperationException("The real helper must have an actual available dose."));

    internal WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind, IEnumerable<WorldActionCommand>? actions = null) =>
        new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, kind, [], actions ?? [], state.ProfileVersion);

    internal WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextId(), control.PrincipalId, control.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }

    internal TrustedCommandDelivery Delivery(WorldActionCommand command) => new(command.PrincipalId, command.OriginSessionId);

    internal static WorldStepDecision Commit(CareWorldSession session, WorldStepBatch batch)
    {
        var result = session.Commit(batch);
        Assert.All(result.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
        return result;
    }

    private WorldSnapshot Enter(CareWorldSession session, EncounterScenarioDefinition scenario)
    {
        var state = session.ReadStateForHost();
        state = Commit(session, Batch(state, WorldBatchKind.EncounterLifecycle,
            [Command(state, Player, new EnterEncounterIntent(scenario.DefinitionId,
                EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
                state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision))])).Candidate;
        for (int stage = 0; stage < 2; stage++) state = Commit(session, Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        return state;
    }

    private WorldSnapshot NextCombatBoundary(CareWorldSession session, EntityId wolf, bool playerAttacks)
    {
        var state = session.ReadStateForHost();
        var timing = state.Pacing!.Encounter!;
        var due = WorldEncounterActions.DueActors(state);
        var commands = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
            .Select(actor => Command(state, actor.ActorId, actor.ActorId == Player && !playerAttacks
                ? new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision)
                : new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision,
                    actor.ActorId == Player ? Strike : Bite, actor.ActorId == Player ? wolf : Player))).ToArray();
        var kind = commands.Length != 0 ? WorldBatchKind.EncounterDecision
            : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
        return Commit(session, Batch(state, kind, commands)).Candidate;
    }

    private void Approach(CareWorldSession session)
    {
        var state = session.ReadStateForHost();
        var goal = state.Space.GetActor(Player).Feet.Offset(0, -256, 0);
        for (int step = 0; state.Space.GetActor(Helper).Feet != goal; step++)
        {
            Assert.True(step < 256, "The helper must approach along the existing bounded real ground route.");
            var route = state.Space.QueryGroundRoute(Helper, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = state.Space.GetActor(Helper).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            var control = state.Inventory.Controls.Single(value => value.ActorId == Helper);
            state = Commit(session, new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(new(control.PrincipalId, control.SessionId), Helper, dx, dy)], [], state.ProfileVersion)).Candidate;
            Assert.Equal(feet.Offset(dx, dy, 0), state.Space.GetActor(Helper).Feet);
        }
    }

    private EntityId NextId()
    {
        var id = new EntityId(0xca25feed00000000, _identity++);
        Assert.False(Genesis.IdentityIndex.Contains(id));
        return id;
    }
}

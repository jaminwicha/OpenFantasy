using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.RestorativeChoicesNative;

internal sealed partial class RestorativeChoicesWorld
{
    private void EarnInjury(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        var strike = state.EncounterLifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey);
        var bite = state.EncounterLifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.WolfBiteKey);
        EntityId wolf = scenario.TargetActorId;
        Assert.Equal(Owner, scenario.InitiatingActorId);
        if (state.Space.QueryInteraction(Owner, wolf, scenario.EntryRangeSubunits) != SpatialInteraction.Reachable)
            Walk(authority, Owner, Near(state, Owner, state.Space.GetActor(wolf).Feet, wolf, scenario.EntryRangeSubunits));
        foreach (EntityId actor in Actors.Where(actor => actor != Owner))
        {
            state = authority.ReadStateForHost();
            if (state.Space.QueryInteraction(Owner, actor, scenario.EntryRangeSubunits) == SpatialInteraction.Reachable
                && state.Space.QueryInteraction(actor, wolf, strike.RangeSubunits) == SpatialInteraction.Reachable) continue;
            Walk(authority, actor, Near(state, actor, state.Space.GetActor(wolf).Feet, wolf, strike.RangeSubunits));
        }
        WaitReady(authority);
        state = authority.ReadStateForHost();
        state = Commit(authority, WorldBatchKind.EncounterLifecycle, Command(state, Owner,
            new EnterEncounterIntent(scenario.DefinitionId, EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
                state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(wolf).Revision)));
        for (int stage = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; stage++)
        {
            Assert.True(stage < 4, "The actual encounter did not finish its existing staging boundaries.");
            state = Commit(authority, WorldBatchKind.EncounterLifecycle);
        }
        bool weakened = false;
        bool finishing = false;
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1000, "Actual source-stat injury exceeded the existing 1000-boundary combat preparation limit.");
            int hp = state.Progression!.GetActor(Owner).CurrentHp;
            Assert.True(hp > 0, "The patient fell; this preparation cannot claim a living injured native boundary.");
            weakened |= state.Progression.GetActor(wolf).CurrentHp <= 24;
            finishing |= hp < 20;
            var timing = state.Pacing!.Encounter!;
            var due = WorldEncounterActions.DueActors(state);
            var choices = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId)).Select(actor =>
            {
                if (actor.ActorId == wolf)
                    return Command(state, wolf, new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, bite.DefinitionId, Owner));
                bool attack = (finishing || !weakened && actor.ActorId == Owner)
                    && state.Space.QueryInteraction(actor.ActorId, wolf, strike.RangeSubunits) == SpatialInteraction.Reachable;
                return Command(state, actor.ActorId, attack
                    ? new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, strike.DefinitionId, wolf)
                    : new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision));
            }).ToArray();
            state = Commit(authority, choices.Length > 0 ? WorldBatchKind.EncounterDecision
                : due.Count > 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance, choices);
        }
        Assert.InRange(state.Progression!.GetActor(Owner).CurrentHp, 1, 19);
        Assert.Equal(0, state.Progression.GetActor(wolf).CurrentHp);
        var outcome = WorldEncounterOutcomes.Determine(state)!;
        Assert.Equal(Actors.Order(), outcome.RequiredHumans.Order());
        foreach (var actor in outcome.RequiredHumans)
            state = Commit(authority, WorldBatchKind.EncounterLifecycle,
                Command(state, actor, new AcknowledgeEncounterOutcomeIntent(outcome.EncounterId, outcome.ActionHash)));
        for (int stage = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration; stage++)
        {
            Assert.True(stage < 4, "The actual acknowledged outcome did not complete its existing lifecycle boundaries.");
            state = Commit(authority, WorldBatchKind.EncounterLifecycle);
        }
        WaitReady(authority);
        state = authority.ReadStateForHost();
        Assert.Empty(state.Care!.Treatments);
        Assert.Empty(state.Care.Ordinary!.Uses);
        _preparation.Add($"Source-stat first-wolf combat: all four humans made ordinary choices and read the victory; owner HP {state.Progression!.GetActor(Owner).CurrentHp}/{state.Progression.GetActor(Owner).Stats.MaxHp}. No health/stat edit, aftermath dose, quest reward or native-combat claim.");
    }

    private SubunitPosition Near(WorldSnapshot state, EntityId actor, SubunitPosition center, EntityId other, long range)
    {
        var candidates = (from x in new[] { -256L, -128L, 0L, 128L, 256L }
            from y in new[] { -256L, -128L, 0L, 128L, 256L }
            where x != 0 || y != 0
            let goal = center.Offset(x, y, 0)
            where state.Space.QueryOccupancy(actor, goal) == SpatialOccupancy.Clear
            let route = state.Space.QueryGroundRoute(actor, goal)
            where route.Status == GroundRouteStatus.Reachable
            orderby route.Waypoints.Count, Math.Abs(x) + Math.Abs(y), goal.X, goal.Y
            select goal).ToArray();
        foreach (var goal in candidates)
        {
            // Geometry-only destination query; every persisted displacement below is an ordinary movement commit.
            var previous = state.Space.GetActor(actor);
            var placed = new ActorPresence(previous.Binding, previous.Condition, previous.Scope, goal, previous.Revision, previous.HomePlaceId);
            var geometry = state.Space.WithActors(state.Space.Actors.Select(value => value.ActorId == actor ? placed : value));
            if (geometry.QueryInteraction(actor, other, range) == SpatialInteraction.Reachable
                && geometry.QueryInteraction(other, actor, range) == SpatialInteraction.Reachable
                && (actor == Owner || geometry.QueryInteraction(Owner, actor, range) == SpatialInteraction.Reachable)) return goal;
        }
        throw new InvalidOperationException("No bounded actual route supplies the ordinary encounter cohort.");
    }

    private void CraftSecondDose(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var term = Assert.Single(recipe.Inputs);
        Assert.Equal(3UL, term.Count);
        var berries = recipe.ItemFor(term);
        var node = state.Gathering!.Nodes.Single(value => value.Definition.YieldDefinition == berries);
        // The actual cooperative fighting formation can occupy the lead's exit. Return only the
        // moved humans to their admitted places through ordinary movement before departing for berries.
        foreach (var actor in Actors.Where(actor => actor != Owner))
        {
            var home = Genesis.Space.GetActor(actor).Feet;
            if (authority.ReadStateForHost().Space.GetActor(actor).Feet != home) Walk(authority, actor, home);
        }
        // The production route query owns obstacles and support; an assumed L-shaped corner may
        // itself be blocked even when the actual berry destination has a valid route.
        Walk(authority, Owner, node.Feet);
        EntityId bag = state.GetResident(Owner).InventoryCustodianId;
        ulong firstIssued = node.NextUnissuedUnit;
        for (int harvest = 0; harvest < 3; harvest++)
        {
            state = authority.ReadStateForHost();
            for (int rest = 0; state.Gathering!.GetActorWork(Owner).Effort < GatherRulesV1.EffortPerSwing; rest++)
            {
                Assert.True(rest < 40, "Ordinary gathering effort exceeded the existing 40-step recovery bound.");
                state = Commit(authority, WorldBatchKind.AdvancingStep);
            }
            node = state.Gathering!.GetNode(node.NodeId);
            var tool = state.Gathering.Tools.Single(value => value.Kind == node.Definition.RequiredToolKind);
            var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == tool.Definition);
            state = Commit(authority, WorldBatchKind.AdvancingStep,
                Command(state, Owner, new GatherIntent(node.NodeId, node.Revision, lot.LotId, lot.Revision)));
        }
        Assert.Equal(firstIssued + 3, state.Gathering!.GetNode(node.NodeId).NextUnissuedUnit);
        var ingredients = state.Inventory.Custody.Lots.Where(value => value.CustodianId == bag && value.Definition == berries).OrderBy(value => value.LotId).ToArray();
        Assert.Equal(3UL, ingredients.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        state = Commit(authority, WorldBatchKind.AdvancingStep, Command(state, Owner,
            new CraftIntent(new(recipe.DefinitionId, recipe.Version, state.Production!.Revision, bag,
                state.Inventory.Custody.GetStorage(bag).Revision,
                ingredients.Select(lot => new IngredientRequest(IngredientLotKind.Source, lot.LotId, lot.Revision, lot.Quantity))))));
        WaitReady(authority);
        state = authority.ReadStateForHost();
        var potion = Assert.Single(state.Production!.Lots, lot => lot.CustodianId == bag && lot.Definition == state.Care!.Remedies.Single().Item);
        var produced = Assert.Single(state.Production.Batches, batch => batch.BatchId == potion.Output.BatchId);
        Assert.Equal(Owner, produced.ActorId);
        Assert.Equal(recipe.DefinitionId, produced.RecipeId);
        Assert.Equal(1UL, potion.Quantity);
        _preparation.Add($"Three source gathering choices consumed actual work effort and finite berries, then recipe {recipe.DefinitionId} produced one Potion {potion.LotId}; original raw Potion remains. No native harvesting/crafting claim.");
        EntityId helper = state.Care!.Ordinary!.Permissions.Single(value => value.PermissionId == Permission).HelperActorId;
        Assert.NotEqual(SpatialInteraction.Reachable, state.Space.QueryInteraction(helper, Owner));
        _preparation.Add("The injured owner remains at the berry field, outside the stocked helper's mutual reach. Native withdrawal proves exact permission/no-cost; eligible-helper suppression is a separate managed contrast.");
    }
}

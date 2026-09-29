using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Unmodified production stats, actual defeat/recovery/victory, then ordinary finite self or companion use.</summary>
internal sealed class OrdinaryCareWorldFixture
{
    internal OrdinaryCareWorldFixture(string seedText = EncounterJourneyWorldFixture.SeedText)
        : this(RestorativeJourneyAdmission.CreateEmbedded(seedText, EncounterJourneyWorldFixture.PlayerName))
    {
    }

    internal OrdinaryCareWorldFixture(WorldSnapshot genesis)
    {
        Genesis = genesis;
        Journey = new(false, Genesis);
    }
    internal WorldSnapshot Genesis { get; }
    internal CareRewardJourney Journey { get; }
    internal EntityId Player => Journey.Player;
    internal EntityId Helper => Journey.Helper;

    internal WorldSnapshot ReachInjuredExploration(CareWorldSession session) => ReachExploration(session, fullDose: false);

    internal WorldSnapshot ReachFullDoseInjuredExploration(CareWorldSession session) => ReachExploration(session, fullDose: true);

    private WorldSnapshot ReachExploration(CareWorldSession session, bool fullDose)
    {
        var defeated = Journey.PrepareDefeat(session);
        var pending = CareRewardJourney.Commit(session, Journey.Batch(defeated, WorldBatchKind.Care,
            [Journey.Acknowledge(defeated)])).Candidate;
        var treated = CareRewardJourney.Commit(session, Journey.Batch(pending, WorldBatchKind.Care,
            [Journey.Treat(pending)])).Candidate;
        Assert.Equal(40, treated.Progression!.GetActor(Player).CurrentHp);
        if (fullDose) SelectOneAdditionalPatientWait(session);
        var state = Journey.WinAndRelease(session);
        if (fullDose)
        {
            var bites = state.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>()
                .Where(value => value.Request.ActorId == Journey.First.TargetActorId
                    && value.Resolution.WorldSequence > treated.Revision).ToArray();
            Assert.True(bites.Length >= 3, "The deliberate extra Wait must earn an additional actual source wolf bite before victory.");
            Assert.All(bites, value => Assert.Equal(Player, value.Resolution.TargetActorId));
            Assert.Equal(40L - state.Progression!.GetActor(Player).CurrentHp,
                bites.Sum(value => (long)value.Resolution.TargetHpBefore - value.Resolution.TargetHpAfter));
            Assert.InRange(state.Progression!.GetActor(Player).CurrentHp, 1, 20);
        }
        for (int step = 0; !WorldActorAvailability.CanAct(state, Player) || !WorldActorAvailability.CanAct(state, Helper); step++)
        {
            Assert.True(step < 32);
            state = CareRewardJourney.Commit(session, Journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        Assert.InRange(state.Progression!.GetActor(Player).CurrentHp, 1, 59);
        return state;
    }

    private void SelectOneAdditionalPatientWait(CareWorldSession session)
    {
        for (int boundary = 0; boundary < 128; boundary++)
        {
            var state = session.ReadStateForHost();
            var timing = state.Pacing!.Encounter!;
            var due = WorldEncounterActions.DueActors(state);
            var commands = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
                .Select(actor => Journey.Command(state, actor.ActorId, actor.ActorId == Player
                    ? new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision)
                    : new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, Journey.Bite, Player))).ToArray();
            bool selectedWait = commands.Any(value => value.ActorId == Player && value.Intent is QueueEncounterWaitIntent);
            var kind = commands.Length != 0 ? WorldBatchKind.EncounterDecision
                : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
            CareRewardJourney.Commit(session, Journey.Batch(state, kind, commands));
            if (selectedWait) return;
        }
        throw new InvalidOperationException("The real patient must reach one extra Wait choice within the existing encounter boundary budget.");
    }

    internal WorldSnapshot Supply(CareWorldSession session, bool self)
    {
        var state = session.ReadStateForHost();
        var storage = state.GetResident(Helper).InventoryCustodianId;
        var potion = state.Care!.Remedies.Single().Item;
        WorldActionIntent intent;
        if (self)
        {
            var playerStorage = state.GetResident(Player).InventoryCustodianId;
            Assert.Equal(1UL, state.Inventory.Custody.Lots.Single(value => value.CustodianId == playerStorage
                && value.Definition == potion).Quantity);
            return state;
        }
        else
        {
            var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
            var input = Assert.Single(recipe.Inputs);
            var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == storage && value.Definition == recipe.ItemFor(input));
            intent = new CraftIntent(new(recipe.DefinitionId, recipe.Version, state.Production.Revision, storage,
                state.Inventory.Custody.GetStorage(storage).Revision, [new(IngredientLotKind.Source, lot.LotId, lot.Revision, input.Count)]));
        }
        return CareRewardJourney.Commit(session, Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [Journey.Command(state, Helper, intent)])).Candidate;
    }

    internal UseRestorativeItemIntent Intent(WorldSnapshot state, bool self)
    {
        var definition = state.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode ==
            (self ? RestorativeDeliveryMode.SelfUse : RestorativeDeliveryMode.LivingAssistance));
        EntityId actor = self ? Player : Helper;
        var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state, actor, Player,
            definition.DefinitionId, self ? null : state.Care.Ordinary.Permissions.Single().PermissionId));
        if (self) return prepared;
        var lot = state.Production!.Lots.Single(value => value.Definition == state.Care.Remedies.Single().Item);
        return new(prepared.RecipientActorId, prepared.UseDefinitionId, prepared.UseDefinitionVersion, prepared.PermissionId,
            prepared.PermissionGrantVersion, prepared.ExpectedPermissionRevision, prepared.ExpectedPatientPresenceRevision,
            prepared.ExpectedPatientProgressionRevision, prepared.ExpectedStorageRevision, prepared.ExpectedProductionRevision,
            new(IngredientLotKind.Transformed, lot.LotId, lot.Revision, 1));
    }
}

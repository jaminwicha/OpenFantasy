using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Tests.Worlds.CoPlayerTrade;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.ActorSelection;

public sealed class ControlledActorProjectionTests
{
    [Fact]
    public void Separately_accepted_scopes_disclose_only_current_purpose_sections_and_never_blanket_progression()
    {
        WorldControlCapabilities[] scopes =
        [
            WorldControlCapabilities.Traversal, WorldControlCapabilities.Combat,
            WorldControlCapabilities.Recovery, WorldControlCapabilities.CareDisposition,
            WorldControlCapabilities.Inventory, WorldControlCapabilities.Trade,
            WorldControlCapabilities.Production, WorldControlCapabilities.ConversationAndQuests,
            WorldControlCapabilities.PrivateHistory, WorldControlCapabilities.All
        ];
        foreach (var scope in scopes)
        {
            var fixture = new EffectiveControlFixture();
            using var files = new StorageFiles();
            using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
            fixture.Grant(authority, scope);
            fixture.LoseAndExpire(authority);
            var state = fixture.Acquire(authority);
            var view = Project(fixture, state, new LocalRegionObserver(fixture.Owner));
            bool Has(WorldControlCapabilities required) => (scope & required) == required;
            Assert.Equal(fixture.Owner, view.ActorId);
            Assert.Equal(state.WorldId, view.WorldId);
            Assert.Equal(state.BranchId, view.BranchId);
            Assert.Equal(state.Calendar, view.Calendar);
            Assert.Equal(scope, view.Capabilities);
            Assert.Equal(fixture.Owner, view.Regional!.PlayerActorId);
            Assert.Contains(view.Actors, actor => actor.ActorId == fixture.Grantee);
            Assert.Equal(Has(WorldControlCapabilities.Combat), view.Combat is not null);
            Assert.Equal(Has(WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal), view.Retreat is not null);
            Assert.Equal(Has(WorldControlCapabilities.Recovery), view.Recovery is not null);
            Assert.Equal(Has(WorldControlCapabilities.CareDisposition), view.CareDisposition is not null);
            Assert.Equal(Has(WorldControlCapabilities.Inventory), view.Inventory is not null);
            Assert.Equal(Has(WorldControlCapabilities.Inventory), view.MaterialProposals is not null);
            Assert.Equal(Has(WorldControlCapabilities.Trade), view.Trade is not null);
            Assert.Equal(Has(WorldControlCapabilities.Production), view.Production is not null);
            Assert.Equal(Has(WorldControlCapabilities.ConversationAndQuests), view.Conversation is not null);
            if (view.Inventory is { } inventory)
            {
                Assert.Equal(state.GetResident(fixture.Owner).InventoryCustodianId, inventory.CustodianId);
                Assert.NotEqual(state.GetResident(fixture.Grantee).InventoryCustodianId, inventory.CustodianId);
                Assert.NotEmpty(inventory.Lots);
            }
            if (view.Combat is { } combat)
            {
                Assert.Equal(state.Progression!.GetActor(fixture.Owner).CurrentHp, combat.Resources!.Hp);
                Assert.Equal(state.Progression.GetActor(fixture.Owner).CurrentMp, combat.Resources.Mp);
            }
            if (view.Recovery is { } recovery) Assert.NotEmpty(recovery.SelfUseOffers);
            if (view.CareDisposition is { } disposition) Assert.NotEmpty(disposition.PermissionOffers);
            if (view.Trade is { } trade)
                Assert.Equal(state.Progression!.Currency.GetActorAccount(fixture.Owner).Balance, trade.Wallet!.Balance);
            if (view.Production is { } production)
            {
                Assert.NotEmpty(production.GatheringTools);
                Assert.NotNull(production.Crafting);
                Assert.NotNull(production.Worksite);
            }
            string json = JsonSerializer.Serialize(view);
            Assert.DoesNotContain("\"Experience\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ProfileName\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"CurrencyBalance\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"EncounterOutcome\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Last\"", json, StringComparison.Ordinal);
            Assert.Same(state, authority.ReadStateForHost());
        }
    }

    [Fact]
    public void Exact_current_witness_home_delivery_and_regional_observer_are_required_without_mutating_the_root()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Grant(authority, WorldControlCapabilities.All);
        fixture.LoseAndExpire(authority);
        var state = fixture.Acquire(authority);
        var witness = state.EffectiveControl!.Find(fixture.Owner)!.Witness!;
        var original = Project(fixture, state);
        string originalBytes = JsonSerializer.Serialize(original);
        Assert.Throws<ArgumentException>(() => LocalControlledActorView.Project(state, fixture.Owner,
            fixture.Delivery(fixture.Grantee), witness));
        Assert.Throws<ArgumentException>(() => LocalControlledActorView.Project(state, fixture.Grantee,
            fixture.Delivery(fixture.Owner), witness));
        Assert.Throws<ArgumentException>(() => Project(fixture, state, new LocalRegionObserver(fixture.Grantee)));
        Assert.Same(state, authority.ReadStateForHost());
        fixture.Release(authority);
        var replacement = fixture.Acquire(authority);
        Assert.NotEqual(witness, replacement.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Throws<ArgumentException>(() => LocalControlledActorView.Project(replacement, fixture.Grantee,
            fixture.Delivery(fixture.Grantee), witness));
        _ = Project(fixture, replacement);
        Assert.Equal(originalBytes, JsonSerializer.Serialize(original));
        var current = replacement.EffectiveControl!.Find(fixture.Owner)!.Witness!;
        var revoked = fixture.Revoke(authority);
        Assert.Throws<ArgumentException>(() => LocalControlledActorView.Project(revoked, fixture.Grantee,
            fixture.Delivery(fixture.Grantee), current));
        Assert.Same(revoked, authority.ReadStateForHost());
    }

    [Fact]
    public void Real_open_material_and_paid_terms_are_visible_but_their_actual_withdrawals_are_not_history_access()
    {
        var fixture = new CoPlayerTradeFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var state = fixture.Prepare(authority, fund: false);
        fixture.Submit(authority, fixture.Offer(state, fixture.Owner, fixture.Helper, fixture.Lot(state, fixture.Owner)));
        state = authority.ReadStateForHost();
        var material = Assert.Single(state.Coordination!.Proposals!.Open);
        var (issue, quote) = fixture.Quote(state, fixture.Owner, fixture.Helper, fixture.Lot(state, fixture.Owner));
        fixture.Submit(authority, issue);
        state = authority.ReadStateForHost();
        var open = Project(fixture.Control, state);
        Assert.Equal(material.ProposalId, Assert.Single(open.MaterialProposals!.Offers).ProposalId);
        Assert.Equal(quote.QuoteId, Assert.Single(open.Trade!.Quotes).QuoteId);
        Assert.Null(open.Combat);
        Assert.Null(open.Recovery);
        fixture.Submit(authority, fixture.Control.Delegated(state, new WithdrawMaterialProposalIntent(material.ProposalId)));
        state = authority.ReadStateForHost();
        fixture.Submit(authority, fixture.Control.Delegated(state, new WithdrawSpotQuoteIntent(quote.QuoteId)));
        state = authority.ReadStateForHost();
        Assert.NotNull(state.Coordination!.Proposals!.FindOutcome(material.ProposalId));
        Assert.NotNull(state.Coordination.PaidExchange!.FindOutcome(quote.QuoteId));
        var full = new LocalWorldView(state, fixture.Owner);
        Assert.Contains(full.MaterialProposals!.Offers, offer => offer.Status == MaterialProposalStatus.Withdrawn);
        Assert.Contains(full.PaidQuotes, offer => offer.Status == PaidQuoteStatus.Withdrawn);
        var scoped = Project(fixture.Control, state);
        Assert.Empty(scoped.MaterialProposals!.Offers);
        Assert.Empty(scoped.Trade!.Quotes);
        Assert.NotNull(scoped.MaterialProposals.Preparation);
        Assert.Single(open.MaterialProposals.Offers);
        Assert.Single(open.Trade.Quotes);
        Assert.Same(state, authority.ReadStateForHost());
    }

    [Fact]
    public void Genuine_pending_care_and_outcome_choices_disappear_after_their_actual_cancellation_and_completion()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Grant(authority, WorldControlCapabilities.Combat | WorldControlCapabilities.Recovery
            | WorldControlCapabilities.CareDisposition);
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.GetScenario(fixture.Journey.FirstScenario);
        fixture.Journey.WalkTo(authority, fixture.Owner, fixture.Journey.Beside(state, fixture.Owner, scenario.TargetActorId));
        state = authority.ReadStateForHost();
        fixture.Journey.WalkTo(authority, fixture.Other, fixture.Journey.Beside(state, fixture.Other, fixture.Owner));
        state = fixture.Journey.EnterAndFight(authority, fixture.Journey.FirstScenario, patientWaits: true);
        Assert.Equal(0, state.Progression!.GetActor(fixture.Owner).CurrentHp);
        fixture.LoseAndExpire(authority);
        state = fixture.Acquire(authority);
        var offered = Project(fixture, state);
        Assert.NotEmpty(offered.Recovery!.Care!.Offers);
        Assert.Null(offered.CareDisposition!.Care);
        Assert.Equal(new LocalWorldView(state, fixture.Owner).EncounterOutcome!.Acknowledgment,
            Assert.IsType<AcknowledgeEncounterOutcomeIntent>(offered.Combat!.OutcomeAcknowledgment));
        var agreement = Assert.Single(state.Care!.Agreements);
        var acknowledgment = fixture.Delegated(state, new AcknowledgeCareIntent(agreement.AgreementId,
            state.EncounterActions!.Scope.EncounterId, state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash));
        state = fixture.Apply(authority, WorldBatchKind.Care, acknowledgment);
        var pending = Project(fixture, state);
        Assert.Null(pending.Combat!.OutcomeAcknowledgment);
        Assert.NotNull(pending.Recovery!.Care!.PendingAcknowledgmentId);
        Assert.Null(pending.Recovery.Care.Decision!.Cancellation);
        Assert.Null(pending.Recovery.Care.Decision.Decline);
        Assert.Null(pending.CareDisposition!.Care!.Decision!.Treatment);
        var cancellation = Assert.IsType<CancelPendingCareIntent>(pending.CareDisposition.Care.Decision.Cancellation);
        state = fixture.Apply(authority, WorldBatchKind.Care, fixture.Delegated(state, cancellation));
        Assert.NotNull(new LocalWorldView(state, fixture.Owner).Care!.Closure);
        var closed = Project(fixture, state);
        Assert.Null(closed.Recovery!.Care);
        Assert.Null(closed.CareDisposition!.Care);
        Assert.Equal(cancellation.AcknowledgmentId, pending.Recovery.Care.PendingAcknowledgmentId);
        var outcomeChoice = Assert.IsType<AcknowledgeEncounterOutcomeIntent>(closed.Combat!.OutcomeAcknowledgment);
        Assert.Equal(state.EncounterActions!.Scope.EncounterId, outcomeChoice.EncounterId);
        Assert.Equal(state.EncounterActions.ContentHash, outcomeChoice.ExpectedActionHash);
        var acknowledgeOutcome = fixture.Delegated(state, outcomeChoice);
        state = fixture.Apply(authority, WorldBatchKind.EncounterLifecycle, acknowledgeOutcome);
        Assert.Null(Project(fixture, state).Combat!.OutcomeAcknowledgment);
        state = authority.Commit(fixture.Commands.Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
        Assert.NotNull(state.EncounterOutcome!.Last);
        Assert.NotNull(new LocalWorldView(state, fixture.Owner).EncounterOutcome!.CompletedAtSequence);
        var completed = Project(fixture, state);
        Assert.Null(completed.Combat!.OutcomeAcknowledgment);
        string serialized = JsonSerializer.Serialize(completed);
        Assert.DoesNotContain("\"CompletedAtSequence\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"EncounterOutcome\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Experience\"", serialized, StringComparison.Ordinal);
        Assert.Equal(outcomeChoice, closed.Combat.OutcomeAcknowledgment);
        Assert.Same(state, authority.ReadStateForHost());
    }

    private static LocalControlledActorView Project(EffectiveControlFixture fixture, WorldSnapshot state,
        LocalRegionObserver? observer = null) => LocalControlledActorView.Project(state, fixture.Grantee,
        fixture.Delivery(fixture.Grantee), state.EffectiveControl!.Find(fixture.Owner)!.Witness!, regions: observer);

    [Fact]
    public void Quest_and_craft_requests_use_only_scoped_inputs_after_real_resource_and_storage_revisions()
    {
        var trade = new CoPlayerTradeFixture();
        var fixture = trade.Control;
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var state = authority.ReadStateForHost();
        fixture.Journey.WalkTo(authority, fixture.Owner, fixture.Journey.Beside(state, fixture.Owner, fixture.Other));
        state = trade.Ready(authority);
        var selfUse = state.Care!.Ordinary!.Definitions.Single(definition =>
            definition.DeliveryMode == RestorativeDeliveryMode.SelfUse);
        var use = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.PrepareAvailability(state,
            fixture.Owner, fixture.Owner, selfUse.DefinitionId).Intent);
        fixture.Apply(authority, WorldBatchKind.AdvancingStep, fixture.Commands.Command(state, fixture.Owner, use));
        trade.Ready(authority);
        fixture.Journey.CraftDose(authority);
        state = trade.Ready(authority);
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var input = Assert.Single(recipe.Inputs);
        var supplied = state.Inventory.Custody.Lots.Single(lot =>
            lot.CustodianId == state.GetResident(fixture.Other).InventoryCustodianId && lot.Definition == recipe.ItemFor(input));
        Assert.True(supplied.Quantity >= input.Count);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, fixture.Other, fixture.Owner,
            ConsentedMaterialLotKind.CountedSource, supplied.LotId, supplied.Definition, supplied.CustodianId,
            state.GetResident(fixture.Owner).InventoryCustodianId, input.Count, supplied.Revision);
        trade.Submit(authority, fixture.Commands.Command(state, fixture.Other,
            new ProposeMaterialIntent(terms, state.AdvancingSteps + 1_024)));
        state = authority.ReadStateForHost();
        var offer = Assert.Single(state.Coordination!.Proposals!.Open);
        trade.Submit(authority, fixture.Commands.Command(state, fixture.Owner,
            AcceptMaterialProposalIntent.Approving(offer, state.Space.GetActor(fixture.Other).Revision)));
        trade.Ready(authority);
        fixture.Grant(authority, WorldControlCapabilities.Production | WorldControlCapabilities.ConversationAndQuests);
        fixture.LoseAndExpire(authority);
        state = fixture.Acquire(authority);
        var selectedWitness = state.EffectiveControl!.Find(fixture.Owner)!.Witness!;

        var view = Project(fixture, state);
        Assert.Null(view.Inventory);
        Assert.Null(view.Combat);
        Assert.Null(view.Recovery);
        Assert.Null(view.Trade);
        var speaker = view.Actors.Single(actor => actor.ActorId == fixture.Other);
        Apply(view, new SpeakToActorIntent(speaker.ActorId, speaker.PresenceRevision), WorldBatchKind.AdvancingStep);
        view = Project(fixture, authority.ReadStateForHost());
        var quest = Assert.Single(view.Conversation!.Quests);
        var requests = view.Conversation.Requests!;
        Assert.True(requests.ProgressionRevision > 0);
        Assert.True(requests.InventoryRevision > 0);
        var accept = Assert.IsType<AcceptDeliveryQuestIntent>(requests.Prepare(quest.DefinitionId));
        Assert.Equal(requests.ProgressionRevision, accept.ExpectedProgressionRevision);
        Apply(view, accept, WorldBatchKind.HeldInteraction);
        view = Project(fixture, authority.ReadStateForHost());
        var complete = Assert.IsType<CompleteDeliveryQuestIntent>(view.Conversation!.Requests!.Prepare(quest.DefinitionId));
        Assert.True(complete.ExpectedProgressionRevision > 0);
        Assert.True(complete.ExpectedInventoryRevision > 0);
        Assert.All(complete.Selections, selected => Assert.Contains(view.Conversation.Requests.DeliveryLots,
            lot => lot.LotId == selected.LotId && lot.Revision == selected.ExpectedRevision));
        Apply(view, complete, WorldBatchKind.HeldInteraction);
        view = Project(fixture, authority.ReadStateForHost());
        Assert.Empty(view.Conversation!.Quests);
        Assert.Empty(view.Conversation.Requests!.DeliveryLots);
        var visibleProject = Assert.Single(view.Production!.Construction);
        var actualProject = authority.ReadStateForHost().Construction!.Projects.Single(project => project.ProjectId == visibleProject.ProjectId);
        Assert.NotEqual(view.ActorId, actualProject.BuilderActorId);
        Assert.Empty(view.Production.ConstructionWork);
        // Commission visibility is not permission to use the separate NPC builder's private tools or work tokens.
        Assert.DoesNotContain(view.Production.GatheringTools, tool =>
            authority.ReadStateForHost().Inventory.Custody.Lots.Any(lot => lot.LotId == tool.LotId
                && lot.CustodianId == authority.ReadStateForHost().GetResident(actualProject.BuilderActorId).InventoryCustodianId));
        Apply(view, new CloseDialogueIntent(view.Conversation.Dialogue!.DialogueId), WorldBatchKind.HeldInteraction);
        trade.Ready(authority);
        view = Project(fixture, authority.ReadStateForHost());
        var crafting = view.Production!.CraftPreparation!;
        var chosenRecipe = crafting.Recipes.Single(value => value.Key == CareJourneyAdmission.RecipeKey);
        var craft = Assert.IsType<CraftRequest>(crafting.Prepare(chosenRecipe.RecipeId));
        Assert.True(craft.ExpectedProductionRevision > 0);
        Assert.True(craft.ExpectedStorageRevision > 0);
        Assert.Equal(crafting.CustodianId, craft.CustodianId);
        Assert.All(crafting.Ingredients, lot => Assert.Contains(crafting.Recipes.SelectMany(value => value.Terms),
            term => term.Role == RecipeMaterialRole.Input && term.DefinitionId == lot.DefinitionId));
        var before = authority.ReadStateForHost();
        Apply(view, new CraftIntent(craft), WorldBatchKind.AdvancingStep);
        state = authority.ReadStateForHost();
        Assert.Equal(before.Production!.Revision + 1, state.Production!.Revision);
        Assert.Contains(state.Production.Lots, lot => lot.CustodianId == craft.CustodianId);
        Assert.Null(view.Inventory);
        Assert.DoesNotContain("\"Experience\"", JsonSerializer.Serialize(view), StringComparison.Ordinal);
        Assert.DoesNotContain("\"ProfileName\"", JsonSerializer.Serialize(view), StringComparison.Ordinal);

        void Apply(LocalControlledActorView captured, WorldActionIntent intent, WorldBatchKind kind)
        {
            var delivery = fixture.Delivery(fixture.Grantee);
            var command = new WorldActionCommand(fixture.Commands.NextId(), delivery.PrincipalId, delivery.SessionId,
                captured.ActorId, captured.WorldId, captured.BranchId,
                captured.Actors.Single(actor => actor.ActorId == captured.ActorId).PresenceRevision,
                intent, selectedWitness);
            fixture.Apply(authority, kind, command);
        }
    }
}

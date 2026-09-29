using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class MaterialProposalIntegrationTests
{
    [Fact]
    public void One_current_principal_can_create_and_accept_as_two_distinct_actors_after_reopening()
    {
        using var live = new MaterialProposalTestSession(MaterialProposalTestSession.CreateSharedControlGenesis());
        live.Actions.Approach(new CareWorldSession(live.Authority));
        EntityId sender = live.Actions.Helper, recipient = live.Actions.Player;
        Assert.Equal(live.Delivery(sender), live.Delivery(recipient));
        Assert.NotEqual(sender, recipient);
        WorldMaterialProposal proposal = live.Save(live.Terms(sender, recipient));
        live.Reopen();
        Assert.Equal(proposal.ContentHash, live.Book.FindOpen(proposal.ProposalId)!.ContentHash);
        WorldActionCommand accept = live.Acceptance(proposal);
        WorldActionReceipt receipt = live.Service.Submit(accept, live.Delivery(recipient));
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, live.Book.FindOutcome(proposal.ProposalId)!.Kind);
        var material = live.State.Inventory.ContentHash;
        var complete = live.State.ContentHash;
        live.Reopen();
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
            WorldReceiptCodecV1.Encode(live.Service.Submit(accept, live.Delivery(recipient))));
        Assert.Equal((material, complete), (live.State.Inventory.ContentHash, live.State.ContentHash));
        Assert.True(live.Authority.IsIdentityOccupiedForHost(proposal.CreationCommandId));
        Assert.True(live.Authority.IsIdentityOccupiedForHost(accept.CommandId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distinct_stock_offers_share_one_sender_opportunity_and_a_refusal_does_not_spend_it(bool firstIsStale)
    {
        var company = new ConsentedCompanyFixture();
        using var live = new MaterialProposalTestSession(company.Create());
        // A real earlier gift gives the sender a second independent lot, without manufacturing stock.
        WorldMaterialProposal supplied = live.Save(live.Terms(company.Second, company.First));
        Assert.Equal(WorldActionRejection.None, live.Accept(supplied).Rejection);
        EntityId received = live.Book.FindOutcome(supplied.ProposalId)!.Settlement!.MovedLotId;
        EntityId original = live.State.Inventory.Custody.Lots.First(value =>
            value.CustodianId == live.State.GetResident(company.First).InventoryCustodianId
            && value.LotId != received).LotId;
        WorldMaterialProposal first = live.Save(live.Terms(company.First, live.Actions.Player, original));
        WorldMaterialProposal second = live.Save(live.Terms(company.First, company.Second, received));
        WorldSnapshot before = live.State;
        WorldActionCommand[] commands = [live.Acceptance(first), live.Acceptance(second)];
        var processor = new WorldStepProcessor();
        // Each acceptance is independently valid from exactly the same real root, against different lots.
        foreach (WorldActionCommand command in commands)
        {
            WorldStepDecision alone = processor.Decide(before,
                live.Actions.Batch(before, WorldBatchKind.AdvancingStep, [command]), live.Authority.IsIdentityOccupiedForHost);
            Assert.Equal(WorldActionRejection.None, Assert.Single(alone.Receipts).Rejection);
        }
        commands = commands.OrderBy(value => value.ActorId).ToArray();
        if (firstIsStale)
        {
            WorldActionCommand current = commands[0];
            commands[0] = new(current.CommandId, current.PrincipalId, current.OriginSessionId, current.ActorId,
                current.WorldId, current.BranchId, current.ExpectedActorRevision + 1, current.Intent);
        }
        WorldStepDecision result = live.Authority.Commit(live.Actions.Batch(before, WorldBatchKind.AdvancingStep,
            commands.Reverse()));
        var reasons = commands.Select(command => result.Receipts.Single(value => value.CommandId == command.CommandId).Rejection).ToArray();
        Assert.Equal(firstIsStale ? [WorldActionRejection.RevisionConflict, WorldActionRejection.None]
            : new[] { WorldActionRejection.None, WorldActionRejection.Unavailable }, reasons);
        EntityId accepted = ((AcceptMaterialProposalIntent)commands[firstIsStale ? 1 : 0].Intent).ProposalId;
        EntityId pending = ((AcceptMaterialProposalIntent)commands[firstIsStale ? 0 : 1].Intent).ProposalId;
        WorldMaterialProposalOutcome settled = live.Book.FindOutcome(accepted)!;
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, settled.Kind);
        Assert.Equal(settled.Proposal.Terms.DestinationCustodianId,
            live.State.Inventory.Custody.GetLot(settled.Settlement!.MovedLotId).CustodianId);
        Assert.NotNull(live.Book.FindOpen(pending));
        Assert.Null(live.Book.FindOutcome(pending));
        Assert.Equal(1, live.Book.Outcomes.Count(value => value.WorldSequence == live.State.Revision));
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1), (live.State.Revision, live.State.AdvancingSteps));
        live.Reopen();
        Assert.NotNull(live.Book.FindOpen(pending));
        Assert.NotNull(live.Book.FindOutcome(accepted));
    }

    [Fact]
    public void A_nearby_ready_animal_without_manipulation_cannot_accept_but_a_human_can()
    {
        var company = new ConsentedCompanyFixture();
        using var live = new MaterialProposalTestSession(company.Create());
        EntityId sender = live.Actions.Player, wolf = live.Actions.First.TargetActorId;
        Assert.True(WorldActorAvailability.CanAct(live.State, sender));
        Assert.True(WorldActorAvailability.CanAct(live.State, wolf));
        Assert.Equal(OpenFantasy.Core.Spatial.SpatialInteraction.Reachable, live.State.Space.QueryInteraction(sender, wolf));
        Assert.Equal(OpenFantasy.Core.Spatial.SpatialInteraction.Reachable, live.State.Space.QueryInteraction(wolf, sender));
        Assert.False(live.State.Space.GetActor(wolf).Condition.SupportsFunction(BodyFunction.Manipulation));
        WorldMaterialProposal proposal = live.Save(live.Terms(sender, wolf));
        WorldSnapshot before = live.State;
        Assert.Equal(WorldActionRejection.Unavailable, live.Accept(proposal).Rejection);
        Assert.Equal((before.Inventory.ContentHash, before.Production!.ContentHash, before.Coordination!.Proposals!.ContentHash),
            (live.State.Inventory.ContentHash, live.State.Production!.ContentHash, live.Book.ContentHash));
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1), (live.State.Revision, live.State.AdvancingSteps));
        WorldMaterialProposal human = live.Save(live.Terms(sender, company.First, proposal.Terms.LotId));
        Assert.Equal(WorldActionRejection.None, live.Accept(human).Rejection);
        Assert.NotNull(live.Book.FindOpen(proposal.ProposalId));
    }

    [Fact]
    public void Tactical_time_expires_an_open_offer_and_exploration_records_its_outcome_without_settling()
    {
        var company = new ConsentedCompanyFixture();
        using var live = new MaterialProposalTestSession(company.Create());
        WorldMaterialProposal expiring = live.Save(live.Terms(company.First, company.Second), lifetime: 2);
        var session = new CareWorldSession(live.Authority);
        EnterFirst(live);
        live.Actions.WinAndRelease(session);
        Assert.True(live.State.AdvancingSteps >= expiring.ExpiresAtAdvancingStep);
        Assert.NotNull(live.Book.FindOpen(expiring.ProposalId));
        Assert.Null(live.Book.FindOutcome(expiring.ProposalId));
        Assert.Equal(MaterialProposalStatus.Expired,
            live.Service.Find(expiring.ProposalId, company.Second, live.Delivery(company.Second))!.Status);
        live.Reopen();
        Assert.NotNull(live.Book.FindOpen(expiring.ProposalId));
        Assert.True(WorldActorAvailability.CanAct(live.State, company.First));
        Assert.True(WorldActorAvailability.CanAct(live.State, company.Second));
        WorldSnapshot before = live.State;
        Assert.Equal(WorldActionRejection.Unavailable, live.Accept(expiring).Rejection);
        Assert.Equal((before.Inventory.ContentHash, before.Production!.ContentHash),
            (live.State.Inventory.ContentHash, live.State.Production!.ContentHash));
        WorldMaterialProposalOutcome expired = live.Book.FindOutcome(expiring.ProposalId)!;
        Assert.Equal((WorldMaterialProposalOutcomeKind.Expired, live.State.Revision), (expired.Kind, expired.WorldSequence));
        Assert.Null(expired.TerminalCommandId);
        Assert.Null(live.Book.FindOpen(expiring.ProposalId));
        live.Reopen();
        Assert.Equal(WorldMaterialProposalOutcomeKind.Expired, live.Book.FindOutcome(expiring.ProposalId)!.Kind);
    }

    [Fact]
    public void Actual_post_encounter_recovery_blocks_a_gift_until_the_sender_is_ready()
    {
        var company = new ConsentedCompanyFixture();
        using var live = new MaterialProposalTestSession(company.Create());
        WorldMaterialProposal awaitingReadiness = live.Save(live.Terms(live.Actions.Player, company.First));
        EnterFirst(live);
        live.Actions.WinAndRelease(new CareWorldSession(live.Authority));
        // Inspect the debt before any exploration step can pay its recovery cost.
        Assert.True(live.State.EncounterLifecycle!.GetDebt(live.Actions.Player).RemainingDelay > 0);
        Assert.False(WorldActorAvailability.CanAct(live.State, live.Actions.Player));
        Assert.True(WorldActorAvailability.CanAct(live.State, company.First));
        WorldSnapshot before = live.State;
        Assert.Equal(WorldActionRejection.Unavailable, live.Accept(awaitingReadiness).Rejection);
        Assert.Equal((before.Inventory.ContentHash, before.Production!.ContentHash, before.Coordination!.Proposals!.ContentHash),
            (live.State.Inventory.ContentHash, live.State.Production!.ContentHash, live.Book.ContentHash));
        for (int step = 0; !WorldActorAvailability.CanAct(live.State, live.Actions.Player); step++)
        {
            Assert.True(step < 32, "The actual existing recovery debt must retire within the journey's retained bound.");
            live.Step();
        }
        Assert.Equal(WorldActionRejection.None, live.Accept(awaitingReadiness).Rejection);
        live.Reopen();
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, live.Book.FindOutcome(awaitingReadiness.ProposalId)!.Kind);
    }

    private static void EnterFirst(MaterialProposalTestSession live)
    {
        WorldSnapshot state = live.State;
        var scenario = live.Actions.First;
        WorldActionCommand enter = live.Actions.Command(state, live.Actions.Player, new EnterEncounterIntent(scenario.DefinitionId,
            EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
            state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            live.Actions.Batch(state, WorldBatchKind.EncounterLifecycle, [enter])).Receipts).Rejection);
        for (int stage = 0; stage < 2; stage++) live.Authority.Commit(live.Actions.Batch(live.State, WorldBatchKind.EncounterLifecycle));
        Assert.Equal(EncounterLifecyclePhase.Active, live.State.EncounterLifecycle!.Phase);
    }
}

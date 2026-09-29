using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialNpcTests(MaterialProposalWorldFixture world) : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public async Task An_explicit_helper_policy_offers_actual_medicine_for_later_player_acceptance_and_use()
    {
        var prepared = CreateCraftedMedicineJourney();
        using var live = prepared.Live;
        var actions = prepared.Actions;
        WorldSnapshot before = live.State;
        EntityId helperBag = before.GetResident(actions.Helper).InventoryCustodianId;
        var preferred = before.Production!.Lots.Where(value => value.CustodianId == helperBag
            && value.Definition == before.Care!.Remedies.Single().Item).OrderBy(value => value.LotId).First();
        var policy = Policy(before, actions.Helper, actions.Player);
        var policies = new List<LocalMaterialGiftPolicy> { policy };
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, actions.Player, live.Delivery(actions.Player), time, policies);
        policies.Clear(); // The running preference is owned and immutable, not the caller's collection.
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        session.SetReady(true);
        await Tick(time);
        WorldSnapshot offered = live.State;
        WorldMaterialProposal proposal = Assert.Single(live.Book.Open);
        Assert.Equal((actions.Helper, actions.Player, preferred.LotId, 1UL),
            (proposal.Terms.SenderActorId, proposal.Terms.RecipientActorId, proposal.Terms.LotId, proposal.Terms.Quantity));
        Assert.Equal(before.Inventory.ContentHash, offered.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, offered.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, offered.Progression!.ContentHash);
        Assert.Equal(before.AdvancingSteps + 1, offered.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), offered.Calendar);
        Assert.Equal(live.Delivery(actions.Helper), proposal.SenderDelivery);
        WorldActionCommand creation = actions.Command(before, actions.Helper,
            new ProposeMaterialIntent(proposal.Terms, proposal.ExpiresAtAdvancingStep));
        creation = new(proposal.CreationCommandId, creation.PrincipalId, creation.OriginSessionId, creation.ActorId,
            creation.WorldId, creation.BranchId, creation.ExpectedActorRevision, creation.Intent);
        WorldActionReceipt created = live.Service.FindReceipt(creation, live.Delivery(actions.Helper))!;
        Assert.Equal(WorldActionRejection.None, created.Rejection);

        var accept = actions.Command(live.State, actions.Player, Assert.Single(session.ReadView().MaterialProposals!.Offers).PrepareAcceptance());
        WorldActionReceipt accepted = await ScheduledMaterialProposalSessionTests.Submit(session, time, accept);
        Assert.Equal(WorldActionRejection.None, accepted.Rejection);
        WorldSnapshot received = live.State;
        Assert.Equal(offered.Progression.ContentHash, received.Progression!.ContentHash);
        EntityId child = live.Book.FindOutcome(proposal.ProposalId)!.Settlement!.MovedLotId;
        Assert.Equal(preferred.LotId, child); // This actual crafted lot contains one dose, so it moves intact.
        var lot = Assert.Single(session.ReadView().PlayerInventory.Lots, value => value.LotId == child);
        var available = Assert.Single(session.ReadView().Restorative!.SelfUseOffers).Intent!;
        var use = actions.Command(live.State, actions.Player, new UseRestorativeItemIntent(available.RecipientActorId,
            available.UseDefinitionId, available.UseDefinitionVersion, available.PermissionId, available.PermissionGrantVersion,
            available.ExpectedPermissionRevision, available.ExpectedPatientPresenceRevision, available.ExpectedPatientProgressionRevision,
            available.ExpectedStorageRevision, available.ExpectedProductionRevision,
            new(lot.Provenance, lot.LotId, lot.Revision, 1)));
        Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, use)).Rejection);
        WorldSnapshot after = live.State;
        Assert.Equal(before.AdvancingSteps + 3, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(5400), after.Calendar);
        Assert.True(after.Progression!.GetActor(actions.Player).CurrentHp > before.Progression.GetActor(actions.Player).CurrentHp);
        var consumed = Assert.Single(after.Production!.ConsumedItems, value => value.CommandId == use.CommandId);
        Assert.Equal((child, preferred.Output.BatchId, 1UL), (consumed.Ingredient.LotId, consumed.Ingredient.Output!.BatchId, consumed.Ingredient.Quantity));
        Assert.True(after.Production.Lots.Where(value => value.CustodianId == helperBag)
            .Aggregate(0UL, (total, value) => total + value.Quantity) >= policy.RetainedDoses);
        await session.StopAsync();
        live.Reopen();
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(live.State));
        Assert.Equal(WorldReceiptCodecV1.Encode(created), WorldReceiptCodecV1.Encode(
            live.Service.FindReceipt(creation, live.Delivery(actions.Helper))!));
        Assert.Equal(WorldReceiptCodecV1.Encode(accepted), WorldReceiptCodecV1.Encode(
            live.Service.FindReceipt(accept, live.Delivery(actions.Player))!));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Only_an_explicit_recipient_policy_accepts_wanted_or_declines_unwanted_player_offers(bool medicine, bool configured)
    {
        using var live = new MaterialProposalTestSession(new OrdinaryCareWorldFixture().Genesis);
        live.Actions.Approach(new(live.Authority));
        var state = live.State;
        var remedy = state.Care!.Remedies.Single().Item;
        var lot = state.Inventory.Custody.Lots.First(value => value.CustodianId == state.GetResident(live.Actions.Player).InventoryCustodianId
            && (value.Definition == remedy) == medicine);
        var policy = Policy(state, live.Actions.Helper, live.Actions.Player, offers: false);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, live.Actions.Player, live.Delivery(live.Actions.Player), time,
            configured ? [policy] : []);
        session.SetReady(true);
        var create = live.Actions.Command(state, live.Actions.Player,
            new ProposeMaterialIntent(live.Terms(live.Actions.Player, live.Actions.Helper, lot.LotId), state.AdvancingSteps + 64));
        Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, create)).Rejection);
        var proposal = Assert.Single(live.Book.Open);
        WorldSnapshot beforeAnswer = live.State;
        await Tick(time);
        if (configured)
        {
            WorldMaterialProposalOutcome result = Assert.Single(live.Book.Outcomes);
            Assert.Equal(medicine ? WorldMaterialProposalOutcomeKind.Accepted : WorldMaterialProposalOutcomeKind.Declined, result.Kind);
            Assert.Equal(LocalMaterialGiftIdentity.Answer(beforeAnswer, policy, proposal.ProposalId), result.TerminalCommandId);
            Assert.Empty(live.Book.Open);
            if (medicine)
            {
                Assert.Equal(beforeAnswer.GetResident(live.Actions.Helper).InventoryCustodianId,
                    live.State.Inventory.Custody.GetLot(lot.LotId).CustodianId);
            }
            else
            {
                Assert.Equal(beforeAnswer.Inventory.ContentHash, live.State.Inventory.ContentHash);
            }
        }
        else
        {
            Assert.NotNull(live.Book.FindOpen(proposal.ProposalId));
            Assert.Empty(live.Book.Outcomes);
            Assert.Equal(beforeAnswer.Inventory.ContentHash, live.State.Inventory.ContentHash);
        }
        Assert.Equal(beforeAnswer.Progression!.ContentHash, live.State.Progression!.ContentHash);
        Assert.Equal(beforeAnswer.AdvancingSteps + 1, live.State.AdvancingSteps);
        var after = live.State;
        await session.StopAsync();
        live.Reopen();
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(live.State));
    }

    [Fact]
    public void Invalid_or_ambiguous_configuration_does_not_start_a_session_or_grant_control()
    {
        using var live = world.Open();
        WorldSnapshot state = live.State;
        var policy = Policy(state, world.Helper, world.Player);
        foreach (LocalMaterialGiftPolicy[] choices in new[]
        {
            new[] { policy, policy }, new[] { Policy(state, world.Player, world.Helper) },
            new[] { new LocalMaterialGiftPolicy(world.Helper, world.Player, world.Player, policy.SelfUseVersion) }
        })
        {
            Assert.Throws<AuthorityDispatchException>(() => new LocalWorldSession(live.Authority, world.Player,
                world.DeliveryOf(state, world.Player), new ManualSessionTimeProvider(), choices));
            Assert.Equal(state.ContentHash, live.State.ContentHash);
        }
        Assert.Throws<ArgumentException>(() => new LocalMaterialGiftPolicy(world.Helper, world.Player,
            policy.SelfUseDefinitionId, policy.SelfUseVersion, retainedDoses: 0));
    }

    [Fact]
    public async Task An_outstanding_gift_waits_for_the_player_instead_of_becoming_automatic_treatment()
    {
        var prepared = CreateCraftedMedicineJourney();
        using var live = prepared.Live;
        var actions = prepared.Actions;
        var policy = Policy(live.State, actions.Helper, actions.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, actions.Player,
            live.Delivery(actions.Player), time, [policy]);
        Assert.True(live.State.Care!.Ordinary!.Permissions.Single().IsActive);
        session.SetReady(true);
        await Tick(time);
        var offer = Assert.Single(live.Book.Open);
        var stock = live.State.Production!.ContentHash;
        var health = live.State.Progression!.ContentHash;
        for (int step = 0; step < 3; step++)
        {
            Assert.Equal(OpenFantasy.Core.Spatial.SpatialInteraction.Reachable,
                live.State.Space.QueryInteraction(actions.Helper, actions.Player));
            await Tick(time);
            Assert.NotNull(live.Book.FindOpen(offer.ProposalId));
            Assert.Equal(stock, live.State.Production!.ContentHash);
            Assert.Equal(health, live.State.Progression!.ContentHash);
        }
    }

    [Fact]
    public void Deferring_a_supplier_neither_excludes_him_as_patient_nor_changes_permission()
    {
        var prepared = CreateCraftedMedicineJourney();
        using var live = prepared.Live;
        var controller = new LocalRestorativeController();
        var state = live.State;
        var ordinary = Assert.Single(controller.SelectActions(state, live.Authority.IsIdentityOccupiedForHost, new HashSet<EntityId>()));
        var withPatientDeferred = Assert.Single(controller.SelectActions(state, live.Authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>(), new HashSet<EntityId> { prepared.Actions.Player }));
        Assert.Equal(WorldActionCodec.Encode(ordinary), WorldActionCodec.Encode(withPatientDeferred));
        Assert.Empty(controller.SelectActions(state, live.Authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>(), new HashSet<EntityId> { prepared.Actions.Helper }));
        Assert.Equal(state.ContentHash, live.State.ContentHash);
        Assert.True(live.State.Care!.Ordinary!.Permissions.Single().IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_invalid_partner_request_cannot_suppress_the_npcs_enabled_offer_or_decline(bool answers)
    {
        using var live = world.Open();
        var state = live.State;
        var raw = state.Inventory.Custody.Lots.First(value => value.CustodianId == world.PlayerBag
            && value.Definition != state.Care!.Remedies.Single().Item);
        var terms = new OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialTerms(state.WorldId, state.BranchId,
            world.Player, world.Helper, OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind.CountedSource,
            raw.LotId, raw.Definition, world.PlayerBag, world.HelperBag, 1, raw.Revision);
        var incoming = live.Save(terms);
        var policy = Policy(live.State, world.Helper, world.Player, answers: answers);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player, world.DeliveryOf(live.State, world.Player), time, [policy]);
        session.SetReady(true);
        var invalid = live.Command(world.Player, new DeclineMaterialProposalIntent(world.Helper));
        var receipt = await ScheduledMaterialProposalSessionTests.Submit(session, time, invalid);
        Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
        if (answers)
        {
            Assert.Equal(WorldMaterialProposalOutcomeKind.Declined, live.Book.FindOutcome(incoming.ProposalId)!.Kind);
            Assert.Empty(live.Book.Open);
        }
        else
        {
            Assert.NotNull(live.Book.FindOpen(incoming.ProposalId));
            Assert.Single(live.Book.Open, value => value.Terms.SenderActorId == world.Helper);
        }
        Assert.Equal(state.Progression!.ContentHash, live.State.Progression!.ContentHash);
    }

    internal static (MaterialProposalTestSession Live, CareRewardJourney Actions) CreateCraftedMedicineJourney()
    {
        var live = new MaterialProposalTestSession(new OrdinaryCareWorldFixture().Genesis);
        try
        {
            var ordinary = new OrdinaryCareWorldFixture(live.State);
            var journey = new CareWorldSession(live.Authority);
            ordinary.ReachFullDoseInjuredExploration(journey);
            ordinary.Supply(journey, self: false);
            ordinary.Supply(journey, self: false);
            Assert.DoesNotContain(live.State.Inventory.Custody.Lots, value =>
                value.CustodianId == live.State.GetResident(ordinary.Helper).InventoryCustodianId
                && value.Definition == live.State.Care!.Remedies.Single().Item);
            return (live, ordinary.Journey);
        }
        catch
        {
            live.Dispose();
            throw;
        }
    }

    internal static LocalMaterialGiftPolicy Policy(WorldSnapshot state, EntityId actor, EntityId partner,
        bool offers = true, bool answers = true, ulong lifetime = 64, ulong retained = 1)
    {
        var self = state.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse);
        return new(actor, partner, self.DefinitionId, self.Version, offers, answers, retained, offerLifetimeSteps: lifetime);
    }
}

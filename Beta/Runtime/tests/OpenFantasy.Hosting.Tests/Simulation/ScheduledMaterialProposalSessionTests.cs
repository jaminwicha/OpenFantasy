using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialProposalSessionTests(MaterialProposalWorldFixture world)
    : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public async Task Accepted_owned_medicine_is_used_on_a_later_cutoff_and_reopens_with_exact_receipts()
    {
        using var live = world.Open();
        var stock = live.State.Production!.Lots.Single(value => value.LotId == world.Stock);
        var terms = new ConsentedMaterialTerms(live.State.WorldId, live.State.BranchId, world.Helper, world.Player,
            ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, world.HelperBag, world.PlayerBag, 1, stock.Revision);
        var offered = live.Save(terms);
        byte[] creation = WorldReceiptCodecV1.Encode(live.Service.FindReceipt(offered.Command, offered.Delivery)!);
        WorldSnapshot before = live.State;
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player, world.DeliveryOf(before, world.Player), time);
        MaterialProposalView view = Assert.Single(session.ReadView().MaterialProposals!.Offers);
        Assert.Equal((offered.ProposalId, MaterialProposalStatus.Open, false), (view.ProposalId, view.Status, view.ViewerIsSender));
        WorldActionCommand accept = live.Command(world.Player, view.PrepareAcceptance());
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(accept));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        session.SetReady(true);
        session.SetMovement(32, 0);
        Task<WorldActionReceipt> selected = session.SubmitAction(accept);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(accept);
        Assert.False(selected.IsCompleted);
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        await Tick(time);
        WorldActionReceipt receipt = await selected;
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await duplicate));
        WorldSnapshot received = live.State;
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1, before.Calendar.AddMilliseconds(1800)),
            (received.Revision, received.AdvancingSteps, received.Calendar));
        Assert.Equal(before.Space.GetActor(world.Player).Feet, received.Space.GetActor(world.Player).Feet);
        Assert.Equal(before.Space.GetActor(world.Helper).Feet, received.Space.GetActor(world.Helper).Feet);
        Assert.Equal(before.Progression!.ContentHash, received.Progression!.ContentHash);
        Assert.Equal(before.Care!.ContentHash, received.Care!.ContentHash);
        Assert.Equal(MaterialProposalStatus.Accepted, Assert.Single(session.ReadView().MaterialProposals!.Offers).Status);

        EntityId child = EntityId.DeriveSpawned(offered.Command.CommandId, 0);
        LocalItemLotView owned = Assert.Single(session.ReadView().PlayerInventory.Lots, value => value.LotId == child);
        var available = Assert.Single(session.ReadView().Restorative!.SelfUseOffers).Intent!;
        Assert.NotNull(available);
        var exactDose = new UseRestorativeItemIntent(available.RecipientActorId, available.UseDefinitionId,
            available.UseDefinitionVersion, available.PermissionId, available.PermissionGrantVersion,
            available.ExpectedPermissionRevision, available.ExpectedPatientPresenceRevision,
            available.ExpectedPatientProgressionRevision, available.ExpectedStorageRevision, available.ExpectedProductionRevision,
            new(owned.Provenance, owned.LotId, owned.Revision, 1));
        WorldActionCommand use = live.Command(world.Player, exactDose);
        Task<WorldActionReceipt> usingDose = session.SubmitAction(use);
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await usingDose).Rejection);
        WorldSnapshot after = live.State;
        Assert.Equal((received.Revision + 1, received.AdvancingSteps + 1), (after.Revision, after.AdvancingSteps));
        Assert.True(after.Progression!.GetActor(world.Player).CurrentHp > received.Progression!.GetActor(world.Player).CurrentHp);
        var spent = Assert.Single(after.Production!.ConsumedItems, value => value.CommandId == use.CommandId);
        Assert.Equal((child, world.Producer, 1UL), (spent.Ingredient.LotId, spent.Ingredient.Output!.BatchId, spent.Ingredient.Quantity));
        session.SetReady(false);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(accept)));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(after.ContentHash, live.State.ContentHash);
        await session.StopAsync();
        live.Reopen();
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(live.State));
        Assert.Equal(creation, WorldReceiptCodecV1.Encode(live.Service.FindReceipt(offered.Command, offered.Delivery)!));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(live.Service.FindReceipt(accept, world.Actions.Delivery(accept))!));
    }

    [Fact]
    public async Task The_player_creates_withdraws_and_declines_through_cutoffs_without_seeing_another_pairs_offer()
    {
        var company = new ConsentedCompanyFixture();
        using var live = new MaterialProposalTestSession(company.Create());
        WorldMaterialProposal foreign = live.Save(live.Terms(company.First, company.Second));
        WorldMaterialProposal incoming = live.Save(live.Terms(company.First, live.Actions.Player));
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, live.Actions.Player, live.Delivery(live.Actions.Player), time);
        var before = live.State.ContentHash;
        Assert.Equal(incoming.ProposalId, Assert.Single(session.ReadView().MaterialProposals!.Offers).ProposalId);
        Assert.DoesNotContain(session.ReadView().MaterialProposals!.Offers, value => value.ProposalId == foreign.ProposalId);
        Assert.Equal(before, live.State.ContentHash);
        session.SetReady(true);
        var create = live.Actions.Command(live.State, live.Actions.Player,
            new ProposeMaterialIntent(live.Terms(live.Actions.Player, company.First), live.State.AdvancingSteps + 64));
        Assert.Equal(WorldActionRejection.None, (await Submit(session, time, create)).Rejection);
        EntityId own = WorldMaterialProposal.DeriveIdentity(create.CommandId);
        Assert.True(session.ReadView().MaterialProposals!.Offers.Single(value => value.ProposalId == own).ViewerIsSender);
        var withdraw = live.Actions.Command(live.State, live.Actions.Player, new WithdrawMaterialProposalIntent(own));
        Assert.Equal(WorldActionRejection.None, (await Submit(session, time, withdraw)).Rejection);
        var decline = live.Actions.Command(live.State, live.Actions.Player, new DeclineMaterialProposalIntent(incoming.ProposalId));
        Assert.Equal(WorldActionRejection.None, (await Submit(session, time, decline)).Rejection);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, live.Book.FindOutcome(own)!.Kind);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Declined, live.Book.FindOutcome(incoming.ProposalId)!.Kind);
        Assert.NotNull(live.Book.FindOpen(foreign.ProposalId));

        var wrongActor = live.Actions.Command(live.State, company.First, new WithdrawMaterialProposalIntent(foreign.ProposalId));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(wrongActor));
        Assert.False(session.Completion.IsCompleted);
        var pending = live.Actions.Command(live.State, live.Actions.Player, new DeclineMaterialProposalIntent(foreign.ProposalId));
        var waiting = session.SubmitAction(pending);
        var pendingRoot = live.State.ContentHash;
        await session.StopAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        live.Reopen();
        Assert.Equal(pendingRoot, live.State.ContentHash);
        Assert.Null(live.Service.FindReceipt(pending, live.Delivery(live.Actions.Player)));
    }

    [Fact]
    public async Task A_reading_hold_refuses_offer_selection_without_faulting_and_closes_without_elapsed_time()
    {
        using var live = world.Open();
        var state = live.State;
        var speak = live.Command(world.Player, new SpeakToActorIntent(world.Helper, state.Space.GetActor(world.Helper).Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            world.Actions.Batch(state, WorldBatchKind.AdvancingStep, [speak])).Receipts).Rejection);
        var held = live.State;
        Assert.True(held.HasReadingHold);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player, world.DeliveryOf(held, world.Player), time);
        session.SetReady(true);
        foreach (WorldActionIntent intent in new WorldActionIntent[]
        {
            new ProposeMaterialIntent(world.PinnedRawTerms(held, world.Player, world.Helper), held.AdvancingSteps + 64),
            new DeclineMaterialProposalIntent(speak.CommandId), new WithdrawMaterialProposalIntent(speak.CommandId)
        })
        {
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(live.Command(world.Player, intent)));
        }
        await Tick(time);
        Assert.Equal(held.ContentHash, live.State.ContentHash);
        Assert.False(session.Completion.IsCompleted);
        var close = live.Command(world.Player, new CloseDialogueIntent(held.Dialogues.Single().DialogueId));
        Assert.Equal(WorldActionRejection.None, (await Submit(session, time, close)).Rejection);
        Assert.Equal(held.Calendar, live.State.Calendar);
        Assert.False(live.State.HasReadingHold);
    }

    [Fact]
    public async Task An_older_profile_refuses_proposals_before_queueing_and_its_session_keeps_running()
    {
        var ordinary = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, ordinary.Genesis);
        var state = authority.ReadStateForHost();
        var command = ordinary.Journey.Command(state, ordinary.Player,
            new DeclineMaterialProposalIntent(ordinary.Helper));
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, ordinary.Player, ordinary.Journey.Delivery(command), time);
        Assert.Null(session.ReadView().MaterialProposals);
        session.SetReady(true);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(command));
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
        await Tick(time);
        Assert.Equal(state.Revision + 1, authority.ReadStateForHost().Revision);
        Assert.False(session.Completion.IsCompleted);
    }

    internal static async Task<WorldActionReceipt> Submit(LocalWorldSession session,
        ManualSessionTimeProvider time, WorldActionCommand command)
    {
        Task<WorldActionReceipt> pending = session.SubmitAction(command);
        await Tick(time);
        await Until(() => pending.IsCompleted);
        return await pending;
    }
}

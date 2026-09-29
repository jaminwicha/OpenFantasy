using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialNpcPersistenceTests(MaterialProposalWorldFixture world) : IClassFixture<MaterialProposalWorldFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Declined_or_expired_gifts_do_not_repeat_after_ticks_movement_archive_or_reopen(bool expire)
    {
        using var live = world.Open();
        Assert.Empty(live.State.Care!.Ordinary!.Permissions);
        var policy = ScheduledMaterialNpcTests.Policy(live.State, world.Helper, world.Player, answers: false, lifetime: expire ? 2UL : 64UL);
        var time = new ManualSessionTimeProvider();
        WorldMaterialProposal original;
        await using (var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(live.State, world.Player), time, [policy]))
        {
            session.SetReady(true);
            await Tick(time);
            original = Assert.Single(live.Book.Open);
            if (expire)
            {
                await Tick(time);
                await Tick(time);
            }
            else
            {
                var decline = live.Command(world.Player, new DeclineMaterialProposalIntent(original.ProposalId));
                Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, decline)).Rejection);
            }
            Assert.Equal(expire ? WorldMaterialProposalOutcomeKind.Expired : WorldMaterialProposalOutcomeKind.Declined,
                live.Book.FindOutcome(original.ProposalId)!.Kind);
            var health = live.State.Progression!.ContentHash;
            for (int step = 0; step < 4; step++)
            {
                session.SetMovement(32, 0);
                await Tick(time);
                Assert.Empty(live.Book.Open);
                Assert.Equal(health, live.State.Progression!.ContentHash);
            }
            session.SetMovement(0, 0);

            if (!expire)
            {
                ulong archives = live.Authority.InspectForHost().Journal.MaterialCareArchiveCount;
                for (int cycle = 0; live.Authority.InspectForHost().Journal.MaterialCareArchiveCount == archives; cycle++)
                {
                    Assert.True(cycle <= WorldMaterialProposalBook.MaximumOutcomes, "Real outcome pressure must close one complete segment within its existing window.");
                    WorldSnapshot state = live.State;
                    var create = live.Command(world.Player, new ProposeMaterialIntent(
                        world.PinnedRawTerms(state, world.Player, world.Helper), state.AdvancingSteps + 64));
                    Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, create)).Rejection);
                    var withdraw = live.Command(world.Player, new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(create.CommandId)));
                    Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, time, withdraw)).Rejection);
                }
                Assert.Equal(archives + 1, live.Authority.InspectForHost().Journal.MaterialCareArchiveCount);
                Assert.Null(live.Book.FindOutcome(original.ProposalId));
                Assert.Empty(live.Book.Open);
            }
            Assert.Equal(health, live.State.Progression!.ContentHash);
        }
        live.Reopen();
        // Restore actual reach through ordinary movement; an unreachable helper would prove no retry only vacuously.
        world.Actions.Approach(new(live.Authority));
        Assert.Equal(SpatialInteraction.Reachable, live.State.Space.QueryInteraction(world.Helper, world.Player));
        Assert.Equal(original.CreationCommandId, LocalMaterialGiftIdentity.Offer(live.State, policy, original.Terms));
        Assert.True(live.Authority.IsIdentityOccupiedForHost(original.CreationCommandId));
        var clock = new ManualSessionTimeProvider();
        await using var resumed = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(live.State, world.Player), clock, [policy]);
        ulong receipts = live.Authority.InspectForHost().Journal.CommandOutcomeCount;
        resumed.SetReady(true);
        await Tick(clock);
        Assert.Empty(live.Book.Open);
        Assert.Equal(receipts, live.Authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Equal(expire ? MaterialProposalStatus.Expired : MaterialProposalStatus.Declined,
            live.Service.FindRecorded(original.ProposalId, world.Player, world.DeliveryOf(live.State, world.Player))!.Status);
    }

    [Fact]
    public async Task A_refused_wanted_acceptance_keeps_its_one_decision_identity_when_stock_is_already_spent()
    {
        var prepared = ScheduledMaterialNpcTests.CreateCraftedMedicineJourney();
        using var live = prepared.Live;
        var actions = prepared.Actions;
        var permission = live.State.Care!.Ordinary!.Permissions.Single();
        var revoke = actions.Command(live.State, actions.Player,
            new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            actions.Batch(live.State, WorldBatchKind.AdvancingStep, [revoke])).Receipts).Rejection);
        var before = live.State;
        var medicine = before.Care!.Remedies.Single().Item;
        var dose = before.Inventory.Custody.Lots.Single(value => value.CustodianId == before.GetResident(actions.Player).InventoryCustodianId && value.Definition == medicine);
        var terms = new OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialTerms(before.WorldId, before.BranchId,
            actions.Player, actions.Helper, OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind.CountedSource,
            dose.LotId, dose.Definition, before.GetResident(actions.Player).InventoryCustodianId, before.GetResident(actions.Helper).InventoryCustodianId, 1, dose.Revision);
        var creation = actions.Command(live.State, actions.Player, new ProposeMaterialIntent(terms, live.State.AdvancingSteps + 4097));
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            actions.Batch(live.State, WorldBatchKind.AdvancingStep, [creation])).Receipts).Rejection);
        EntityId proposalId = WorldMaterialProposal.DeriveIdentity(creation.CommandId);
        var useDefinition = before.Care.Ordinary!.Definitions.Single(value => value.DeliveryMode == OpenFantasy.Core.Care.RestorativeDeliveryMode.SelfUse);
        var use = actions.Command(live.State, actions.Player, WorldRestorativeActions.Prepare(live.State, actions.Player, actions.Player,
            useDefinition.DefinitionId, null)!);
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            actions.Batch(live.State, WorldBatchKind.AdvancingStep, [use])).Receipts).Rejection);
        Assert.DoesNotContain(live.State.Inventory.Custody.Lots, value => value.LotId == dose.LotId);
        var policy = new LocalMaterialGiftPolicy(actions.Helper, actions.Player, useDefinition.DefinitionId,
            useDefinition.Version, offersMedicine: false, desiredDoses: 64);
        var proposal = live.Book.FindOpen(proposalId)!;
        EntityId identity = LocalMaterialGiftIdentity.Answer(live.State, policy, proposal.ProposalId);
        var answer = actions.Command(live.State, actions.Helper,
            AcceptMaterialProposalIntent.Approving(proposal, live.State.Space.GetActor(actions.Player).Revision));
        answer = new(identity, answer.PrincipalId, answer.OriginSessionId, answer.ActorId,
            answer.WorldId, answer.BranchId, answer.ExpectedActorRevision, answer.Intent);
        var time = new ManualSessionTimeProvider();
        await using (var session = new LocalWorldSession(live.Authority, actions.Player,
            live.Delivery(actions.Player), time, [policy]))
        {
            session.SetReady(true);
            await Tick(time);
            var receipt = live.Service.FindReceipt(answer, live.Delivery(actions.Helper));
            Assert.NotNull(receipt);
            Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
            ulong count = live.Authority.InspectForHost().Journal.CommandOutcomeCount;
            for (int step = 0; step < 4; step++)
            {
                await Tick(time);
                Assert.Equal(count, live.Authority.InspectForHost().Journal.CommandOutcomeCount);
            }
            Assert.NotNull(live.Book.FindOpen(proposal.ProposalId));
        }
        live.Reopen();
        var clock = new ManualSessionTimeProvider();
        await using var resumed = new LocalWorldSession(live.Authority, actions.Player,
            live.Delivery(actions.Player), clock, [policy]);
        ulong afterReopen = live.Authority.InspectForHost().Journal.CommandOutcomeCount;
        resumed.SetReady(true);
        await Tick(clock);
        Assert.Equal(afterReopen, live.Authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.NotNull(live.Book.FindOpen(proposal.ProposalId));
        Assert.True(live.Authority.IsIdentityOccupiedForHost(identity));
    }

}

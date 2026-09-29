using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialExcavationTests
{
    [Fact]
    public async Task A_saved_offer_survives_real_profile30_excavation_at_the_ordinary_cutoff()
    {
        using var live = new MaterialProposalTestSession(new OrdinaryCareWorldFixture().Genesis);
        var actions = live.Actions;
        var current = live.State;
        var site = current.Excavation!.Sites.Single(value => value.OwnerActorId == actions.Player);
        var start = current.Space.GetActor(actions.Player).Feet;
        int movementSteps = 0;
        foreach (var waypoint in new[] { new SubunitPosition(start.X, site.Stance.Y, start.Z), site.Stance })
        {
            while (current.Space.GetActor(actions.Player).Feet != waypoint)
            {
                Assert.True(movementSteps++ < 512, "The worksite must be reached by a finite actual ground route.");
                var feet = current.Space.GetActor(actions.Player).Feet;
                long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
                var destination = feet.Offset(dx, dy, 0);
                Assert.Equal(GroundRouteStatus.Reachable, current.Space.QueryGroundRoute(actions.Player, destination).Status);
                var selected = actions.Batch(current, WorldBatchKind.AdvancingStep);
                current = live.Authority.Commit(new(selected.CauseId, current.WorldId, current.BranchId,
                    current.Revision, current.AdvancingSteps, WorldBatchKind.AdvancingStep,
                    [new(live.Delivery(actions.Player), actions.Player, dx, dy)], [], current.ProfileVersion)).Candidate;
                Assert.Equal(destination, current.Space.GetActor(actions.Player).Feet);
            }
        }
        for (int step = 0; current.Gathering!.GetActorWork(actions.Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 128, "Excavation effort must be earned through real exploration boundaries.");
            live.Step();
            current = live.State;
        }
        var saved = live.Save(live.Terms(actions.Helper, actions.Player));
        var before = live.State;
        Assert.Null(before.ConsequencePolicy);
        Assert.Null(before.PendingExcavation);
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, actions.Player, live.Delivery(actions.Player), clock);
        var work = session.ReadView().Worksite!;
        var target = work.ExcavationTargets.Single(value => value.SiteId == site.SiteId);
        var tool = work.Tools.Single(value => value.DefinitionId == site.RequiredTool.DefinitionId);
        var command = actions.Command(before, actions.Player, new ExcavateIntent(target.SiteId,
            target.SiteRevision, target.TerrainRevision, tool.LotId, tool.Revision, target.DestinationRevision));
        session.SetReady(true);
        var receipt = await ScheduledMaterialProposalSessionTests.Submit(session, clock, command);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        var after = live.State;
        Assert.Equal((ushort)30, after.ProfileVersion);
        Assert.Equal(site.CompletedWork + 1, after.Excavation!.Sites.Single(value => value.SiteId == site.SiteId).CompletedWork);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Care!.ContentHash, after.Care!.ContentHash);
        Assert.Equal(saved.ContentHash, live.Book.FindOpen(saved.ProposalId)!.ContentHash);
        Assert.Null(after.PendingExcavation);
        session.SetReady(false);
        await session.StopAsync();
        live.Reopen();
        Assert.Equal(after.ContentHash, live.State.ContentHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
            live.Authority.FindReceipt(command, live.Delivery(actions.Player))!));
    }

    [Fact]
    public async Task Actual_deferred_excavation_excludes_proposals_without_fabricating_a_combined_profile()
    {
        var fixture = new StructuralSettlementWorldFixture(safeActorDrop: 256, consequenceWorkBudget: 1);
        using var files = new SessionFiles();
        var before = fixture.PrepareBeforeExcavation(files.Path);
        var excavation = fixture.NextExcavation(before).Actions.Single();
        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        var delivery = ConstructionUpgradeFixture.Delivery(before, fixture.Player);
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, delivery, clock);
        session.SetReady(true);
        var work = session.SubmitAction(excavation);
        await Tick(clock);
        var pending = authority.ReadStateForHost();
        Assert.Equal((ushort)16, pending.ProfileVersion);
        Assert.NotNull(pending.PendingExcavation);
        Assert.Null(session.ReadView().MaterialProposals);
        var rejected = new WorldActionCommand(fixture.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            fixture.Player, pending.WorldId, pending.BranchId, pending.Space.GetActor(fixture.Player).Revision,
            new DeclineMaterialProposalIntent(fixture.Player));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(rejected));
        Assert.Equal(pending.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.False(work.IsCompleted);
        await session.StopAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(pending.ContentHash, reopened.ReadStateForHost().ContentHash);
    }
}

using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class EventSettlementSessionTests
{
    [Fact]
    public async Task A_restarted_funded_event_job_advances_before_readiness_and_authenticated_exact_retries_share_its_result()
    {
        var fixture = new CoupledSettlementWorldFixture(consequenceWorkBudget: 1, eventOrdered: true);
        using var files = new SessionFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldActionCommand action;
        WorldSnapshot pending;
        using (SqliteWorldAuthority writer = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepBatch trigger = fixture.NextExcavation(before);
            action = Assert.Single(trigger.Actions);
            WorldStepDecision decision = writer.Commit(trigger);
            Assert.Empty(decision.Receipts);
            pending = decision.Candidate;
            Assert.NotNull(pending.PendingExcavation);
            Assert.Equal(0UL, pending.PendingExcavation.Resolution.CompletedWorkUnits);
        }

        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(before, fixture.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, delivery, time);
        Assert.Equal(pending.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(LocalWorldRunState.Resolving, session.ReadRunState());

        // No readiness or actor request has been supplied. Restart alone resumes retained work.
        await Tick(time);
        WorldSnapshot resumed = authority.ReadStateForHost();
        Assert.Equal(pending.Revision + 1, resumed.Revision);
        Assert.NotNull(resumed.PendingExcavation);
        Assert.Equal(1UL, resumed.PendingExcavation.Resolution.CompletedWorkUnits);
        Assert.Equal(before.Calendar, resumed.Calendar);
        Assert.Equal(before.AdvancingSteps, resumed.AdvancingSteps);
        Assert.Null(authority.FindReceipt(action, delivery));

        var wrongSession = new TrustedCommandDelivery(delivery.PrincipalId,
            EntityId.DeriveSpawned(delivery.SessionId, 91234));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(action, wrongSession));
        var forgedEnvelope = new WorldActionCommand(action.CommandId, action.PrincipalId, wrongSession.SessionId,
            action.ActorId, action.WorldId, action.BranchId, action.ExpectedActorRevision, action.Intent);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(forgedEnvelope));
        var intent = Assert.IsType<ExcavateIntent>(action.Intent);
        var changedBytes = new WorldActionCommand(action.CommandId, action.PrincipalId, action.OriginSessionId,
            action.ActorId, action.WorldId, action.BranchId, action.ExpectedActorRevision,
            new ExcavateIntent(intent.SiteId, intent.ExpectedSiteRevision + 1, intent.ExpectedTerrainRevision,
                intent.ToolLotId, intent.ExpectedToolLotRevision, intent.ExpectedDestinationRevision));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(changedBytes));
        Assert.Equal(resumed.ContentHash, authority.ReadStateForHost().ContentHash);

        Task<WorldActionReceipt> retry = session.SubmitAction(action);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(action);
        Assert.False(retry.IsCompleted);
        Assert.False(duplicate.IsCompleted);
        session.SetMovement(32, 0);
        ulong continuations = 1;
        ulong maximum = pending.PendingExcavation!.MaximumContinuationCount;
        while (session.ReadView().IsResolvingConsequences)
        {
            Assert.True(continuations < maximum, "The saved event job must terminate inside its actual scene work bound.");
            await Tick(time);
            continuations++;
            WorldSnapshot current = authority.ReadStateForHost();
            if (current.PendingExcavation is not null)
            {
                Assert.Equal(before.Calendar, current.Calendar);
                Assert.Equal(before.AdvancingSteps, current.AdvancingSteps);
                Assert.Equal(before.Inventory.ContentHash, current.Inventory.ContentHash);
                Assert.False(retry.IsCompleted);
                Assert.False(duplicate.IsCompleted);
            }
        }

        WorldActionReceipt receipt = await retry.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        WorldActionReceipt duplicateReceipt = await duplicate.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(duplicateReceipt));
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        WorldSnapshot after = authority.ReadStateForHost();
        Assert.Equal((ushort)18, after.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.EventOrdered, after.DescentMode);
        Assert.Equal(after.Revision, receipt.CommitSequence);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.Space.GetActor(fixture.Player).Feet, after.Space.GetActor(fixture.Player).Feet);
        Assert.Equal(before.Space.GetActor(fixture.Builder).Feet.Offset(0, 0, -256), after.Space.GetActor(fixture.Builder).Feet);
        Assert.Single(after.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        Assert.Null(after.PendingExcavation);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(action)));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
        await session.StopAsync();

        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(after.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
            Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(action, delivery))));
    }
}

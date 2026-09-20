using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ConsequenceSessionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_selected_excavation_is_isolated_and_its_waiters_complete_only_after_paused_resolution(bool harmless)
    {
        var fixture = new StructuralSettlementWorldFixture(safeActorDrop: harmless ? 256 : 128, consequenceWorkBudget: 1);
        using var files = new SessionFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldActionCommand action = fixture.NextExcavation(before).Actions.Single();
        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(before, fixture.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, delivery, time);
        session.SetReady(true);
        session.SetMovement(32, 0);
        Task<WorldActionReceipt> first = session.SubmitAction(action);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(action);
        await Tick(time);

        WorldSnapshot pending = authority.ReadStateForHost();
        Assert.Equal(before.Revision + 1, pending.Revision);
        Assert.NotNull(pending.PendingExcavation);
        Assert.Equal(WorldActionCodec.Encode(action), WorldActionCodec.Encode(pending.PendingExcavation.Command));
        AssertStableBasis(before, pending);
        Assert.False(first.IsCompleted);
        Assert.False(duplicate.IsCompleted);
        Assert.Null(authority.FindReceipt(action, delivery));
        Assert.True(session.ReadView().IsResolvingConsequences);
        Assert.False(session.ReadView().HasReadingHold);

        session.SetReady(false);
        session.SetMovement(32, 0);
        Assert.Equal(LocalWorldRunState.Resolving, session.ReadRunState());
        int continuations = await Finish(time, session, authority, before);
        Assert.True(continuations > 0);
        WorldActionReceipt receipt = await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(receipt.ResultId, (await duplicate).ResultId);
        WorldSnapshot after = authority.ReadStateForHost();
        Assert.Equal(after.Revision, receipt.CommitSequence);
        Assert.Equal(harmless ? CommandReceiptStatus.Applied : CommandReceiptStatus.Rejected, receipt.Status);
        Assert.Equal(harmless ? WorldActionRejection.None : WorldActionRejection.Unavailable, receipt.Rejection);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        uint debit = harmless ? 400U : 0U;
        Assert.Equal(Math.Min(1000U, before.Gathering!.GetActorWork(fixture.Player).Effort - debit + 10),
            after.Gathering!.GetActorWork(fixture.Player).Effort);
        Assert.Equal(before.Space.GetActor(fixture.Player).Feet, after.Space.GetActor(fixture.Player).Feet);
        Assert.Equal(before.Space.GetActor(fixture.Builder).Feet.Offset(0, 0, harmless ? -256 : 0),
            after.Space.GetActor(fixture.Builder).Feet);
        Assert.Equal(harmless ? 1UL : 0UL, after.Excavation!.Sites.Single().CompletedWork);
        Assert.Equal(harmless ? 1 : 0, after.Inventory.Custody.Lots.Count(lot => lot.Source.SourceId == fixture.Site.CountedSourceId));
        Assert.False(session.ReadView().IsResolvingConsequences);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        Assert.Equal(receipt.ResultId, (await session.SubmitAction(action)).ResultId);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(before.ContentHash, authority.ReadRecordedStateForHost(before.Revision).StateHash);
        Assert.Equal(pending.ContentHash, authority.ReadRecordedStateForHost(pending.Revision).StateHash);
    }

    [Fact]
    public async Task A_reopened_pending_world_resumes_without_readiness_or_an_actor_request()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new SessionFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldActionCommand action;
        WorldSnapshot pending;
        using (SqliteWorldAuthority writer = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepBatch batch = fixture.NextExcavation(before);
            action = batch.Actions.Single();
            WorldStepDecision trigger = writer.Commit(batch);
            Assert.Empty(trigger.Receipts);
            pending = trigger.Candidate;
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reader = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(pending.ContentHash, reader.ReadStateForHost().ContentHash);
            Assert.Equal(before.ContentHash, reader.ReadRecordedStateForHost(before.Revision).StateHash);
            Assert.Equal(pending.ContentHash, reader.ReadRecordedStateForHost(pending.Revision).StateHash);
            Assert.Equal(pending.ContentHash, reader.ReadStateForHost().ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));

        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(before, fixture.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, delivery, time);
        Assert.Equal(LocalWorldRunState.Resolving, session.ReadRunState());
        Assert.True(session.ReadView().IsResolvingConsequences);
        await Finish(time, session, authority, before);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        WorldActionReceipt receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(action, delivery));
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        WorldSnapshot after = authority.ReadStateForHost();
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Single(after.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Exact_saved_retry_attaches_while_paused_and_stop_cancels_only_its_waiter()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new SessionFiles();
        WorldSnapshot before = fixture.PrepareBeforeExcavation(files.Path);
        WorldActionCommand action;
        WorldSnapshot pending;
        using (SqliteWorldAuthority writer = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldStepBatch batch = fixture.NextExcavation(before);
            action = batch.Actions.Single();
            pending = writer.Commit(batch).Candidate;
        }
        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(before, fixture.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, delivery, time);
        Task<WorldActionReceipt> retry = session.SubmitAction(action);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(action);
        Assert.False(retry.IsCompleted);
        Assert.False(duplicate.IsCompleted);
        Assert.Equal(LocalWorldRunState.Resolving, session.ReadRunState());

        var original = Assert.IsType<ExcavateIntent>(action.Intent);
        var changed = new WorldActionCommand(action.CommandId, action.PrincipalId, action.OriginSessionId, action.ActorId,
            action.WorldId, action.BranchId, action.ExpectedActorRevision,
            new ExcavateIntent(original.SiteId, original.ExpectedSiteRevision + 1, original.ExpectedTerrainRevision,
                original.ToolLotId, original.ExpectedToolLotRevision, original.ExpectedDestinationRevision));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(changed));
        WorldActionCommand different = fixture.NextExcavation(before).Actions.Single();
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(different));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(action,
            new TrustedCommandDelivery(delivery.PrincipalId, EntityId.DeriveSpawned(delivery.SessionId, 91234))));
        Assert.Equal(pending.ContentHash, authority.ReadStateForHost().ContentHash);
        await session.StopAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => duplicate);

        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(pending.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.NotNull(reopened.ReadStateForHost().PendingExcavation);
        Assert.Null(reopened.FindReceipt(action, delivery));
        AssertStableBasis(before, reopened.ReadStateForHost());
    }

    private static async Task<int> Finish(ManualSessionTimeProvider time, LocalWorldSession session,
        SqliteWorldAuthority authority, WorldSnapshot basis)
    {
        int count = 0;
        while (session.ReadView().IsResolvingConsequences)
        {
            Assert.True(count < 128, "The finite settlement must complete within its retained query budget.");
            WorldSnapshot previous = authority.ReadStateForHost();
            await Tick(time);
            WorldSnapshot current = authority.ReadStateForHost();
            Assert.Equal(previous.Revision + 1, current.Revision);
            if (current.PendingExcavation is not null)
            {
                AssertStableBasis(basis, current);
                Assert.Equal(LocalWorldRunState.Resolving, session.ReadRunState());
            }
            count++;
        }
        return count;
    }

    private static void AssertStableBasis(WorldSnapshot before, WorldSnapshot pending)
    {
        Assert.Equal(before.Calendar, pending.Calendar);
        Assert.Equal(before.AdvancingSteps, pending.AdvancingSteps);
        Assert.Equal(before.Inventory.ContentHash, pending.Inventory.ContentHash);
        var stable = new WorldSnapshot(pending.Inventory, pending.Space, pending.Residents, pending.AdmissionVersion,
            pending.Calendar, before.Revision, pending.AdvancingSteps, pending.Dialogues, pending.Gathering,
            pending.Production, pending.Progression, pending.Quests, pending.Construction,
            excavation: pending.Excavation, constructionPhysical: pending.ConstructionPhysical,
            structuralPolicy: pending.StructuralPolicy, structuralSettlementPolicy: pending.StructuralSettlementPolicy,
            consequencePolicy: pending.ConsequencePolicy);
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(stable));
        Assert.Equal(before.Gathering!.ContentHash, pending.Gathering!.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, pending.Excavation!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, pending.ConstructionPhysical!.ContentHash);
        Assert.All(before.Residents, resident => Assert.Equal(resident.ExperiencedTime,
            pending.GetResident(resident.ActorId).ExperiencedTime));
    }
}

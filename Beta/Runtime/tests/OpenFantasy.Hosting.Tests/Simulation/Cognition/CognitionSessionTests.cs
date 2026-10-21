using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Cognition;

/// <summary>
/// Wren's first episode in a real local session: only the player submits requests, and the host's cognition policy
/// proposes every one of Wren's looks, decisions, approaches, swings and work steps through ordinary batches.
/// </summary>
public sealed class CognitionSessionTests
{
    private const int StepLimit = 1200;

    [Fact]
    public async Task The_host_policy_drives_wren_from_waiting_through_gathering_to_the_funded_guard_across_a_restart()
    {
        var fixture = new CognitionWorldFixture();
        using var files = new StorageFiles();
        var time = new ManualSessionTimeProvider();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery(fixture.Player), time);
        session.SetReady(true);
        WorldSnapshot State() => authority.ReadStateForHost();
        ActorCognition Wren() => State().Cognition!.For(fixture.Wren);
        async Task Until(string what, Func<bool> condition)
        {
            for (int step = 0; !condition(); step++)
            {
                Assert.True(step < StepLimit, $"Wren should {what} within {StepLimit} ordinary steps.");
                await SessionFixture.Tick(time);
            }
        }
        async Task<WorldActionReceipt> Submit(WorldActionIntent intent)
        {
            WorldSnapshot state = State();
            TrustedCommandDelivery delivery = fixture.Delivery(fixture.Player);
            var command = new WorldActionCommand(fixture.NextId(), delivery.PrincipalId, delivery.SessionId, fixture.Player,
                state.WorldId, state.BranchId, state.Space.GetActor(fixture.Player).Revision, intent);
            Task<WorldActionReceipt> receipt = session.SubmitAction(command);
            while (!receipt.IsCompleted) await SessionFixture.Tick(time);
            return await receipt;
        }

        // From home Wren sees the oak, and without a tool she honestly waits.
        await Until("look at the oak", () => Wren().Beliefs.Sources.Any(source => source.SourceId == fixture.Oak));
        await Until("decide to wait", () => Wren().Memory.Count != 0);
        Assert.Equal(CognitionDeferral.MissingTool, Wren().Memory.Single().Reason);
        Assert.Equal(0UL, State().Construction!.Projects.Single().Progress.CompletedUnits);

        // The player gives the axe outright; Wren chooses, approaches and gathers a small personal stock.
        CountedSourceLot axe = State().Inventory.Custody.Lots.Single(lot => lot.CustodianId == State().GetResident(fixture.Player).InventoryCustodianId
            && lot.Definition == fixture.Axe);
        EntityId bag = State().GetResident(fixture.Player).InventoryCustodianId;
        Assert.Equal(CommandReceiptStatus.Applied, (await Submit(new GiveItemIntent(fixture.Wren, State().Space.GetActor(fixture.Wren).Revision,
            axe.LotId, axe.Revision, bag, State().Inventory.Custody.GetStorage(bag).Revision, 1))).Status);
        await Until("adopt gathering", () => Wren().Intention?.Kind == IntentionKind.GatherPersonalStock);
        await Until("gather once", () => Wren().Beliefs.Sources.Any(source => source.LatestYield is > 0));

        // Restart with the gathering wake pending: the same save continues under a new session.
        CognitionWake pending = Wren().Wake!;
        await session.DisposeAsync();
        authority.Dispose();
        authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(pending, authority.ReadStateForHost().Cognition!.For(fixture.Wren).Wake);
        session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery(fixture.Player), time);
        session.SetReady(true);
        await Until("meet the stock target", () => Wren().Memory.Any(entry => entry.Reason == CognitionDeferral.StockTargetMet));
        Assert.Equal(CognitionWorldFixture.PersonalStockTarget, Holding(State(), fixture.Wren, fixture.Quest.Item));
        // Each newer gathering outcome replaced the uncited earlier one; the journal keeps every yield.
        Assert.Single(Wren().Evidence, entry => entry.Kind == EvidenceKind.GatherOutcome);

        // Without the delivery Wren never starts the funded guard: world truth is not her knowledge.
        Assert.Equal(0UL, State().Construction!.Projects.Single().Progress.CompletedUnits);

        // The real delivery reaches Wren in person; afterwards she works the original funded project.
        DeliveryQuestDefinition quest = fixture.Quest;
        Assert.Equal(CommandReceiptStatus.Applied, (await Submit(new SpeakToActorIntent(fixture.Wren, State().Space.GetActor(fixture.Wren).Revision))).Status);
        Assert.Equal(CommandReceiptStatus.Applied, (await Submit(new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, fixture.Wren,
            State().Space.GetActor(fixture.Wren).Revision, 0, State().Progression!.GetActor(fixture.Player).Revision))).Status);
        CountedSourceLot wood = State().Inventory.Custody.Lots.Single(lot => lot.CustodianId == bag && lot.Definition == quest.Item);
        Assert.Equal(CommandReceiptStatus.Applied, (await Submit(new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, fixture.Wren,
            State().Space.GetActor(fixture.Wren).Revision, 1, State().Progression!.GetActor(fixture.Player).Revision,
            State().Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)]))).Status);
        Assert.Equal(WakeSuppression.ReadingHold, Wren().Wake!.Suppression);
        Assert.Equal(CommandReceiptStatus.Applied, (await Submit(new CloseDialogueIntent(
            State().Dialogues.Single(dialogue => dialogue.ListenerActorId == fixture.Player).DialogueId))).Status);
        await Until("adopt the funded guard", () => Wren().Intention?.Kind == IntentionKind.WorkFundedProject);
        await Until("start the guard", () => State().Construction!.Projects.Single().Progress.CompletedUnits != 0);
        Assert.Equal(CognitionWorldFixture.PersonalStockTarget, Holding(State(), fixture.Wren, quest.Item));
        await session.DisposeAsync();
        authority.Dispose();
    }

    [Fact]
    public async Task A_host_decision_past_the_memory_bound_stops_the_authority_through_its_overflow_route()
    {
        var fixture = new CognitionWorldFixture();
        fixture.FillWrenMemoryWithAlternatingReasons();
        WorldSnapshot head = fixture.State;
        using var files = new StorageFiles();
        var time = new ManualSessionTimeProvider();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        foreach (byte[] batch in fixture.Batches) authority.Commit(WorldStepCodec.Decode(batch));
        Assert.Equal(head.ContentHash, authority.ReadStateForHost().ContentHash);

        // The host policy's honest "missing tool" would be the 65th memory. It is proposed, not swallowed, so the
        // authority's preview stops it and the session ends with the overflow.
        var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery(fixture.Player), time);
        session.SetReady(true);
        for (int step = 0; !session.Completion.IsCompleted; step++)
        {
            Assert.True(step < 8, "The overflowing decision is due at the first boundary.");
            await SessionFixture.Until(() => time.HasPendingTimer || session.Completion.IsCompleted);
            if (!session.Completion.IsCompleted) time.Advance(LocalWorldSession.StepInterval);
        }
        await Assert.ThrowsAsync<CognitionCapacityException>(() => session.Completion);
        Assert.Throws<InvalidOperationException>(() => authority.Commit(WorldStepCodec.Decode(fixture.Batches[^1])));
        await Assert.ThrowsAsync<CognitionCapacityException>(async () => await session.DisposeAsync());
        authority.Dispose();

        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(head.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    private static ulong Holding(WorldSnapshot state, EntityId actor, CountedItemDefinition definition) =>
        state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == state.GetResident(actor).InventoryCustodianId
            && lot.Definition == definition).Aggregate(0UL, (total, lot) => total + lot.Quantity);
}

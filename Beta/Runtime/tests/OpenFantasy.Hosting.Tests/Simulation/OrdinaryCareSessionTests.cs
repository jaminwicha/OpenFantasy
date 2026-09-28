using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class OrdinaryCareSessionTests
{
    [Fact]
    public async Task Real_self_use_requires_readiness_and_one_tick_then_reuses_its_receipt_while_paused()
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var before = fixture.ReachInjuredExploration(new(authority));
        var command = fixture.Journey.Command(before, fixture.Player, fixture.Intent(before, self: true));
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.Journey.Delivery(command), time);

        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(command));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
        session.SetReady(true);
        session.SetMovement(32, 0);
        var submitted = session.SubmitAction(command);
        var duplicate = session.SubmitAction(command);
        await Tick(time);
        await Until(() => submitted.IsCompleted && duplicate.IsCompleted);
        var receipt = await submitted;
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await duplicate));
        var after = authority.ReadStateForHost();
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Space.GetActor(fixture.Player).Feet, after.Space.GetActor(fixture.Player).Feet);
        Assert.Equal(command.CommandId, Assert.Single(after.Care!.Ordinary!.Uses).CommandId);
        Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
        session.SetReady(false);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(command)));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.False(session.Completion.IsCompleted);
    }

    [Fact]
    public async Task Reading_hold_refuses_ordinary_selection_without_faulting_then_allows_explicit_patient_revocation()
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var journey = new CareWorldSession(authority);
        var before = fixture.ReachInjuredExploration(journey);
        var dose = fixture.Intent(before, self: true);
        var speak = fixture.Journey.Command(before, fixture.Player,
            new SpeakToActorIntent(fixture.Helper, before.Space.GetActor(fixture.Helper).Revision));
        var held = CareRewardJourney.Commit(journey, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [speak])).Candidate;
        Assert.True(held.HasReadingHold);
        var use = fixture.Journey.Command(held, fixture.Player, dose);
        var permission = held.Care!.Ordinary!.Permissions.Single();
        var revoke = fixture.Journey.Command(held, fixture.Player,
            new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.Journey.Delivery(use), time);
        session.SetReady(true);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(use));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(revoke));
        await Tick(time);
        Assert.Equal(held.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.False(session.Completion.IsCompleted);
        var close = fixture.Journey.Command(held, fixture.Player, new CloseDialogueIntent(held.Dialogues.Single().DialogueId));
        var closing = session.SubmitAction(close);
        await Tick(time);
        await Until(() => closing.IsCompleted);
        Assert.Equal(CommandReceiptStatus.Applied, (await closing).Status);
        var unheld = authority.ReadStateForHost();
        Assert.False(unheld.HasReadingHold);
        Assert.Equal(held.Calendar, unheld.Calendar);
        var selected = fixture.Journey.Command(unheld, fixture.Player, revoke.Intent);
        var revoking = session.SubmitAction(selected);
        await Tick(time);
        await Until(() => revoking.IsCompleted);
        Assert.Equal(CommandReceiptStatus.Applied, (await revoking).Status);
        var revoked = authority.ReadStateForHost();
        Assert.False(revoked.Care!.Ordinary!.Permissions.Single().IsActive);
        Assert.Equal(unheld.Progression!.ContentHash, revoked.Progression!.ContentHash);
        Assert.Equal(unheld.Production!.ContentHash, revoked.Production!.ContentHash);
        Assert.Equal(unheld.Calendar.AddMilliseconds(1800), revoked.Calendar);
        Assert.Empty(revoked.Care.Ordinary.Uses);
        Assert.False(session.Completion.IsCompleted);
        session.SetReady(false);
    }
}

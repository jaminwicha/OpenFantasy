using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalSessionTests
{
    [Fact]
    public async Task Two_ports_choose_independently_and_the_first_choice_does_not_bypass_full_wait()
    {
        var run = WithdrawalWorldFixture.Active();
        using var files = new StorageFiles();
        var authority = Publish(files.Path, run);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, run.Humans[0], run.Delivery(run.Humans[0]), time);
        var second = await host.ConnectPlayerAsync(run.Humans[1], run.Delivery(run.Humans[1]));
        host.SetReady(true);
        second.SetReady(true);
        await Until(() => host.ReadView().Retreat!.Offer!.CanQueue && second.ReadView().Retreat!.Offer!.CanQueue, time);
        var first = Route(host.Player, run);
        var accepted = host.SubmitAction(first);
        await Until(() => accepted.IsCompleted, time);
        Assert.Equal(WorldActionRejection.None, (await accepted).Rejection);
        var held = authority.ReadStateForHost();
        Assert.Single(held.Retreat!.Withdrawal!.Parties.Single().Members);
        for (int tick = 0; tick < 6; tick++) await SessionFixture.Tick(time);
        Assert.Equal(held.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Null(second.ReadView().Retreat!.Withdrawal!.Choice);
        Assert.True(second.ReadView().Retreat!.Offer!.CanQueue);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => second.SubmitAction(first));
        var another = Route(second, run);
        var acceptedSecond = second.SubmitAction(another);
        await Until(() => acceptedSecond.IsCompleted, time);
        Assert.Equal(WorldActionRejection.None, (await acceptedSecond).Rejection);
        var choices = authority.ReadStateForHost().Retreat!.Withdrawal!.Parties.Single().Members;
        Assert.Equal(new[] { first.CommandId, another.CommandId }.Order(), choices.Select(member => member.Command.CommandId).Order());
        foreach (var player in new[] { host.Player, second })
        {
            var view = player.ReadView();
            Assert.Equal(view.PlayerActorId, PlayerViewCodec.Decode(PlayerViewCodec.EncodeWithCollectiveWithdrawal(view)).PlayerActorId);
        }
        var pending = new Dictionary<EntityId, Task<WorldActionReceipt>>();
        for (int cutoff = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration; cutoff++)
        {
            Assert.True(cutoff < 1200, "The shared session must reach actual collective departure.");
            Assert.Equal(EncounterLifecyclePhase.Active, authority.ReadStateForHost().EncounterLifecycle!.Phase);
            foreach (var player in new[] { host.Player, second })
            {
                if (pending.TryGetValue(player.ActorId, out var previous))
                {
                    if (!previous.IsCompleted) continue;
                    Assert.Equal(WorldActionRejection.None, (await previous).Rejection);
                    pending.Remove(player.ActorId);
                }
                var view = player.ReadView();
                if (view.Retreat!.IsRetreating) continue;
                WorldActionCommand? next = view.Retreat.Offer?.CanQueue == true ? Route(player, run)
                    : view.EncounterActions is { CanWait: true } actions ? CurrentCommand(player, run,
                        new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision)) : null;
                if (next is not null) pending.Add(player.ActorId, player.SubmitAction(next));
            }
            await SessionFixture.Tick(time);
        }
        foreach (var receipt in pending.Values) Assert.Equal(WorldActionRejection.None, (await receipt).Rejection);
        var departed = authority.ReadStateForHost();
        Assert.Equal(run.Humans.Order(), departed.Retreat!.Withdrawal!.Last!.Ended.Select(end => end.Member.ActorId).Order());
        Assert.All(departed.Retreat.Withdrawal.Last.Ended, end => Assert.Equal(EncounterWithdrawalEndReason.Disengaged, end.Reason));
        Assert.Null(departed.EncounterOutcome!.Last);
        Assert.True(departed.EncounterLifecycle!.TacticalTicks > held.EncounterLifecycle!.TacticalTicks);
        Assert.Equal(held.Inventory.ContentHash, departed.Inventory.ContentHash);
        Assert.All(run.Humans, actor => Assert.True(departed.Progression!.GetActor(actor).CurrentHp <= held.Progression!.GetActor(actor).CurrentHp));
        var pins = new PlayerWorldCompatibility(held.WorldId, held.BranchId, 40, held.AdmissionVersion,
            PlayerWorldCompatibility.ResolvePackages([]));
        Assert.False(pins.Matches(held));
    }

    [Fact]
    public async Task Cancellation_between_paid_samples_preserves_debt_and_position_and_retries_the_original_receipt()
    {
        var run = WithdrawalWorldFixture.Active();
        var actor = run.Humans[0];
        var route = run.Command(actor, PartyWithdrawalCapacityTests.LongestRoute(run, actor));
        run.Choose(route);
        for (int step = 0; run.State.Retreat!.Withdrawal!.FindMember(actor)!.SamplesWalked == 0; step++)
        {
            Assert.True(step < 60);
            run.ChooseDue(attacks: false);
            run.ResolveOrTick();
        }
        Assert.False(run.State.Pacing!.Encounter!.GetActor(actor).IsDue);
        using var files = new StorageFiles();
        var authority = Publish(files.Path, run);
        var time = new ManualSessionTimeProvider();
        var host = new LocalWorldSession(authority, actor, run.Delivery(actor), time);
        WorldActionCommand cancellation;
        byte[] receiptBytes;
        WorldSnapshot after;
        try
        {
            host.SetReady(true);
            await Until(() => host.ReadView().Retreat!.Withdrawal!.Cancellation is not null, time);
            var view = host.ReadView();
            var before = authority.ReadStateForHost();
            var offer = view.Retreat!.Withdrawal!.Cancellation!;
            cancellation = CurrentCommand(host.Player, run, offer);
            var result = host.SubmitAction(cancellation);
            await Until(() => result.IsCompleted, time);
            Assert.Equal(WorldActionRejection.None, (await result).Rejection);
            receiptBytes = WorldReceiptCodecV1.Encode(await result);
            after = authority.ReadStateForHost();
            Assert.Equal(before.Space.GetActor(actor).Feet, after.Space.GetActor(actor).Feet);
            Assert.Equal(before.Calendar, after.Calendar);
            Assert.Equal(before.Pacing!.Encounter!.GetActor(actor).RemainingDelay, after.Pacing!.Encounter!.GetActor(actor).RemainingDelay);
            Assert.Null(after.Retreat!.Withdrawal!.FindMember(actor));
            Assert.Equal(EncounterWithdrawalEndReason.ChoiceChanged, Assert.Single(after.Retreat.Withdrawal.Last!.Ended).Reason);
            Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(await host.SubmitAction(cancellation)));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            var stale = host.SubmitAction(CurrentCommand(host.Player, run, offer));
            await Until(() => stale.IsCompleted, time);
            Assert.NotEqual(WorldActionRejection.None, (await stale).Rejection);
            after = authority.ReadStateForHost();
        }
        finally { await host.DisposeAsync(); }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(run.Genesis));
        Assert.Equal(after.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(reopened.FindReceipt(cancellation, run.Delivery(actor))!));
    }

    [Fact]
    public void Autonomous_people_choose_independent_routes_without_taking_a_connected_humans_choice()
    {
        var run = WithdrawalWorldFixture.Active(3);
        foreach (var actor in run.Humans.Take(2))
        {
            run.ControllerEvent(actor, WorldControllerEventKind.Disconnected);
            // Core expiry inputs only; elapsed Host allowance is covered by its maintained suite.
            run.ControllerEvent(actor, WorldControllerEventKind.AllowanceExpired);
        }
        var commands = new LocalAutonomousEncounterController().SelectChoices(run.State, [], run.NextId);
        Assert.Equal(run.Humans.Take(2).Order(), commands.Select(command => command.ActorId).Order());
        Assert.All(commands, command => Assert.IsType<QueueEncounterRetreatIntent>(command.Intent));
        run.Choose(commands.ToArray());
        Assert.Null(run.State.Retreat!.Withdrawal!.FindMember(run.Humans[2]));
        Assert.True(WorldEncounterActions.HasRequiredDecisions(run.State));
    }

    private static SqliteWorldAuthority Publish(string path, WithdrawalWorldFixture run)
    {
        var authority = SqliteWorldAuthority.CreateNew(path, run.Genesis);
        foreach (var batch in run.Batches) authority.Commit(batch);
        Assert.Equal(run.State.ContentHash, authority.ReadStateForHost().ContentHash);
        return authority;
    }

    private static WorldActionCommand Route(LocalPlayerConnection player, WithdrawalWorldFixture run)
    {
        var offer = player.ReadView().Retreat!.Offer!;
        Assert.True(offer.CanQueue);
        return CurrentCommand(player, run, new QueueEncounterRetreatIntent(offer.EncounterId, offer.TimingRevision, offer.Destination, offer.Route));
    }

    private static WorldActionCommand CurrentCommand(LocalPlayerConnection player, WithdrawalWorldFixture run, WorldActionIntent intent)
    {
        var view = player.ReadView();
        return new(run.NextId(), player.Delivery.PrincipalId, player.Delivery.SessionId, player.ActorId,
            view.WorldId, view.BranchId, view.Actors.Single(actor => actor.ActorId == player.ActorId).PresenceRevision, intent);
    }

    private static async Task Until(Func<bool> ready, ManualSessionTimeProvider time)
    {
        for (int cutoff = 0; !ready(); cutoff++)
        {
            Assert.True(cutoff < 80, "The session did not reach the required withdrawal boundary.");
            await SessionFixture.Tick(time);
        }
    }
}

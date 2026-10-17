using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Server.Tests;

/// <summary>Ordinary native decisions through enemy damage, individual withdrawal and owned medicine.</summary>
internal sealed class NativeExpeditionEncounter(
    IReadOnlyDictionary<EntityId, NativeRosterProcess> peers, EntityId lead,
    Action<string> mark, Action<string> setStage, CancellationToken token)
{
    private readonly CancellationToken _token = token;

    internal async Task Complete(IReadOnlyDictionary<EntityId, int> healthy)
    {
        EntityId[] humans = peers.Keys.ToArray();
        var medicine = humans.ToDictionary(actor => actor,
            actor => peers[actor].View.Restorative!.SelfUseOffers.Single().ItemDefinitionId);
        Assert.Equal(1UL, Units(lead, medicine[lead]));
        Assert.All(humans.Where(actor => actor != lead), actor => Assert.Equal(0UL, Units(actor, medicine[actor])));
        foreach (EntityId actor in humans) await Send(actor, "resume");
        await NativeEncounterObservationBarrier.Active(peers.Values.ToArray(), peers[lead].View.Encounter!.EncounterId, _token);
        Assert.All(peers.Values, peer => Assert.Equal(humans.Order(),
            peer.View.Retreat!.Withdrawal!.Members.Select(row => row.ActorId).Order()));
        for (int decisions = 0; !humans.Any(actor => peers[actor].View.Progression!.Hp < healthy[actor]); decisions++)
        {
            Assert.True(decisions < 20, "Ordinary choices did not expose a real enemy turn.");
            await ChooseDueWaits();
        }
        Assert.All(peers.Values, peer => Assert.True(peer.View.Progression!.Hp > 0,
            "A companion was incapacitated; collective withdrawal must not bypass that real outcome."));
        Mark("ordinary-enemy-turn-inflicted-real-injury");

        SetStage("collective-withdrawal");
        var requested = new HashSet<EntityId>();
        for (int decisions = 0; peers[lead].View.IsInEncounter; decisions++)
        {
            Assert.True(decisions < 100, "Collective withdrawal did not resolve within its bounded decision count.");
            foreach (EntityId actor in humans)
            {
                if (!peers[lead].View.IsInEncounter) break;
                var view = peers[actor].View;
                Assert.True(view.Progression!.Hp > 0, "An incapacitated participant requires actual recovery, not a waived escape guard.");
                if (view.Retreat?.Withdrawal?.Choice is null && view.Retreat?.Offer?.CanQueue == true)
                {
                    Assert.True(requested.Add(actor), "A route ended unexpectedly; do not silently retry it.");
                    await Send(actor, "retreat");
                }
                else if (view.EncounterActions?.CanWait == true) await Send(actor, "wait");
            }
            await Observe();
            await Task.Delay(50, _token);
        }
        await NativeEncounterObservationBarrier.Reach(peers.Values.ToArray(), peers[lead].View, _token);
        Assert.Equal(2, requested.Count);
        Assert.All(peers.Values, peer =>
        {
            var ended = peer.View.Retreat!.Withdrawal!.Last;
            Assert.NotNull(ended);
            Assert.Equal(EncounterWithdrawalEndReason.Disengaged, ended.Reason);
            Assert.Equal(ended.RouteSamples, ended.SamplesTaken);
            Assert.True(ended.ReadinessCharged > 0);
        });
        await Send(lead, "capture");
        Mark("both-withdrew-with-independent-paid-routes");

        SetStage("real-care");
        int treated = 0;
        foreach (EntityId actor in humans.Where(actor => peers[actor].View.Progression!.Hp < healthy[actor]))
        {
            var ready = Stopwatch.StartNew();
            while (peers[actor].View.EncounterLifecycle?.PlayerCanAct != true)
            {
                Assert.True(ready.Elapsed.TotalSeconds < 10, "Ordinary exit recovery did not restore the actor's ability to act.");
                await Observe();
                await Task.Delay(50, _token);
            }
            _token.ThrowIfCancellationRequested();
            Assert.True(ready.Elapsed.TotalSeconds < 10, "Medicine readiness completed beyond its absolute bound.");
            var remedy = peers[actor].View.Restorative!.SelfUseOffers.Single();
            if (Units(actor, medicine[actor]) == 0)
            {
                Assert.False(remedy.CanUse);
                Assert.Equal(RestorativeAvailabilityReason.NoOwnedDose, remedy.Reason);
                Assert.Null(remedy.Intent);
                continue;
            }
            Assert.True(remedy.CanUse);
            int hp = peers[actor].View.Progression!.Hp;
            ulong units = Units(actor, remedy.ItemDefinitionId);
            await Send(actor, "care-library-open");
            await Send(actor, "care-library-select", "UseRemedy", remedy.DefinitionId);
            await Send(actor, "care-library-prepare");
            await Send(actor, "care-library-confirm", "Applied");
            Assert.Equal(hp + remedy.ExpectedHealingHp, peers[actor].View.Progression!.Hp);
            Assert.Equal(units - 1, Units(actor, remedy.ItemDefinitionId));
            await Send(actor, "care-library-close");
            treated++;
        }
        Assert.Equal(1, treated);
        Mark("one-real-dose-treated-injury-with-no-invented-companion-supply");
    }

    private Task Observe() => Task.WhenAll(peers.Values.Select(peer => peer.Send("observe")));
    private async Task Send(EntityId actor, string verb, string? direction = null, EntityId? target = null)
    {
        await peers[actor].Send(verb, direction, target);
        if (verb is "wait" or "strike" or "retreat" or "cancel-withdrawal")
            await NativeEncounterObservationBarrier.Reach(peers.Values.ToArray(), peers[actor].View, _token);
        else await Observe();
    }
    private async Task ChooseDueWaits()
    {
        foreach (EntityId actor in peers.Keys)
            if (peers[actor].View.EncounterActions?.CanWait == true) await Send(actor, "wait");
        await Observe();
        await Task.Delay(50, _token);
    }
    private ulong Units(EntityId actor, EntityId item) => peers[actor].View.PlayerInventory.Lots
        .Where(row => row.DefinitionId == item).Aggregate(0UL, (sum, row) => sum + row.Quantity);
    private void Mark(string name) => mark(name);
    private void SetStage(string stage) => setStage(stage);
}

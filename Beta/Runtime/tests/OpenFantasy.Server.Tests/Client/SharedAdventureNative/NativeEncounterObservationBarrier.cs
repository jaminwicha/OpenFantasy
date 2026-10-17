using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

/// <summary>Receipt delivery is not a cross-client observation barrier.</summary>
internal static class NativeEncounterObservationBarrier
{
    internal static Task Active(IReadOnlyCollection<NativeRosterProcess> peers, EntityId encounter,
        CancellationToken token) => ObserveUntil(peers, view =>
            view.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Active
            && view.Encounter?.EncounterId == encounter, token);

    internal static Task Reach(IReadOnlyCollection<NativeRosterProcess> peers, LocalWorldView accepted,
        CancellationToken token)
    {
        bool Reached(LocalWorldView view)
        {
            if (accepted.EncounterLifecycle is not { } lifecycle || view.EncounterLifecycle is not { } current
                || current.EncounterCount < lifecycle.EncounterCount) return false;
            if (accepted.Encounter is not { } encounter)
                return current.Phase is EncounterLifecyclePhase.Outcome or EncounterLifecyclePhase.Exploration;
            return view.Encounter is { } observed && observed.EncounterId == encounter.EncounterId
                && observed.TimingRevision >= encounter.TimingRevision
                || current.Phase is EncounterLifecyclePhase.Outcome or EncounterLifecyclePhase.Exploration;
        }
        return ObserveUntil(peers, Reached, token);
    }

    private static async Task ObserveUntil(IReadOnlyCollection<NativeRosterProcess> peers,
        Func<LocalWorldView, bool> reached, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        do
        {
            Assert.True(watch.Elapsed.TotalSeconds < 5, "A peer did not receive the accepted public encounter boundary.");
            await Task.WhenAll(peers.Select(peer => peer.Send("observe")));
            token.ThrowIfCancellationRequested();
            Assert.True(watch.Elapsed.TotalSeconds < 5, "Peer observations completed beyond their absolute bound.");
            if (peers.All(peer => reached(peer.View))) return;
            await Task.Delay(20, token);
        } while (true);
    }
}

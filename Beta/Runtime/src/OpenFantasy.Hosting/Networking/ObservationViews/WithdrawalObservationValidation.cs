using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Checks presentation scope and consistency, never command authorization or world admission.</summary>
internal static class WithdrawalObservationValidation
{
    internal static void Validate(LocalWorldView view, bool included)
    {
        Require(included == (view.Retreat?.Withdrawal is not null), "Collective withdrawal requires its explicit observation version.");
        if (view.Retreat?.Withdrawal is { } withdrawal)
            Validate(withdrawal, view.PlayerActorId, view.Actors, view.EncounterActors, view.Encounter,
                view.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Active);
    }

    internal static void Validate(LocalControlledActorView view, bool included)
    {
        if (view.Retreat is not { } retreat) return;
        Require(included == (retreat.Withdrawal is not null), "Controlled withdrawal requires its explicit observation version.");
        if (retreat.Withdrawal is { } withdrawal)
        {
            Require(withdrawal.Last is null, "Delegated current choices must not disclose terminal history.");
            Validate(withdrawal, view.ActorId, view.Actors, view.Combat!.Actors, view.Combat.Encounter,
                view.Combat.Lifecycle?.Phase == EncounterLifecyclePhase.Active);
        }
    }

    private static void Validate(LocalWithdrawalView view, EntityId observer, IReadOnlyList<LocalActorView> actors,
        IReadOnlyList<LocalEncounterActorView> combatants, LocalEncounterView? encounter, bool active)
    {
        Require(view.ActorId == observer && actors.Any(actor => actor.ActorId == observer), "Foreign withdrawal observer.");
        Require(view.Members.Count <= EncounterTimingSnapshot.MaximumActors
            && view.Members.All(member => member is not null && Enum.IsDefined(member.Readiness)), "Invalid withdrawal member status.");
        var side = combatants.SingleOrDefault(actor => actor.ActorId == observer)?.Side ?? 0;
        var required = active && side > 0 ? combatants.Where(actor => actor.Side == side).Select(actor => actor.ActorId).Order().ToArray() : [];
        Require(view.Members.Select(member => member.ActorId).SequenceEqual(required), "Withdrawal readiness must describe the complete observed side.");
        foreach (var member in view.Members)
            Require((member.Readiness == LocalWithdrawalReadiness.Incapacitated)
                == combatants.Single(actor => actor.ActorId == member.ActorId).IsIncapacitated, "Withdrawal readiness disagrees with actual incapacity.");
        if (view.Choice is { } choice)
        {
            Require(encounter is not null && active && choice.CommandId != default && choice.RouteSamples is > 0 and <= 32
                && choice.SamplesTaken >= 0 && choice.SamplesTaken <= choice.RouteSamples
                && (choice.SamplesTaken == 0) == (choice.ReadinessCharged == 0), "Invalid own withdrawal progress.");
            var own = view.Members.SingleOrDefault(member => member.ActorId == observer);
            Require(own is not null && own.Readiness == (choice.HasArrived ? LocalWithdrawalReadiness.Arrived : LocalWithdrawalReadiness.Walking),
                "Own withdrawal progress disagrees with party readiness.");
            if (choice.HasArrived)
                Require(actors.Single(actor => actor.ActorId == observer).Feet == choice.Destination, "An arrived actor must occupy its destination.");
        }
        else Require(view.Cancellation is null && view.Members.All(member => member.ActorId != observer
            || member.Readiness is LocalWithdrawalReadiness.NeedsChoice or LocalWithdrawalReadiness.Incapacitated), "Missing own withdrawal choice.");
        if (view.Cancellation is { } cancellation)
            Require(view.Choice is not null && encounter is not null && cancellation.WithdrawalCommandId == view.Choice.CommandId
                && cancellation.EncounterId == encounter.EncounterId && cancellation.ExpectedTimingRevision == encounter.TimingRevision,
                "Cancellation must name the observer's exact choice and current timing boundary.");
        if (view.Last is { } last)
            Require(last.ActorId == observer && last.CommandId != default && last.WorldSequence != 0 && Enum.IsDefined(last.Reason)
                && last.RouteSamples is > 0 and <= 32 && last.SamplesTaken >= 0 && last.SamplesTaken <= last.RouteSamples,
                "Invalid own terminal withdrawal record.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new FormatException(message);
    }
}

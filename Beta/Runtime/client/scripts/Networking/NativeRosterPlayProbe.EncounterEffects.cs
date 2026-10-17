using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

internal sealed partial class NativeRosterPlayProbe
{
    private bool EncounterEffectObserved(FarmSubmission accepted) => accepted.Offer.Scope.Selection is not null
        ? SelectedEffectObserved(_game.CurrentSelection, accepted) && SelectedEffectObserved(_game.PresentedSelection, accepted)
        : HomeEffectObserved(_game.CurrentView, accepted) && HomeEffectObserved(_game.PresentedHomeView, accepted);

    private static bool SelectedEffectObserved(LocalSelectedPlayerObservation? observation, FarmSubmission accepted)
    {
        if (observation is null || observation.Selection != accepted.Offer.Scope.Selection
            || observation.Selection.ActorId != observation.Selection.HomeActorId) return false;
        return HomeEffectObserved(observation.HomeView, accepted);
    }

    private static bool HomeEffectObserved(LocalWorldView? view, FarmSubmission accepted)
    {
        if (view is null || view.PlayerActorId != accepted.Offer.Scope.ActorId
            || view.WorldId != accepted.Offer.Scope.WorldId || view.BranchId != accepted.Offer.Scope.BranchId) return false;
        bool Advanced(OpenFantasy.Core.EntityId encounter, ulong revision) =>
            view.Encounter is { } current && current.EncounterId == encounter && current.TimingRevision > revision
            || view.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Outcome or EncounterLifecyclePhase.Exploration;
        return accepted.Offer.Intent switch
        {
            QueueEncounterWaitIntent wait => Advanced(wait.EncounterId, wait.ExpectedTimingRevision),
            QueueEncounterAbilityIntent ability => Advanced(ability.EncounterId, ability.ExpectedTimingRevision),
            QueueEncounterRetreatIntent => view.Retreat?.Withdrawal is { } withdrawal
                && (withdrawal.Choice?.CommandId == accepted.CommandId || withdrawal.Last?.CommandId == accepted.CommandId),
            CancelEncounterWithdrawalIntent cancel => view.Retreat?.Withdrawal is { } withdrawal
                && withdrawal.Choice?.CommandId != cancel.WithdrawalCommandId
                && withdrawal.Last?.CommandId == cancel.WithdrawalCommandId,
            _ => false
        };
    }
}

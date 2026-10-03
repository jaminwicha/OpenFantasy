using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Ordinary care facts disclosed for this patient; missing capabilities never become empty owned facets.</summary>
internal sealed class FarmCarePresentation
{
    private FarmCarePresentation(LocalWorldView view, LocalActorSelection? selection)
    {
        WorldId = view.WorldId;
        BranchId = view.BranchId;
        ActorId = view.PlayerActorId;
        Selection = selection;
        ActorName = view.Actors.Single(actor => actor.ActorId == ActorId).DisplayName;
        HasRecovery = view.Restorative is not null;
        HasCareDisposition = view.Restorative is not null;
        Remedies = view.Restorative?.SelfUseOffers.ToArray() ?? [];
        Permissions = view.Restorative?.PermissionOffers.ToArray() ?? [];
    }

    private FarmCarePresentation(LocalSelectedPlayerObservation observation, LocalControlledActorView view)
    {
        WorldId = view.WorldId;
        BranchId = view.BranchId;
        ActorId = view.ActorId;
        Selection = observation.Selection;
        ActorName = view.Actors.Single(actor => actor.ActorId == ActorId).DisplayName;
        HasRecovery = view.Recovery is not null;
        HasCareDisposition = view.CareDisposition is not null;
        Remedies = view.Recovery?.SelfUseOffers.ToArray() ?? [];
        Permissions = view.CareDisposition?.PermissionOffers.ToArray() ?? [];
    }

    internal EntityId WorldId { get; }
    internal EntityId BranchId { get; }
    internal EntityId ActorId { get; }
    internal LocalActorSelection? Selection { get; }
    internal string ActorName { get; }
    internal bool HasRecovery { get; }
    internal bool HasCareDisposition { get; }
    internal IReadOnlyList<LocalRestorativeUseView> Remedies { get; }
    internal IReadOnlyList<LocalCarePermissionView> Permissions { get; }
    internal bool HasChoices => Remedies.Count != 0 || Permissions.Count != 0;

    internal static FarmCarePresentation From(LocalWorldView view) => new(view, null);
    internal static FarmCarePresentation From(LocalSelectedPlayerObservation observation) =>
        observation.ControlledView is { } controlled ? new(observation, controlled)
            : new(observation.HomeView, observation.Selection);

    internal bool Matches(FarmActionScope scope) => WorldId == scope.WorldId && BranchId == scope.BranchId
        && ActorId == scope.ActorId && Selection == scope.Selection;
}

using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal sealed record FarmMarketScope(EntityId WorldId, EntityId BranchId, EntityId ActorId,
    LocalActorSelection? Selection);

/// <summary>Only the selected shopper's disclosed trade facts. Inventory and history remain independent permissions.</summary>
internal sealed class FarmMarketPresentation
{
    private FarmMarketPresentation(LocalWorldView view, LocalActorSelection? selection, bool shared)
    {
        Scope = new(view.WorldId, view.BranchId, view.PlayerActorId, selection);
        Actors = view.Actors;
        ActorRevision = Actors.Single(actor => actor.ActorId == ActorId).PresenceRevision;
        Markets = view.Markets;
        PaidQuotes = view.PaidQuotes;
        CurrencyBalance = view.Progression?.CurrencyBalance;
        CurrencyUnit = view.Progression?.CurrencyUnit ?? "coins";
        CanReadHistory = true;
        SharedWorld = shared;
        ContextBlocker = view.HasReadingHold ? "Finish the current conversation before trading."
            : view.IsResolvingConsequences ? "Wait for the current world changes to finish."
            : view.Care?.PendingAcknowledgmentId is not null ? "The accepted recovery treatment must finish first."
            : view.EncounterLifecycle is { Phase: not EncounterLifecyclePhase.Exploration }
                ? "Trading happens during ordinary exploration."
            : view.EncounterLifecycle is { PlayerCanAct: false } ? "You cannot act until your current recovery ends." : null;
    }

    private FarmMarketPresentation(LocalSelectedPlayerObservation observation, LocalControlledActorView view, bool shared)
    {
        var trade = view.Trade ?? throw new InvalidOperationException("Trade permission is not available for this character.");
        Scope = new(view.WorldId, view.BranchId, view.ActorId, observation.Selection);
        Actors = view.Actors;
        ActorRevision = Actors.Single(actor => actor.ActorId == ActorId).PresenceRevision;
        Markets = trade.Markets;
        PaidQuotes = trade.Quotes;
        CurrencyBalance = trade.Wallet?.Balance;
        CurrencyUnit = trade.Wallet?.Unit ?? "coins";
        CanReadHistory = (view.Capabilities & WorldControlCapabilities.PrivateHistory) != 0;
        SharedWorld = shared;
        ContextBlocker = view.HasReadingHold ? "Finish the current conversation before trading."
            : view.IsResolvingConsequences ? "Wait for the current world changes to finish."
            : observation.HomeView.EncounterLifecycle is { Phase: not EncounterLifecyclePhase.Exploration }
                ? "Trading happens during ordinary exploration."
            : Actors.Single(actor => actor.ActorId == ActorId).IsIncapacitated ? "This character cannot act while incapacitated."
            : view.Recovery?.Care?.PendingAcknowledgmentId is not null ? "The accepted recovery treatment must finish first."
            : view.Combat?.Lifecycle is { PlayerCanAct: false } ? "This character cannot act until current recovery ends." : null;
    }

    internal FarmMarketScope Scope { get; }
    internal EntityId WorldId => Scope.WorldId;
    internal EntityId BranchId => Scope.BranchId;
    internal EntityId ActorId => Scope.ActorId;
    internal LocalActorSelection? Selection => Scope.Selection;
    internal ulong ActorRevision { get; }
    internal LocalMarketView? Markets { get; }
    internal IReadOnlyList<PaidQuoteView> PaidQuotes { get; }
    internal IReadOnlyList<LocalActorView> Actors { get; }
    internal ulong? CurrencyBalance { get; }
    internal string CurrencyUnit { get; }
    internal bool CanReadHistory { get; }
    internal bool SharedWorld { get; }
    internal bool IsDelegated => Selection?.ControlWitness is not null;
    internal string? ContextBlocker { get; }
    internal string Name(EntityId actor) => Actors.FirstOrDefault(value => value.ActorId == actor)?.DisplayName ?? "Merchant";
    internal static FarmMarketPresentation From(LocalWorldView view, bool shared = false) => new(view, null, shared);
    internal static FarmMarketPresentation From(LocalSelectedPlayerObservation observation, bool shared = false) =>
        observation.ControlledView is { } controlled ? new(observation, controlled, shared)
            : new(observation.HomeView, observation.Selection, shared);
}

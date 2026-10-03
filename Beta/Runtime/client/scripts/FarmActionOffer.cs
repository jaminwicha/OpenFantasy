using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Identity of the player, world and session an offer was read from. Submission checks these before
/// building a command so a snapshot from another session, world or branch can never be sent.
/// </summary>
internal sealed record FarmActionScope(EntityId PrincipalId, EntityId SessionId, EntityId ActorId,
    EntityId WorldId, EntityId BranchId, ulong ExpectedActorRevision)
{
    internal LocalActorSelection? Selection { get; init; }

    internal static FarmActionScope From(TrustedCommandDelivery delivery, LocalSelectedPlayerObservation observation)
    {
        LocalActorSelection selected = observation.Selection;
        var actors = observation.ControlledView?.Actors ?? observation.HomeView.Actors;
        return new(delivery.PrincipalId, delivery.SessionId, selected.ActorId, observation.HomeView.WorldId,
            observation.HomeView.BranchId, actors.Single(actor => actor.ActorId == selected.ActorId).PresenceRevision)
        { Selection = selected };
    }

    internal bool BelongsTo(TrustedCommandDelivery delivery, LocalSelectedPlayerObservation observation) =>
        Selection == observation.Selection && PrincipalId == delivery.PrincipalId && SessionId == delivery.SessionId
        && ActorId == observation.Selection.ActorId && WorldId == observation.HomeView.WorldId
        && BranchId == observation.HomeView.BranchId;

    internal static FarmActionScope From(TrustedCommandDelivery delivery, LocalWorldView view) => new(
        delivery.PrincipalId, delivery.SessionId, view.PlayerActorId, view.WorldId, view.BranchId,
        view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).PresenceRevision);

    /// <summary>Identity match only; a changed actor revision is left for the host's ordinary rejection.</summary>
    internal bool BelongsTo(TrustedCommandDelivery delivery, LocalWorldView view) =>
        PrincipalId == delivery.PrincipalId && SessionId == delivery.SessionId
        && ActorId == view.PlayerActorId && WorldId == view.WorldId && BranchId == view.BranchId;
}

/// <summary>
/// One presented action read from a public snapshot: what it does, to whom, what it spends and why it
/// may be blocked. The captured intent is submitted verbatim; nothing is reselected at submission.
/// This is client advice for display and commitment, not an authorization token.
/// </summary>
internal sealed class FarmActionOffer
{
    private const string BlockedIntentMessage = "A blocked action offer carries no submittable intent.";

    internal FarmActionOffer(FarmSemanticAction action, FarmActionScope scope, string label, string targetName,
        EntityId? targetId, string costText, string completionText, WorldActionIntent? intent,
        string? blockingReason, EntityId? dialogueId = null, string? detailText = null, EntityId? commandId = null)
        : this(action, scope, label, targetName, targetId, costText, completionText, intent, blockingReason, dialogueId,
            detailText, commandId, opensPanel: false)
    {
    }

    private FarmActionOffer(FarmSemanticAction action, FarmActionScope scope, string label, string targetName,
        EntityId? targetId, string costText, string completionText, WorldActionIntent? intent,
        string? blockingReason, EntityId? dialogueId, string? detailText, EntityId? commandId, bool opensPanel)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(costText);
        if (blockingReason is null && intent is null && !opensPanel)
        {
            throw new ArgumentException("An available offer requires the exact intent it will submit.");
        }
        if (opensPanel && (intent is not null || commandId is not null))
        {
            throw new ArgumentException("A panel choice carries no world intent or command identity.");
        }
        if (commandId is not null && intent is null)
        {
            throw new ArgumentException("A fixed command identity belongs to the intent that names it.");
        }
        OpensPanel = opensPanel;
        CommandId = commandId;
        Action = action;
        Scope = scope;
        Label = label;
        TargetName = targetName;
        TargetId = targetId;
        CostText = costText;
        CompletionText = completionText;
        Intent = intent;
        BlockingReason = blockingReason;
        DialogueId = dialogueId;
        DetailText = detailText;
    }

    internal FarmSemanticAction Action { get; }
    internal FarmActionScope Scope { get; }
    /// <summary>Short verb shown on the control, without key hints.</summary>
    internal string Label { get; }
    /// <summary>Display name of the real target; the player's own pack for self-directed work.</summary>
    internal string TargetName { get; }
    /// <summary>Identity of an external target, or null when the action has none.</summary>
    internal EntityId? TargetId { get; }
    /// <summary>Known essential spend or commitment, always stated even when nothing is spent.</summary>
    internal string CostText { get; }
    /// <summary>Optional known consequence, such as a reward or remaining effort.</summary>
    internal string? DetailText { get; }
    /// <summary>Message shown after the host applies this exact intent.</summary>
    internal string CompletionText { get; }
    /// <summary>Exact request captured at creation, or null when the action is blocked.</summary>
    internal WorldActionIntent? Intent { get; }
    /// <summary>Null when the offer can be submitted; otherwise the known reason it cannot.</summary>
    internal string? BlockingReason { get; }
    /// <summary>Addressed conversation this offer was read under, when it only applies inside it.</summary>
    internal EntityId? DialogueId { get; }
    internal bool CanExecute => BlockingReason is null;
    internal bool SpendsSomething => CostText != FarmActionOffers.NoCost;

    /// <summary>
    /// Choosing it opens a client panel, such as the market stall; it carries no intent and the session refuses
    /// to submit it. Any commitment inside the panel is a separate offer with its own exact intent.
    /// </summary>
    internal bool OpensPanel { get; }

    internal FarmActionOffer WithScope(FarmActionScope scope) => new(Action, scope, Label, TargetName, TargetId,
        CostText, CompletionText, Intent, BlockingReason, DialogueId, DetailText, CommandId, OpensPanel);

    /// <summary>
    /// The command identity the captured intent already names, such as a paid acceptance that restates its own
    /// command; null lets submission allocate a new identity.
    /// </summary>
    internal EntityId? CommandId { get; }

    /// <summary>A navigable choice that opens a panel and never becomes a world command by itself.</summary>
    internal static FarmActionOffer Panel(FarmSemanticAction action, FarmActionScope scope, string label, string targetName,
        EntityId? targetId, string detailText, string? blockingReason) =>
        new(action, scope, label, targetName, targetId, FarmActionOffers.NoCost, "", null, blockingReason, null, detailText,
            null, opensPanel: true);

    /// <summary>Builds the command from the captured scope and intent; the identity is the only new value.</summary>
    internal WorldActionCommand ToCommand(EntityId commandId)
    {
        WorldActionIntent intent = Intent ?? throw new InvalidOperationException(BlockedIntentMessage);
        if (CommandId is { } named && named != commandId)
        {
            throw new InvalidOperationException("This intent names its own command identity.");
        }
        return new WorldActionCommand(commandId, Scope.PrincipalId, Scope.SessionId, Scope.ActorId,
            Scope.WorldId, Scope.BranchId, Scope.ExpectedActorRevision, intent, Scope.Selection?.ControlWitness);
    }
}

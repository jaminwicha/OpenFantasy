using OpenFantasy.Core;

namespace OpenFantasy.Client;

internal enum FarmCareChoiceKind { UseRemedy, WithdrawPermission }
internal readonly record struct FarmCareChoiceId(FarmCareChoiceKind Kind, EntityId Id);
internal sealed record FarmCareEntry(FarmCareChoiceId Key, string Label, FarmActionOffer Offer);

/// <summary>Explicit remedy or permission selection, with one exact reviewed action and no automatic replacement.</summary>
internal sealed class FarmCareLibraryModel
{
    private readonly Dictionary<FarmCareChoiceId, string> _choiceReferences = [];

    internal FarmActionScope? Scope { get; private set; }
    internal string ActorName { get; private set; } = "Selected character";
    internal IReadOnlyList<FarmCareEntry> Entries { get; private set; } = [];
    internal FarmCareChoiceId? SelectedId { get; private set; }
    internal FarmCareEntry? Selected => Entries.FirstOrDefault(entry => entry.Key == SelectedId);
    internal FarmActionOffer? PreparedOffer { get; private set; }
    internal string? Notice { get; private set; }

    internal void Refresh(FarmCarePresentation presentation, FarmActionScope scope)
    {
        if (!presentation.Matches(scope)) throw new ArgumentException("Care presentation and action scope name different patients.");
        if (Scope is not null && !SameLifetime(Scope, scope)) Clear();
        Scope = scope;
        ActorName = presentation.ActorName;
        var entries = presentation.Remedies.Select(use => new FarmCareEntry(
                new(FarmCareChoiceKind.UseRemedy, use.DefinitionId), $"{use.ItemName} · {use.HealHitPoints} HP",
                RestorativeActionOffers.Use(scope, presentation.ActorName, use)))
            .Concat(presentation.Permissions.Select(permission => new FarmCareEntry(
                new(FarmCareChoiceKind.WithdrawPermission, permission.PermissionId),
                RestorativeActionOffers.PermissionLabel(permission),
                RestorativeActionOffers.Withdraw(scope, permission))))
            .OrderBy(entry => entry.Key.Kind).ThenBy(entry => entry.Key.Id).ToArray();
        Entries = Array.AsReadOnly(DistinguishChoices(entries));
        if (PreparedOffer is { } review && (Selected is not { } current || !SameCapture(review, current.Offer)))
            DiscardReview("This care choice changed. Review it again before confirming.");
        // A removed choice remains absent. Refresh never switches to a different medicine or agreement.
    }

    internal bool Select(FarmCareChoiceId key)
    {
        if (!Entries.Any(entry => entry.Key == key)) return false;
        if (SelectedId != key) { SelectedId = key; DiscardReview(); }
        return true;
    }

    internal bool Prepare()
    {
        if (Selected?.Offer is not { CanExecute: true } offer)
        {
            DiscardReview(Selected?.Offer.BlockingReason ?? "Choose a medicine or aid permission first.");
            return false;
        }
        PreparedOffer = offer;
        Notice = offer.Action == FarmSemanticAction.RevokeCarePermission
            ? "Reviewed: this exact permission will be permanently withdrawn. Nothing has changed yet."
            : "Reviewed: this exact dose will be used. Nothing is spent or reserved yet.";
        return true;
    }

    internal void DiscardReview(string? reason = null) { PreparedOffer = null; Notice = reason; }
    internal void Clear()
    {
        _choiceReferences.Clear();
        Scope = null;
        ActorName = "Selected character";
        Entries = [];
        SelectedId = null;
        DiscardReview();
    }

    private FarmCareEntry[] DistinguishChoices(FarmCareEntry[] entries)
    {
        // References stay attached for this visit even when a colliding permission later disappears.
        // They identify disclosed choices only; they confer no authority and never select an action.
        foreach (var group in entries.GroupBy(entry => (entry.Key.Kind, entry.Label)).Where(group => group.Count() > 1))
        {
            var identities = group.Select(entry => entry.Key.Id.ToString()).ToArray();
            int width = Math.Max(6, group.Max(entry => _choiceReferences.GetValueOrDefault(entry.Key)?.Length ?? 0));
            while (identities.Select(identity => identity[^width..]).Distinct().Count() != identities.Length) width++;
            foreach (var entry in group) _choiceReferences[entry.Key] = entry.Key.Id.ToString()[^width..];
        }
        foreach (var removed in _choiceReferences.Keys.Where(key => !entries.Any(entry => entry.Key == key)).ToArray())
            _choiceReferences.Remove(removed);
        return entries.Select(entry =>
        {
            if (!_choiceReferences.TryGetValue(entry.Key, out string? reference)) return entry;
            var offer = entry.Offer;
            string title = $"Ref {reference} · ";
            string kind = entry.Key.Kind == FarmCareChoiceKind.UseRemedy ? "medicine policy" : "aid permission";
            var distinguished = new FarmActionOffer(offer.Action, offer.Scope, title + offer.Label,
                offer.TargetName, offer.TargetId, offer.CostText, offer.CompletionText, offer.Intent,
                offer.BlockingReason, offer.DialogueId,
                $"{offer.DetailText}\nChoice reference ({kind}): {entry.Key.Id}", offer.CommandId);
            return entry with { Label = title + entry.Label, Offer = distinguished };
        }).ToArray();
    }

    internal static bool SameLifetime(FarmActionScope left, FarmActionScope right) =>
        left.PrincipalId == right.PrincipalId && left.SessionId == right.SessionId && left.ActorId == right.ActorId
        && left.WorldId == right.WorldId && left.BranchId == right.BranchId && left.Selection == right.Selection;

    private static bool SameCapture(FarmActionOffer left, FarmActionOffer right) => right.CanExecute
        && left.Scope == right.Scope && left.Action == right.Action && left.TargetId == right.TargetId
        && left.Intent == right.Intent && left.Label == right.Label && left.CostText == right.CostText
        && left.DetailText == right.DetailText;
}

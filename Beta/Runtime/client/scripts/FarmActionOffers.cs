using OpenFantasy.Core;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Reads one public snapshot into the coherent set of offers the farm presents. Every selection
/// (nearest plant, tool, ingredient lots, quest operation) happens here, once, and is frozen in the
/// returned offers. The host remains the only judge of reach, stock and revisions.
/// </summary>
internal sealed class FarmActionOffers
{
    internal const ulong WoodGiftQuantity = 6;
    internal const string NoCost = "No cost";
    internal const string SpeakFirst = "Speak with Wren to learn about available tasks.";

    private readonly TrustedCommandDelivery _delivery;
    private readonly EntityId _wrenActorId;
    private readonly EntityId _woodDefinitionId;
    private readonly EntityId? _berryDefinitionId;

    internal FarmActionOffers(TrustedCommandDelivery delivery, EntityId wrenActorId, EntityId woodDefinitionId,
        EntityId? berryDefinitionId)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        _delivery = delivery;
        _wrenActorId = wrenActorId;
        _woodDefinitionId = woodDefinitionId;
        _berryDefinitionId = berryDefinitionId;
    }

    /// <summary>Omits domains the save does not admit; blocked known actions stay present with a reason.</summary>
    internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> Create(LocalWorldView view, FarmTarget? selectedTarget = null,
        FarmGiftSelection? giftSelection = null, FarmMarketDraft? marketDraft = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        FarmActionScope scope = FarmActionScope.From(_delivery, view);
        if (EncounterActionOffers.Create(view, scope, selectedTarget) is { } encounterOffers)
        {
            var contextual = encounterOffers.ToDictionary();
            MaterialProposalActionOffers.Add(view, scope, giftSelection, contextual, _woodDefinitionId, _berryDefinitionId);
            return new ReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>(contextual);
        }
        var offers = new Dictionary<FarmSemanticAction, FarmActionOffer>
        {
            [FarmSemanticAction.Speak] = Speak(view, scope),
            [FarmSemanticAction.Give] = view.Progression is null ? GiveWood(view, scope) : QuestOperation(view, scope),
            [FarmSemanticAction.CloseDialogue] = CloseDialogue(view, scope),
        };
        FarmProductionActionOffers.Add(offers, FarmProductionPresentation.From(view), scope,
            selectedTarget, _woodDefinitionId, _berryDefinitionId);
        RestorativeActionOffers.Add(view, scope, selectedTarget, offers);
        MaterialProposalActionOffers.Add(view, scope, giftSelection, offers, _woodDefinitionId, _berryDefinitionId);
        MarketActionOffers.Add(view, scope, marketDraft, offers);
        if (selectedTarget is { } target && (target.Id != _wrenActorId
            || view.Actors.All(actor => actor.ActorId != target.Id)))
        {
            string reason = target.Feet is null ? "The selected target is no longer visible. Choose another target or clear the selection."
                : "This action needs a person. Choose Wren or clear the selection.";
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give })
            {
                offers[action] = new FarmActionOffer(action, scope, action == FarmSemanticAction.Speak ? "Talk" : "Give",
                    target.Name, target.Id, NoCost, "", intent: null, reason);
            }
        }
        if (view.EncounterLifecycle is { PlayerCanAct: false })
        {
            string reason = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).IsIncapacitated
                ? "You are incapacitated and cannot act."
                : "You are recovering; advance world time until you are ready.";
            foreach (var (action, offer) in offers.ToArray())
            {
                if (action == FarmSemanticAction.CloseDialogue) continue;
                offers[action] = new FarmActionOffer(action, offer.Scope, offer.Label, offer.TargetName,
                    offer.TargetId, offer.CostText, offer.CompletionText, null, reason, offer.DialogueId, offer.DetailText);
            }
        }
        return new ReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>(offers);
    }

    private FarmActionOffer Speak(LocalWorldView view, FarmActionScope scope)
    {
        LocalActorView? wren = view.Actors.FirstOrDefault(actor => actor.ActorId == _wrenActorId);
        if (wren is null)
        {
            return new FarmActionOffer(FarmSemanticAction.Speak, scope, "Talk", "Wren", null, NoCost, "",
                intent: null, "Wren is not here.");
        }
        string? blocker = view.Dialogue is not null ? "You are already in a conversation." : null;
        return new FarmActionOffer(FarmSemanticAction.Speak, scope, "Talk", wren.DisplayName, wren.ActorId, NoCost,
            $"You spoke with {wren.DisplayName}.", new SpeakToActorIntent(wren.ActorId, wren.PresenceRevision), blocker);
    }

    /// <summary>The largest wood lot is chosen once; a later restock cannot swap the captured lot or revision.</summary>
    private FarmActionOffer GiveWood(LocalWorldView view, FarmActionScope scope)
    {
        LocalActorView? wren = view.Actors.FirstOrDefault(actor => actor.ActorId == _wrenActorId);
        string targetName = wren?.DisplayName ?? "Wren";
        LocalItemLotView? lot = view.PlayerInventory.Lots.Where(value => value.DefinitionId == _woodDefinitionId
                && value.Provenance == IngredientLotKind.Source)
            .OrderByDescending(value => value.Quantity).ThenBy(value => value.LotId).FirstOrDefault();
        if (wren is null || lot is null)
        {
            return new FarmActionOffer(FarmSemanticAction.Give, scope, "Give", targetName, wren?.ActorId,
                $"{WoodGiftQuantity} wood", "", intent: null,
                wren is null ? "Wren is not here." : "You have no wood in your pack.");
        }
        ulong quantity = Math.Min(WoodGiftQuantity, lot.Quantity);
        var intent = new GiveItemIntent(wren.ActorId, wren.PresenceRevision, lot.LotId, lot.Revision,
            view.PlayerInventory.CustodianId, view.PlayerInventory.Revision, quantity);
        return new FarmActionOffer(FarmSemanticAction.Give, scope, "Give", wren.DisplayName, wren.ActorId,
            $"{quantity} wood", $"You handed {wren.DisplayName} wood from your pack.", intent, blockingReason: null);
    }

    /// <summary>
    /// Accept or deliver is decided by the addressed quest's current status and frozen with the
    /// conversation it was read under; a later status change cannot turn one into the other.
    /// </summary>
    private static FarmActionOffer QuestOperation(LocalWorldView view, FarmActionScope scope)
    {
        LocalDialogueView? dialogue = view.Dialogue;
        LocalQuestView? quest = dialogue is null ? null
            : FarmQuestActions.AddressedQuest(view);
        if (dialogue is null || quest is null)
        {
            return new FarmActionOffer(FarmSemanticAction.Give, scope, "Ask about tasks", "Wren", null,
                "Not yet known", "", intent: null, SpeakFirst);
        }
        string reward = $"{quest.Title} · Reward: {quest.RewardExperience} XP and {quest.RewardCurrency} {quest.CurrencyUnit}";
        if (quest.Status is null)
        {
            string? blocker = quest.CanAccept ? null : "This task cannot be accepted right now.";
            return new FarmActionOffer(FarmSemanticAction.Give, scope, "Accept task", dialogue.SpeakerName,
                quest.GiverActorId, $"Commit to {quest.RequiredQuantity} wood", $"You accepted {quest.Title}.",
                blocker is null ? FarmQuestActions.Request(view, quest) : null, blocker, dialogue.DialogueId, reward);
        }
        string? deliveryBlocker = quest.Status == ActorQuestStatus.Completed
            ? $"You have already completed {quest.Title}."
            : quest.CanComplete ? null
            : $"You need {quest.RequiredQuantity} available wood (you have {quest.EligibleQuantity}).";
        return new FarmActionOffer(FarmSemanticAction.Give, scope, "Deliver", dialogue.SpeakerName,
            quest.GiverActorId, $"{quest.RequiredQuantity} wood", "You delivered the supplies and received Wren's reward.",
            deliveryBlocker is null ? FarmQuestActions.Request(view, quest) : null, deliveryBlocker, dialogue.DialogueId, reward);
    }

    private static FarmActionOffer CloseDialogue(LocalWorldView view, FarmActionScope scope)
    {
        if (view.Dialogue is not { } dialogue)
        {
            return new FarmActionOffer(FarmSemanticAction.CloseDialogue, scope, "Continue", "Nobody", null, NoCost,
                "", intent: null, "There is no addressed conversation to close.");
        }
        return new FarmActionOffer(FarmSemanticAction.CloseDialogue, scope, "Continue", dialogue.SpeakerName,
            dialogue.SpeakerActorId, NoCost, "The conversation ended.", new CloseDialogueIntent(dialogue.DialogueId),
            blockingReason: null, dialogue.DialogueId);
    }

}

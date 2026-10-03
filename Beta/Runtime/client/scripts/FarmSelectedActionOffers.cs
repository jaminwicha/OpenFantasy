using System.Collections.ObjectModel;
using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Offers from purpose-scoped disclosures. An absent facet never becomes empty owned state.</summary>
internal static class FarmSelectedActionOffers
{
    internal static IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> Create(LocalSelectedPlayerObservation observation,
        FarmActionScope scope, FarmTarget? target, EntityId defaultSpeaker, FarmGiftSelection? gifts = null,
        EntityId? woodDefinitionId = null, EntityId? berryDefinitionId = null, FarmMarketDraft? market = null)
    {
        LocalControlledActorView view = observation.ControlledView!;
        Dictionary<FarmSemanticAction, FarmActionOffer> offers = [];
        void Add(FarmSemanticAction action, string label, string name, EntityId? id, string cost,
            WorldActionIntent? intent, string? blocked = null, string? detail = null) => offers[action] =
            new(action, scope, label, name, id, cost, label + " completed.", intent,
                blocked ?? (intent is null ? "This choice is not currently available." : null), detailText: detail);

        if (view.Combat is { } combat)
        {
            if (combat.OutcomeAcknowledgment is { } acknowledgment)
                Add(FarmSemanticAction.ReleaseEncounter, "Accept outcome", "Encounter outcome", null,
                    FarmActionOffers.NoCost, acknowledgment, detail: "Acknowledges this character's outcome. "
                    + "Does not heal or move anyone; completion waits for the other required acknowledgments and pending care.");
            else if (combat.Actions is { } actions)
            {
                var ability = actions.AbilityOffers.FirstOrDefault(value => value.TargetActorId == target?.Id);
                if (ability is not null)
                    Add(FarmSemanticAction.Strike, ability.DisplayName, target!.Name, target.Id,
                        $"{ability.ManaCost} MP · {ability.ReadinessDelay / 100m:0.##} s recovery",
                        new QueueEncounterAbilityIntent(actions.EncounterId, actions.TimingRevision,
                            ability.AbilityDefinitionId, ability.TargetActorId),
                        ability.BlockedReason == LocalEncounterActionBlockedReason.None ? null : ability.BlockedReason.ToString());
                Add(FarmSemanticAction.Wait, "Wait", "Readiness", null, $"{actions.WaitDelay / 100m:0.##} s recovery",
                    new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision),
                    actions.WaitBlockedReason == LocalEncounterActionBlockedReason.None ? null : actions.WaitBlockedReason.ToString());
            }
            else if (combat.Lifecycle is { } lifecycle && target is not null
                && lifecycle.Entries.FirstOrDefault(entry => entry.TargetActorId == target.Id) is { } entry)
                Add(FarmSemanticAction.EnterEncounter, "Confront", target.Name, target.Id, FarmActionOffers.NoCost,
                    new EnterEncounterIntent(entry.ScenarioId, entry.EncounterId, lifecycle.EncounterCount, entry.TargetRevision),
                    entry.CanEnter ? null : "This confrontation cannot begin from your current position or condition.");
        }
        if (view.Retreat?.Offer is { CanQueue: true } retreat)
            Add(FarmSemanticAction.Retreat, "Retreat", RetreatOffers.Destination(retreat.Origin, retreat.Destination), null,
                RetreatOffers.Steps(retreat), new QueueEncounterRetreatIntent(retreat.EncounterId, retreat.TimingRevision,
                    retreat.Destination, retreat.Route), detail: RetreatOffers.Detail + " " + RetreatOffers.Charges(retreat));
        if (view.Recovery is { } recovery)
        {
            var choices = recovery.Care?.Offers.Where(value => target is null || value.HelperActorId == target.Id).ToArray();
            if (choices is { Length: 1 })
                Add(FarmSemanticAction.AcknowledgeCare, "Acknowledge care", choices[0].HelperName, choices[0].HelperActorId,
                    FarmActionOffers.NoCost, choices[0].Intent, choices[0].CanAcknowledge ? null : "The agreed care is unavailable.",
                    "Acknowledges a prior agreement; the helper must provide a separate real dose.");
            if (recovery.Care?.Decision is { Treatment: { } treatment } decision)
                Add(FarmSemanticAction.AdministerCare, "Administer remedy", decision.PatientName, decision.PatientActorId,
                    "1 dose · helper recovery", treatment);
        }
        if (view.CareDisposition?.Care?.Decision is { } disposition)
        {
            if (disposition.Cancellation is { } cancel)
                Add(FarmSemanticAction.CancelCare, "Cancel care request", "Care request", null, FarmActionOffers.NoCost, cancel);
            if (disposition.Decline is { } decline)
                Add(FarmSemanticAction.DeclineCare, "Decline care request", disposition.PatientName,
                    disposition.PatientActorId, FarmActionOffers.NoCost, decline);
        }
        if (view.Conversation is { } conversation)
        {
            if (conversation.Dialogue is { } dialogue)
                offers[FarmSemanticAction.CloseDialogue] = new(FarmSemanticAction.CloseDialogue, scope, "Continue",
                    dialogue.SpeakerName, dialogue.SpeakerActorId, FarmActionOffers.NoCost, "Conversation ended.",
                    new CloseDialogueIntent(dialogue.DialogueId), null, dialogue.DialogueId);
            else if (view.Actors.FirstOrDefault(actor => actor.ActorId == (target?.Id ?? defaultSpeaker)) is { } speaker)
                Add(FarmSemanticAction.Speak, "Talk", speaker.DisplayName, speaker.ActorId, FarmActionOffers.NoCost,
                    new SpeakToActorIntent(speaker.ActorId, speaker.PresenceRevision));
            var quests = conversation.Quests.Where(quest => target is null || quest.GiverActorId == target.Id).ToArray();
            if (quests.Length == 1 && conversation.Requests?.Prepare(quests[0].DefinitionId) is { } questIntent)
                Add(FarmSemanticAction.Give, "Quest", "Selected quest", quests[0].GiverActorId,
                    questIntent is CompleteDeliveryQuestIntent ? $"{quests[0].RequiredQuantity} quest items" : FarmActionOffers.NoCost,
                    questIntent);
        }
        RestorativeActionOffers.Add(FarmCarePresentation.From(observation), scope, target, offers);
        if (view.Production is not null)
            FarmProductionActionOffers.Add(offers, FarmProductionPresentation.From(observation), scope,
                target, woodDefinitionId ?? default, berryDefinitionId);
        if (view.Inventory is not null)
            MaterialProposalActionOffers.Add(FarmInventoryPresentation.From(observation), scope, gifts, offers,
                woodDefinitionId, berryDefinitionId);
        if (view.Trade is not null && market is not null)
            MarketActionOffers.Add(FarmMarketPresentation.From(observation, shared: true), scope, market, offers);
        return new ReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>(offers);
    }

    internal static FarmActionOffer ConfirmQuote(FarmActionScope scope, PaidQuoteView quote, ulong units,
        TrustedCommandDelivery delivery, string currencyUnit)
    {
        EntityId command = EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));
        var intent = quote.PrepareAcceptance(command, delivery, units);
        bool buying = quote.Side == SpotQuoteSide.IssuerSells;
        return new(FarmSemanticAction.ConfirmTrade, scope, buying ? "Confirm purchase" : "Confirm sale",
            "Exact reviewed merchant offer", quote.IssuerActorId,
            buying ? $"{quote.TotalFor(units)} {currencyUnit}" : $"{units} units",
            "The reviewed trade completed.", intent, null, detailText: $"Quote {quote.QuoteId}; {units} units. "
                + "Confirms these exact terms once. A stale offer is refused, never replaced.", commandId: command);
    }
}

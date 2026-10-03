using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Present choices of one actively delegated actor. A missing section is withheld, not empty owned state.
/// No world root, full player view, controller credential or completed history is retained. PrivateHistory
/// is a recorded permission only: this projection does not implement a delegated history reader.
/// </summary>
public sealed class LocalControlledActorView
{
    private LocalControlledActorView(WorldSnapshot state, LocalWorldView source, WorldControlCapabilities capabilities)
    {
        WorldId = source.WorldId;
        BranchId = source.BranchId;
        ActorId = source.PlayerActorId;
        Capabilities = capabilities;
        Calendar = source.Calendar;
        HasReadingHold = source.HasReadingHold;
        IsResolvingConsequences = source.IsResolvingConsequences;
        Actors = Array.AsReadOnly(source.Actors.ToArray());
        Regional = source.Regional;
        IsRegional = source.IsRegional;

        bool Has(WorldControlCapabilities scope) => (capabilities & scope) == scope;
        if (Has(WorldControlCapabilities.Combat))
        {
            var resources = source.Progression is { } current
                ? new LocalControlledCombatResources(current.Hp, current.MaxHp, current.Mp, current.MaxMp) : null;
            // Release hashes and terminal outcome objects are not part of current delegated combat choices.
            var lifecycle = source.EncounterLifecycle is { } phase ? phase with { ReleaseActionHash = null } : null;
            var acknowledgment = source.EncounterOutcome is
                { Stage: LocalOutcomeStage.AwaitingYourReading, CompletedAtSequence: null } outcome
                ? outcome.Acknowledgment : null;
            Combat = new(source.Encounter, lifecycle, source.EncounterActions,
                Array.AsReadOnly(source.EncounterActors.ToArray()), resources, acknowledgment);
        }
        if (Has(WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal) && source.Retreat is { } retreat)
            Retreat = new(retreat.Offer, retreat.Progress, retreat.OtherWithdrawing);
        if (Has(WorldControlCapabilities.Recovery))
        {
            var health = source.Progression is { } current ? new LocalControlledHealth(current.Hp, current.MaxHp) : null;
            Recovery = new(health, Array.AsReadOnly(source.Restorative?.SelfUseOffers.ToArray() ?? []),
                CurrentCare(source.Care, recovery: true));
        }
        if (Has(WorldControlCapabilities.CareDisposition))
            CareDisposition = new(Array.AsReadOnly(source.Restorative?.PermissionOffers
                .Where(permission => permission.IsActive).ToArray() ?? []), CurrentCare(source.Care, recovery: false));
        if (Has(WorldControlCapabilities.Inventory))
        {
            Inventory = source.PlayerInventory;
            if (source.MaterialProposals is { } proposals)
                MaterialProposals = new(Array.AsReadOnly(proposals.Offers
                    .Where(offer => offer.Status == MaterialProposalStatus.Open).ToArray()), proposals.Preparation);
        }
        if (Has(WorldControlCapabilities.Trade))
        {
            var wallet = source.Progression is { } current
                ? new LocalControlledWallet(current.CurrencyBalance, current.CurrencyUnit) : null;
            // Market showing terms disclose usable lots of that commodity for this purpose, not a full bag.
            Trade = new(Array.AsReadOnly(source.PaidQuotes.Where(quote => quote.Status == PaidQuoteStatus.Open).ToArray()),
                CurrentMarkets(source.Markets), wallet);
        }
        if (Has(WorldControlCapabilities.Production))
            Production = new(Array.AsReadOnly(source.GatheringNodes.ToArray()),
                Array.AsReadOnly(source.GatheringTools.ToArray()), source.PlayerGatherEffort, source.Worksite,
                source.Crafting, source.Crafting is null ? null : new LocalControlledCraftPreparation(state, source),
                Array.AsReadOnly(source.Construction.Where(project => !project.IsCompleted).ToArray()),
                LocalControlledConstructionPreparation.Project(state, source));
        if (Has(WorldControlCapabilities.ConversationAndQuests))
            Conversation = new(source.Dialogue,
                Array.AsReadOnly(source.Quests.Where(quest => quest.Status != ActorQuestStatus.Completed).ToArray()),
                source.Progression is null ? null : new LocalControlledQuestPreparation(source));
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId ActorId { get; }
    public WorldControlCapabilities Capabilities { get; }
    public WorldInstant Calendar { get; }
    public bool HasReadingHold { get; }
    public bool IsResolvingConsequences { get; }
    public bool IsRegional { get; }
    public ReadOnlyCollection<LocalActorView> Actors { get; }
    public LocalRegionObservation? Regional { get; }
    public LocalControlledCombatView? Combat { get; }
    public LocalControlledRetreatView? Retreat { get; }
    public LocalControlledRecoveryView? Recovery { get; }
    public LocalControlledCareDispositionView? CareDisposition { get; }
    public LocalInventoryView? Inventory { get; }
    public LocalMaterialProposalView? MaterialProposals { get; }
    public LocalControlledTradeView? Trade { get; }
    public LocalControlledProductionView? Production { get; }
    public LocalControlledConversationView? Conversation { get; }

    internal static LocalControlledActorView Project(WorldSnapshot state, EntityId homeActorId,
        TrustedCommandDelivery delivery, WorldControlWitness witness, bool careHelperSelectionOccupied = false,
        LocalRegionObserver? regions = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(witness);
        if (witness.GranteeActorId != homeActorId
            || !WorldActorCommandAuthority.IsCurrentWitness(state, witness.OwnerActorId, delivery, witness, witness.Capabilities))
            throw new ArgumentException("A delegated observation requires the exact current assignment and authenticated home actor.", nameof(witness));
        // This temporary full projection never escapes or becomes a field of the scoped observation.
        var source = new LocalWorldView(state, witness.OwnerActorId, careHelperSelectionOccupied, regions);
        return new(state, source, witness.Capabilities);
    }

    private static LocalCareView? CurrentCare(LocalCareView? care, bool recovery)
    {
        if (care is null || care.Closure is not null
            || care.Status is LocalCareStatus.Treated or LocalCareStatus.Cancelled or LocalCareStatus.Declined) return null;
        if (!recovery && care.PendingAcknowledgmentId is null) return null;
        var decision = care.Decision is { } current ? current with
        {
            Cancellation = recovery ? null : current.Cancellation,
            Decline = recovery ? null : current.Decline,
            Treatment = recovery ? current.Treatment : null
        } : null;
        // Disposition choices do not disclose the helper's treatment availability or selected dose.
        return new(recovery ? care.Offers : [], recovery ? care.Status : LocalCareStatus.Pending, care.PendingAcknowledgmentId,
            care.HelperActorId, care.HelperName, decision, closure: null);
    }

    private static LocalMarketView? CurrentMarkets(LocalMarketView? markets) => markets is null ? null
        : new(markets.AdvancingStep, markets.Stalls.Select(stall => new LocalMarketStallView(stall.MarketId,
            stall.MerchantActorId, stall.Commodity, stall.CommodityName, stall.MaximumUnitsPerOffer,
            stall.MaximumWindowSteps, stall.IsAvailableInPerson,
            stall.OwnShowing is { IsLive: true } showing ? showing : null, stall.OwnLots,
            stall.CapturedPlayerAccountId, stall.CapturedAdvancingStep)));
}

public sealed record LocalControlledCombatResources(int Hp, int MaxHp, int Mp, int MaxMp);
public sealed record LocalControlledHealth(int Hp, int MaxHp);
public sealed record LocalControlledWallet(ulong Balance, string Unit);
/// <summary>Current combat choices; the outcome intent exists only while this actor owes acknowledgment.</summary>
public sealed record LocalControlledCombatView(LocalEncounterView? Encounter, LocalEncounterLifecycleView? Lifecycle,
    LocalEncounterActionView? Actions, ReadOnlyCollection<LocalEncounterActorView> Actors, LocalControlledCombatResources? Resources,
    AcknowledgeEncounterOutcomeIntent? OutcomeAcknowledgment);
public sealed record LocalControlledRetreatView(LocalRetreatOffer? Offer, LocalRetreatProgress? Progress, bool OtherWithdrawing);
public sealed record LocalControlledRecoveryView(LocalControlledHealth? Health,
    ReadOnlyCollection<LocalRestorativeUseView> SelfUseOffers, LocalCareView? Care);
public sealed record LocalControlledCareDispositionView(ReadOnlyCollection<LocalCarePermissionView> PermissionOffers,
    LocalCareView? Care);
public sealed record LocalControlledTradeView(ReadOnlyCollection<PaidQuoteView> Quotes, LocalMarketView? Markets,
    LocalControlledWallet? Wallet);
public sealed record LocalControlledProductionView(ReadOnlyCollection<LocalGatherNodeView> GatheringNodes,
    ReadOnlyCollection<LocalGatherToolLotView> GatheringTools, uint? GatherEffort, LocalWorksiteView? Worksite,
    LocalCraftingView? Crafting, LocalControlledCraftPreparation? CraftPreparation,
    ReadOnlyCollection<LocalConstructionView> Construction, ReadOnlyCollection<LocalControlledConstructionPreparation> ConstructionWork);
public sealed record LocalControlledConversationView(LocalDialogueView? Dialogue, ReadOnlyCollection<LocalQuestView> Quests,
    LocalControlledQuestPreparation? Requests);

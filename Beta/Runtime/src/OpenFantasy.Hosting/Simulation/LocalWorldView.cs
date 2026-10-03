using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed record LocalActorView(EntityId ActorId, string DisplayName, SubunitPosition Feet,
    ulong PresenceRevision, ResidentControllerKind ControllerKind)
{
    public bool IsIncapacitated { get; init; }
}
public sealed record LocalItemLotView(EntityId LotId, EntityId DefinitionId, ulong Revision, ulong Quantity,
    IngredientLotKind Provenance = IngredientLotKind.Source, bool AvailableForDelivery = true);
public sealed record LocalDialogueView(EntityId DialogueId, EntityId SpeakerActorId, string SpeakerName, string Text);
public sealed record LocalEncounterView(EntityId EncounterId, ulong TimingRevision, ulong ElapsedTacticalUnits,
    bool PlayerIsDue, bool PlayerDecisionQueued, bool HasRequiredDecisions, bool IsOutcome = false);

public sealed record LocalEncounterActorView(EntityId ActorId, int CurrentHp, int MaximumHp, bool IsIncapacitated, ushort Side);

public sealed class LocalInventoryView
{
    internal LocalInventoryView(EntityId custodianId, ulong revision, IEnumerable<LocalItemLotView> lots)
    {
        CustodianId = custodianId;
        Revision = revision;
        Lots = Array.AsReadOnly(lots.OrderBy(lot => lot.LotId).ToArray());
        Quantity = Lots.Aggregate(0UL, (total, lot) => checked(total + lot.Quantity));
    }

    public EntityId CustodianId { get; }
    public ulong Revision { get; }
    public ulong Quantity { get; }
    public ReadOnlyCollection<LocalItemLotView> Lots { get; }
}

/// <summary>
/// Public projection of the admitted sight-complete local profile. This is not a global visibility
/// system; other actors' inventory, source claims, private commit counters and hashes are excluded.
/// </summary>
public sealed class LocalWorldView
{
    /// <summary>Rehydrates immutable presentation values without consulting or constructing authority state.</summary>
    internal LocalWorldView(EntityId worldId,
        EntityId branchId,
        EntityId playerActorId,
        CargoPresentation.LocalRegionObservation? regional,
        bool isRegional,
        WorldInstant calendar,
        bool hasReadingHold,
        bool isResolvingConsequences,
        LocalEncounterView? encounter,
        LocalEncounterLifecycleView? encounterLifecycle,
        LocalEncounterActionView? encounterActions,
        Retreat.LocalRetreatView? retreat,
        EncounterOutcome.LocalEncounterOutcomeView? encounterOutcome,
        IReadOnlyList<RoadThreat.LocalRoadThreatView>? roadThreats,
        LocalCareView? care,
        LocalRestorativeView? restorative,
        LocalMaterialProposalView? materialProposals,
        ReadOnlyCollection<PaidQuoteView> paidQuotes,
        LocalMarketView? markets,
        ReadOnlyCollection<LocalEncounterActorView> encounterActors,
        ReadOnlyCollection<LocalActorView> actors,
        LocalInventoryView playerInventory,
        ReadOnlyCollection<LocalGatherNodeView> gatheringNodes,
        ReadOnlyCollection<LocalGatherToolLotView> gatheringTools,
        uint? playerGatherEffort,
        LocalWorksiteView? worksite,
        LocalLiquidView? liquids,
        LocalCraftingView? crafting,
        LocalDialogueView? dialogue,
        LocalProgressionView? progression,
        ReadOnlyCollection<LocalQuestView> quests,
        ReadOnlyCollection<LocalConstructionView> construction, bool supportsCareDispositions = false,
        LocalMaterialDescriptions? materialDescriptions = null)
    {
        MaterialDescriptions = materialDescriptions;
        SupportsCareDispositions = supportsCareDispositions;
        WorldId = worldId;
        BranchId = branchId;
        PlayerActorId = playerActorId;
        Regional = regional;
        IsRegional = isRegional;
        Calendar = calendar;
        HasReadingHold = hasReadingHold;
        IsResolvingConsequences = isResolvingConsequences;
        Encounter = encounter;
        EncounterLifecycle = encounterLifecycle;
        EncounterActions = encounterActions;
        Retreat = retreat;
        EncounterOutcome = encounterOutcome;
        RoadThreats = roadThreats is null ? null : Array.AsReadOnly(roadThreats.ToArray());
        Care = care;
        Restorative = restorative;
        MaterialProposals = materialProposals;
        PaidQuotes = Array.AsReadOnly(paidQuotes.ToArray());
        Markets = markets;
        EncounterActors = Array.AsReadOnly(encounterActors.ToArray());
        Actors = Array.AsReadOnly(actors.ToArray());
        PlayerInventory = playerInventory;
        GatheringNodes = Array.AsReadOnly(gatheringNodes.ToArray());
        GatheringTools = Array.AsReadOnly(gatheringTools.ToArray());
        PlayerGatherEffort = playerGatherEffort;
        Worksite = worksite;
        Liquids = liquids;
        Crafting = crafting;
        Dialogue = dialogue;
        Progression = progression;
        Quests = Array.AsReadOnly(quests.ToArray());
        Construction = Array.AsReadOnly(construction.ToArray());
    }

    internal LocalWorldView(WorldSnapshot state, EntityId playerActorId, bool careHelperSelectionOccupied = false,
        CargoPresentation.LocalRegionObserver? regions = null)
    {
        if (regions is not null && regions.PlayerActorId != playerActorId)
        {
            throw new ArgumentException("A regional observation belongs to the view's own player.", nameof(regions));
        }
        // Projected from this same root, so a frame's terrain, water, actors and offers always agree.
        Regional = regions?.Observe(state);
        IsRegional = regions is not null;
        SupportsCareDispositions = LocalProfileBindings.AdmitsCareDispositions(state);
        WorldId = state.WorldId;
        BranchId = state.BranchId;
        PlayerActorId = playerActorId;
        Calendar = state.Calendar;
        HasReadingHold = state.HasReadingHold;
        EncounterLifecycle = LocalEncounterLifecycleView.Project(state, playerActorId);
        EncounterActions = LocalEncounterActionView.Project(state, playerActorId);
        Retreat = Simulation.Retreat.LocalRetreatView.Project(state, playerActorId);
        EncounterOutcome = Simulation.EncounterOutcome.LocalEncounterOutcomeView.Project(state, playerActorId);
        RoadThreats = Simulation.RoadThreat.LocalRoadThreatView.Project(state, playerActorId);
        Care = LocalCareView.Project(state, playerActorId, careHelperSelectionOccupied);
        Restorative = LocalRestorativeView.Project(state, playerActorId);
        MaterialProposals = LocalMaterialProposalView.Project(state, playerActorId);
        // Bounded live participant quotes only; archived outcomes need the explicit background inspection.
        PaidQuotes = Array.AsReadOnly(PaidQuoteProjection.Project(state, playerActorId).ToArray());
        Markets = LocalMarketView.Project(state, playerActorId);
        IsResolvingConsequences = state.PendingExcavation is not null;
        if (state.Pacing?.Encounter is { } timing)
        {
            Encounter = new LocalEncounterView(timing.EncounterId, timing.Revision, timing.ElapsedTacticalUnits,
                WorldEncounterActions.DueActors(state).Any(actor => actor.ActorId == playerActorId),
                // A committed retreat is the player's standing choice, so it never reads as an awaited decision.
                timing.Decisions.Any(decision => decision.ActorId == playerActorId) || WorldEncounterRetreats.HoldsActor(state, playerActorId),
                WorldEncounterActions.HasRequiredDecisions(state), WorldEncounterActions.IsOutcome(state));
        }
        EncounterActors = Array.AsReadOnly(state.EncounterActions is { } actions
            ? actions.Combatants.Select(actor =>
            {
                var resources = state.Progression!.GetActor(actor.ActorId);
                return new LocalEncounterActorView(actor.ActorId, resources.CurrentHp, resources.Stats.MaxHp,
                    resources.CurrentHp == 0, actor.Side);
            }).ToArray() : []);
        Actors = Array.AsReadOnly(state.Residents.Select(resident =>
        {
            var presence = state.Space.GetActor(resident.ActorId);
            return new LocalActorView(resident.ActorId, resident.DisplayName, presence.Feet,
                presence.Revision, resident.ControllerKind)
            {
                IsIncapacitated = state.Progression?.GetActor(resident.ActorId).CurrentHp == 0
            };
        }).ToArray());
        EntityId storage = state.GetResident(playerActorId).InventoryCustodianId;
        PlayerInventory = new LocalInventoryView(storage, state.Inventory.Custody.GetStorage(storage).Revision,
            state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage)
                .Select(lot => new LocalItemLotView(lot.LotId, lot.Definition.DefinitionId, lot.Revision, lot.Quantity,
                    IngredientLotKind.Source, lot.Protection == LotProtection.None && lot.ReservedBy is null))
                .Concat(state.Production?.Lots.Where(lot => lot.CustodianId == storage).Select(lot =>
                    new LocalItemLotView(lot.LotId, lot.Definition.DefinitionId, lot.Revision, lot.Quantity,
                        IngredientLotKind.Transformed, lot.Protection == LotProtection.None && lot.ReservedBy is null)) ?? []));
        Crafting = state.Production is { } production ? new LocalCraftingView(production, state.Construction is { } construction
            ? construction.IsConstructionRecipe : null) : null;
        GatheringNodes = Array.AsReadOnly(state.Gathering is { } gathering
            ? gathering.Nodes.Where(node => node.VisibilityAt(state.ProcessIntervals) != GatherNodeVisibility.Absent)
                .Select(node => new LocalGatherNodeView(node.NodeId, node.Definition.DisplayName, node.Feet,
                    node.Revision, node.Definition.Kind, node.Definition.RequiredToolKind,
                    (uint)node.RemainingSwings, node.VisibilityAt(state.ProcessIntervals)))
                .OrderBy(node => node.NodeId).ToArray()
            : []);
        List<LocalGatherToolLotView> tools = [];
        if (state.Gathering is { } admittedGathering)
        {
            PlayerGatherEffort = (uint)admittedGathering.GetActorWork(playerActorId).Effort;
            foreach (var lot in state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage))
            {
                var tool = admittedGathering.Tools.FirstOrDefault(value => value.Definition == lot.Definition);
                if (tool is not null)
                {
                    tools.Add(new LocalGatherToolLotView(lot.LotId, lot.Revision, tool.Kind, tool.Power));
                }
            }
        }
        GatheringTools = Array.AsReadOnly(tools.OrderBy(tool => tool.LotId).ToArray());
        Worksite = state.Excavation is not null ? new LocalWorksiteView(state, playerActorId) : null;
        Liquids = state.LiquidCells is not null ? new LocalLiquidView(state) : null;
        DialogueSession? dialogue = state.Dialogues.FirstOrDefault(value => value.ListenerActorId == playerActorId);
        if (dialogue is not null)
        {
            WorldResident speaker = state.GetResident(dialogue.SpeakerActorId);
            Dialogue = new LocalDialogueView(dialogue.DialogueId, speaker.ActorId, speaker.DisplayName,
                speaker.OpeningUtterance!.Text);
        }
        Quests = ProjectQuests(state, playerActorId, dialogue);
        Construction = Array.AsReadOnly(state.Construction?.Projects.Where(project => project.IsCompleted
                || state.Quests!.FindParticipation(playerActorId, project.QuestId) is not null)
            .Select(project => new LocalConstructionView(project.ProjectId, project.BuilderActorId,
                project.Definition.DisplayName, project.SiteFeet, project.WorkFeet, project.Progress.CompletedUnits,
                project.Definition.RequiredWork.Value, project.IsCompleted)).ToArray() ?? []);
        if (state.Progression is { } progression)
        {
            var actor = progression.GetActor(playerActorId);
            Progression = new LocalProgressionView(actor.Revision, actor.Profile.DisplayName, actor.Level,
                actor.Experience, actor.CurrentHp, actor.Stats.MaxHp, actor.CurrentMp, actor.Stats.MaxMp,
                progression.Currency.GetActorAccount(playerActorId).Balance, progression.Currency.Currency.UnitName);
        }
        MaterialDescriptions = LocalMaterialDescriptionProjection.Project(state, this);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public LocalMaterialDescriptions? MaterialDescriptions { get; }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId PlayerActorId { get; }

    /// <summary>
    /// The player's bounded regional terrain and water observation from this view's own root, in worlds presented
    /// regionally; null elsewhere, or when the player stands on no supported cell floor (see <see cref="IsRegional"/>).
    /// Presentation geometry, not an authority fact: public-state digests compare the authority fields only.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CargoPresentation.LocalRegionObservation? Regional { get; }

    /// <summary>Whether this session presents regionally, so a missing observation is an unsupported situation.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRegional { get; }
    public WorldInstant Calendar { get; }
    public bool HasReadingHold { get; }
    public bool IsResolvingConsequences { get; }
    public bool IsInEncounter => Encounter is not null;
    public LocalEncounterView? Encounter { get; }
    public LocalEncounterLifecycleView? EncounterLifecycle { get; }
    public LocalEncounterActionView? EncounterActions { get; }
    /// <summary>
    /// The explicit retreat interpretation's offer and the observer's own route and record; null, and omitted from the
    /// public encoding, in every other world so earlier interpretations keep their exact public bytes.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Simulation.Retreat.LocalRetreatView? Retreat { get; }

    /// <summary>
    /// The outcome-completion interpretation's current reading or last completion record from the player's side; null,
    /// and omitted from the public encoding, in every other world so earlier interpretations keep their public bytes.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Simulation.EncounterOutcome.LocalEncounterOutcomeView? EncounterOutcome { get; }

    /// <summary>
    /// Hostile opportunities against the player that the player currently observes (same floor, clear sightline,
    /// within the three-cell interaction bound), or null when the world admits none; like Retreat it is omitted from
    /// the public encoding when null, so worlds without a threat keep their exact public view.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Simulation.RoadThreat.LocalRoadThreatView>? RoadThreats { get; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool SupportsCareDispositions { get; }
    public LocalCareView? Care { get; }
    public LocalRestorativeView? Restorative { get; }
    public LocalMaterialProposalView? MaterialProposals { get; }

    /// <summary>The player's open paid quotes and current-segment outcomes, as participant views; empty without paid exchange.</summary>
    public ReadOnlyCollection<PaidQuoteView> PaidQuotes { get; }

    /// <summary>The player's own stall context in a market world, including the advancing step; null without markets.</summary>
    public LocalMarketView? Markets { get; }
    public ReadOnlyCollection<LocalEncounterActorView> EncounterActors { get; }
    public ReadOnlyCollection<LocalActorView> Actors { get; }
    public LocalInventoryView PlayerInventory { get; }
    public ReadOnlyCollection<LocalGatherNodeView> GatheringNodes { get; }
    public ReadOnlyCollection<LocalGatherToolLotView> GatheringTools { get; }
    public uint? PlayerGatherEffort { get; }
    public LocalWorksiteView? Worksite { get; }
    public LocalLiquidView? Liquids { get; }
    public LocalCraftingView? Crafting { get; }
    public LocalDialogueView? Dialogue { get; }
    public LocalProgressionView? Progression { get; }
    public ReadOnlyCollection<LocalQuestView> Quests { get; }
    public ReadOnlyCollection<LocalConstructionView> Construction { get; }

    private ReadOnlyCollection<LocalQuestView> ProjectQuests(WorldSnapshot world, EntityId actorId, DialogueSession? dialogue)
    {
        List<LocalQuestView> views = [];
        if (world.Quests is not { } quests || world.Progression is not { } progression)
        {
            return views.AsReadOnly();
        }
        var actor = progression.GetActor(actorId);
        foreach (DeliveryQuestDefinition definition in quests.Definitions)
        {
            ActorQuestState? participation = quests.FindParticipation(actorId, definition.DefinitionId);
            bool addressed = dialogue?.SpeakerActorId == definition.GiverActorId;
            if (!addressed && participation is null)
            {
                continue;
            }
            ulong eligible = PlayerInventory.Lots.Where(lot => lot.Provenance == IngredientLotKind.Source
                && lot.AvailableForDelivery && lot.DefinitionId == definition.Item.DefinitionId)
                .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
            var reserve = progression.GetAccount(definition.Reward.ReserveAccountId);
            bool funded = quests.OutstandingReward(reserve.AccountId) + definition.Reward.CurrencyAmount <= reserve.Balance;
            bool levelEligible = actor.Level >= definition.MinimumLevel;
            views.Add(new LocalQuestView(definition.DefinitionId, definition.Version, definition.Title,
                definition.Description, definition.GiverActorId, definition.Item.DefinitionId, definition.Quantity,
                eligible, participation?.Status, participation?.Revision ?? 0, definition.MinimumLevel,
                definition.Reward.Experience, definition.Reward.CurrencyAmount, progression.Currency.Currency.UnitName,
                addressed && participation is null && levelEligible && funded,
                addressed && participation?.Status == ActorQuestStatus.Accepted && levelEligible && eligible >= definition.Quantity));
        }
        return views.AsReadOnly();
    }
}

using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.Retreat;
using CargoPresentation = OpenFantasy.Hosting.Simulation.CargoPresentation;
using EncounterOutcome = OpenFantasy.Hosting.Simulation.EncounterOutcome;
using Retreat = OpenFantasy.Hosting.Simulation.Retreat;
using RoadThreat = OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal sealed record WorldViewData(
    EntityId WorldId,
    EntityId BranchId,
    EntityId PlayerActorId,
    CargoPresentation.LocalRegionObservation? Regional,
    bool IsRegional,
    WorldInstant Calendar,
    bool HasReadingHold,
    bool IsResolvingConsequences,
    LocalEncounterView? Encounter,
    LocalEncounterLifecycleView? EncounterLifecycle,
    LocalEncounterActionView? EncounterActions,
    Retreat.LocalRetreatView? Retreat,
    EncounterOutcome.LocalEncounterOutcomeView? EncounterOutcome,
    IReadOnlyList<RoadThreat.LocalRoadThreatView>? RoadThreats,
    LocalCareView? Care,
    LocalRestorativeView? Restorative,
    LocalMaterialProposalView? MaterialProposals,
    ReadOnlyCollection<PaidQuoteView> PaidQuotes,
    LocalMarketView? Markets,
    ReadOnlyCollection<LocalEncounterActorView> EncounterActors,
    ReadOnlyCollection<LocalActorView> Actors,
    LocalInventoryView PlayerInventory,
    ReadOnlyCollection<LocalGatherNodeView> GatheringNodes,
    ReadOnlyCollection<LocalGatherToolLotView> GatheringTools,
    uint? PlayerGatherEffort,
    LocalWorksiteView? Worksite,
    LocalLiquidView? Liquids,
    LocalCraftingView? Crafting,
    LocalDialogueView? Dialogue,
    LocalProgressionView? Progression,
    ReadOnlyCollection<LocalQuestView> Quests,
    ReadOnlyCollection<LocalConstructionView> Construction,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool SupportsCareDispositions = false)
{
    internal LocalWorldView ToView() => new(WorldId, BranchId, PlayerActorId, Regional, IsRegional, Calendar, HasReadingHold, IsResolvingConsequences, Encounter, EncounterLifecycle, EncounterActions, Retreat, EncounterOutcome, RoadThreats, Care, Restorative, MaterialProposals, PaidQuotes, Markets, EncounterActors, Actors, PlayerInventory, GatheringNodes, GatheringTools, PlayerGatherEffort, Worksite, Liquids, Crafting, Dialogue, Progression, Quests, Construction, SupportsCareDispositions);

    internal static WorldViewData From(LocalWorldView value) => new(
        value.WorldId,
        value.BranchId,
        value.PlayerActorId,
        value.Regional,
        value.IsRegional,
        value.Calendar,
        value.HasReadingHold,
        value.IsResolvingConsequences,
        value.Encounter,
        value.EncounterLifecycle,
        value.EncounterActions,
        value.Retreat,
        value.EncounterOutcome,
        value.RoadThreats,
        value.Care,
        value.Restorative,
        value.MaterialProposals,
        value.PaidQuotes,
        value.Markets,
        value.EncounterActors,
        value.Actors,
        value.PlayerInventory,
        value.GatheringNodes,
        value.GatheringTools,
        value.PlayerGatherEffort,
        value.Worksite,
        value.Liquids,
        value.Crafting,
        value.Dialogue,
        value.Progression,
        value.Quests,
        value.Construction,
        value.SupportsCareDispositions);
}

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
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

internal sealed record CraftRecipeViewData(
    EntityId RecipeId,
    ContentHash Version,
    string Key,
    ReadOnlyCollection<LocalCraftTermView> Terms)
{
    internal LocalCraftRecipeView ToView() => new(RecipeId, Version, Key, Terms);

    internal static CraftRecipeViewData From(LocalCraftRecipeView value) => new(
        value.RecipeId,
        value.Version,
        value.Key,
        value.Terms);
}

internal sealed record CraftingViewData(
    ulong Revision,
    ReadOnlyCollection<LocalCraftRecipeView> Recipes)
{
    internal LocalCraftingView ToView() => new(Revision, Recipes);

    internal static CraftingViewData From(LocalCraftingView value) => new(
        value.Revision,
        value.Recipes);
}

internal sealed record EncounterActionViewData(
    EntityId EncounterId,
    ulong TimingRevision,
    ulong ActorRevision,
    ulong RemainingDelay,
    int? CurrentHp,
    int? CurrentMp,
    ulong WaitDelay,
    LocalEncounterActionBlockedReason WaitBlockedReason,
    ReadOnlyCollection<LocalEncounterAbilityOffer> AbilityOffers)
{
    internal LocalEncounterActionView ToView() => new(EncounterId, TimingRevision, ActorRevision, RemainingDelay, CurrentHp, CurrentMp, WaitDelay, WaitBlockedReason, AbilityOffers);

    internal static EncounterActionViewData From(LocalEncounterActionView value) => new(
        value.EncounterId,
        value.TimingRevision,
        value.ActorRevision,
        value.RemainingDelay,
        value.CurrentHp,
        value.CurrentMp,
        value.WaitDelay,
        value.WaitBlockedReason,
        value.AbilityOffers);
}

internal sealed record RetreatViewData(
    LocalRetreatOffer? Offer,
    LocalRetreatProgress? Progress,
    LocalRetreatRecord? Last,
    bool OtherWithdrawing,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    LocalWithdrawalView? Withdrawal = null)
{
    internal LocalRetreatView ToView() => new(Offer, Progress, Last, OtherWithdrawing, Withdrawal);

    internal static RetreatViewData From(LocalRetreatView value) => new(
        value.Offer,
        value.Progress,
        value.Last,
        value.OtherWithdrawing,
        value.Withdrawal);
}

internal sealed record InventoryViewData(
    EntityId CustodianId,
    ulong Revision,
    ReadOnlyCollection<LocalItemLotView> Lots)
{
    internal LocalInventoryView ToView() => new(CustodianId, Revision, Lots);

    internal static InventoryViewData From(LocalInventoryView value) => new(
        value.CustodianId,
        value.Revision,
        value.Lots);
}

internal sealed record CareViewData(
    ReadOnlyCollection<LocalCareAcknowledgmentView> Offers,
    LocalCareStatus Status,
    EntityId? PendingAcknowledgmentId,
    EntityId? HelperActorId,
    string? HelperName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LocalCareDecisionView? Decision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LocalCareClosureView? Closure = null)
{
    internal LocalCareView ToView() => new(Offers, Status, PendingAcknowledgmentId, HelperActorId, HelperName, Decision, Closure);

    internal static CareViewData From(LocalCareView value) => new(
        value.Offers,
        value.Status,
        value.PendingAcknowledgmentId,
        value.HelperActorId,
        value.HelperName,
        value.Decision,
        value.Closure);
}

internal sealed record RestorativeViewData(
    ReadOnlyCollection<LocalRestorativeUseView> SelfUseOffers,
    ReadOnlyCollection<LocalCarePermissionView> PermissionOffers)
{
    internal LocalRestorativeView ToView() => new(SelfUseOffers, PermissionOffers);

    internal static RestorativeViewData From(LocalRestorativeView value) => new(
        value.SelfUseOffers,
        value.PermissionOffers);
}

internal sealed record EncounterOutcomeViewData(
    LocalOutcomeStage Stage,
    EntityId EncounterId,
    EncounterOutcomeResult Result,
    int StandingSide,
    LocalOutcomeStanding Standing,
    ReadOnlyCollection<LocalOutcomeParticipantView> Participants,
    AcknowledgeEncounterOutcomeIntent? Acknowledgment,
    int RequiredReaders,
    int ReadersRemaining,
    bool ObserverHasRead,
    LocalOutcomeRestriction Restriction,
    ulong ObserverRecoveryDelay,
    ulong? CompletedAtSequence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    LocalOutcomeAcknowledgmentMode AcknowledgmentMode = LocalOutcomeAcknowledgmentMode.HumanReading,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool AllowsAssistedRecovery = false)
{
    internal LocalEncounterOutcomeView ToView() => new(Stage, EncounterId, Result, StandingSide, Standing, Participants, Acknowledgment, RequiredReaders, ReadersRemaining, ObserverHasRead, Restriction, ObserverRecoveryDelay, CompletedAtSequence, AcknowledgmentMode, AllowsAssistedRecovery);

    internal static EncounterOutcomeViewData From(LocalEncounterOutcomeView value) => new(
        value.Stage,
        value.EncounterId,
        value.Result,
        value.StandingSide,
        value.Standing,
        value.Participants,
        value.Acknowledgment,
        value.RequiredReaders,
        value.ReadersRemaining,
        value.ObserverHasRead,
        value.Restriction,
        value.ObserverRecoveryDelay,
        value.CompletedAtSequence,
        value.AcknowledgmentMode,
        value.AllowsAssistedRecovery);
}

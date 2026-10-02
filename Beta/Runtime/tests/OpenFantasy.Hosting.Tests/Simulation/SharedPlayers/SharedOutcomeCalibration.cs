using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;

/// <summary>A fresh declared permission catalog: the unrelated human bystander also has a care agreement.</summary>
internal static class SharedOutcomeCalibration
{
    internal static WorldSnapshot WithObserverCare(WorldSnapshot basis)
    {
        Assert.Equal(0UL, basis.Revision);
        var observer = basis.EncounterLifecycle!.Scenarios.Single(value =>
            value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        CareSnapshot care = basis.Care!;
        CareAgreement original = care.Agreements.First();
        var agreement = new CareAgreement(Retreat.RetreatJourneyWorld.NewId(), observer, original.HelperActorId,
            original.RemedyDefinitionId, original.RemedyVersion);
        var calibratedCare = new CareSnapshot(basis.WorldId, basis.BranchId, 0, care.Remedies,
            care.Agreements.Append(agreement), [], [], care.Ordinary);
        var originalScenario = basis.EncounterLifecycle.Scenarios.Single(value =>
            value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        var observerScenario = new EncounterScenarioDefinition(Retreat.RetreatJourneyWorld.NewId(),
            "observer-care-qualification", "Observer care qualification", observer, originalScenario.TargetActorId,
            originalScenario.EntryRangeSubunits, originalScenario.Combatants.Select(actor =>
                actor.ActorId == observer ? new EncounterCombatant(actor.ActorId, 1,
                    originalScenario.Combatants.Single(value => value.ActorId == originalScenario.InitiatingActorId).AbilityIds)
                : actor.ActorId == originalScenario.TargetActorId ? actor : new EncounterCombatant(actor.ActorId, 0, [])));
        var lifecycle = basis.EncounterLifecycle;
        var calibratedLifecycle = new EncounterLifecycleSnapshot(basis.WorldId, basis.BranchId, lifecycle.Seed,
            0, 0, 0, 0, 0, EncounterLifecyclePhase.Exploration, lifecycle.Abilities,
            lifecycle.Scenarios.Append(observerScenario), lifecycle.Debts);
        // Three fixed 32-byte fields bind the complete source, care catalog and qualifying scenario.
        ContentHash admission = ContentHash.Compute(basis.ContentHash.ToByteArray()
            .Concat(calibratedCare.ContentHash.ToByteArray()).Concat(calibratedLifecycle.ContentHash.ToByteArray()).ToArray());
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: calibratedLifecycle,
            care: calibratedCare, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(basis.Progression!, 0, []), []),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }
}

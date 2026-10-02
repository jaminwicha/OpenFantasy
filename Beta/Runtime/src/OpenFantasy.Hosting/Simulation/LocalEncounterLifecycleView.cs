using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed record LocalEncounterEntryView(EntityId ScenarioId, string DisplayName, EntityId TargetActorId,
    ulong TargetRevision, EntityId EncounterId, bool CanEnter);

/// <summary>Visible entry choices and an opaque outcome token; private world history remains host-owned.</summary>
public sealed record LocalEncounterLifecycleView(EncounterLifecyclePhase Phase, ulong EncounterCount,
    ulong PlayerRecoveryDelay, bool PlayerCanAct, ContentHash? ReleaseActionHash,
    ReadOnlyCollection<LocalEncounterEntryView> Entries)
{
    internal static LocalEncounterLifecycleView? Project(WorldSnapshot world, EntityId playerId)
    {
        if (world.EncounterLifecycle is not { } lifecycle) return null;
        var entries = lifecycle.Scenarios.Where(scenario => scenario.InitiatingActorId == playerId)
            .Select(scenario => new LocalEncounterEntryView(scenario.DefinitionId, scenario.DisplayName,
                scenario.TargetActorId, world.Space.GetActor(scenario.TargetActorId).Revision,
                EncounterEpisodeIdentity.Derive(lifecycle, scenario.DefinitionId),
                lifecycle.Phase == EncounterLifecyclePhase.Exploration && !world.HasReadingHold
                && scenario.Combatants.Where(actor => actor.Side != 0).All(actor =>
                    WorldActorAvailability.CanCooperate(world, actor.ActorId)
                    && (actor.ActorId == playerId || world.Space.QueryInteraction(playerId, actor.ActorId,
                        scenario.EntryRangeSubunits) == SpatialInteraction.Reachable)))).ToArray();
        // The outcome-completion world has no initiator release; its reading is offered by the outcome view instead.
        bool mayRelease = lifecycle.Phase == EncounterLifecyclePhase.Outcome && world.EncounterOutcome is null
            && lifecycle.GetScenario(lifecycle.ScenarioId!.Value).InitiatingActorId == playerId
            && WorldActorAvailability.CanCooperate(world, playerId);
        return new(lifecycle.Phase, lifecycle.EncounterCount, lifecycle.GetDebt(playerId).RemainingDelay,
            WorldActorAvailability.CanAct(world, playerId), mayRelease ? lifecycle.Actions!.ContentHash : null,
            Array.AsReadOnly(entries));
    }
}

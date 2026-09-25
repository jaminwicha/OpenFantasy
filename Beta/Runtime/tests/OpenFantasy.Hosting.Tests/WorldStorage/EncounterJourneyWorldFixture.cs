using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Actual admitted four-resident content; helpers capture commands without granting effects or recovery.</summary>
internal sealed class EncounterJourneyWorldFixture
{
    internal const string SeedText = "encounter-lifecycle-world";
    internal const string PlayerName = "Traveler";
    private ulong _identity = 10_000;

    internal EncounterJourneyWorldFixture()
    {
        Genesis = EncounterJourneyAdmission.CreateEmbedded(SeedText, PlayerName);
        Player = Genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var lifecycle = Genesis.EncounterLifecycle!;
        var first = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        var second = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey);
        FirstScenario = first.DefinitionId;
        SecondScenario = second.DefinitionId;
        WolfFirst = first.TargetActorId;
        WolfSecond = second.TargetActorId;
        Wren = Genesis.Residents.Single(value => value.ActorId != Player && value.ActorId != WolfFirst
            && value.ActorId != WolfSecond).ActorId;
        Strike = lifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
        Bite = lifecycle.Abilities.Single(value => value.Key == EncounterFarmAdmission.WolfBiteKey).DefinitionId;
        Delivery = DeliveryFor(Player);
    }

    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Wren { get; }
    internal EntityId WolfFirst { get; }
    internal EntityId WolfSecond { get; }
    internal EntityId FirstScenario { get; }
    internal EntityId SecondScenario { get; }
    internal EntityId Strike { get; }
    internal EntityId Bite { get; }
    internal TrustedCommandDelivery Delivery { get; }

    internal static EntityId Id(ulong value) => new(0xee12400000000000, value);

    internal EntityId NextId()
    {
        EntityId identity = Id(_identity++);
        if (Genesis.IdentityIndex.Contains(identity)) throw new InvalidOperationException("Fixture identity aliases admitted content.");
        return identity;
    }

    internal TrustedCommandDelivery DeliveryFor(EntityId actor)
    {
        var binding = Genesis.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }

    internal WorldActionCommand Enter(WorldSnapshot state, EntityId scenarioId, EntityId? encounterId = null)
    {
        var lifecycle = state.EncounterLifecycle!;
        var scenario = lifecycle.GetScenario(scenarioId);
        return Command(state, scenario.InitiatingActorId, new EnterEncounterIntent(scenarioId, encounterId ?? EncounterEpisodeIdentity.Derive(lifecycle, scenarioId),
            lifecycle.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision));
    }

    internal WorldActionCommand Release(WorldSnapshot state)
    {
        var lifecycle = state.EncounterLifecycle!;
        var actions = lifecycle.Actions!;
        return Command(state, lifecycle.GetScenario(lifecycle.ScenarioId!.Value).InitiatingActorId,
            new ReleaseEncounterIntent(actions.Scope.EncounterId, actions.ContentHash));
    }

    internal WorldActionCommand Attack(WorldSnapshot state, EntityId actor, EntityId target,
        EntityId? ability = null, ulong? timingRevision = null) => Command(state, actor,
            new QueueEncounterAbilityIntent(state.Pacing!.Encounter!.EncounterId,
                timingRevision ?? state.Pacing.Encounter.Revision,
                ability ?? (actor == WolfFirst || actor == WolfSecond ? Bite : Strike), target));

    internal WorldActionCommand Wait(WorldSnapshot state, EntityId actor, ulong? timingRevision = null) => Command(state, actor,
        new QueueEncounterWaitIntent(state.Pacing!.Encounter!.EncounterId, timingRevision ?? state.Pacing.Encounter.Revision));

    internal WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind,
        IEnumerable<WorldActionCommand>? actions = null) => new(NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, kind, [], actions ?? [], state.ProfileVersion);

    internal WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        var binding = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextId(), binding.PrincipalId, binding.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }
}

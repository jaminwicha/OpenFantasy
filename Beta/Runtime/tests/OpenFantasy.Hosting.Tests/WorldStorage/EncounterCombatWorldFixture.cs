using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Production-admitted fresh content; helpers capture real controls and revisions without changing authority.</summary>
internal sealed class EncounterCombatWorldFixture
{
    internal const string SeedText = "encounter-combat-world";
    internal const string PlayerName = "Traveler";
    private ulong _identity = 10_000;

    internal EncounterCombatWorldFixture()
    {
        Genesis = EncounterFarmAdmission.CreateEmbedded(SeedText, PlayerName);
        Player = Genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Wolf = Genesis.EncounterActions!.Combatants.Single(value => value.Side == 2).ActorId;
        Wren = Genesis.EncounterActions.Combatants.Single(value => value.Side == 0).ActorId;
        Strike = Genesis.EncounterActions.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
        Bite = Genesis.EncounterActions.Abilities.Single(value => value.Key == EncounterFarmAdmission.WolfBiteKey).DefinitionId;
        Delivery = DeliveryFor(Player);
    }

    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Wolf { get; }
    internal EntityId Wren { get; }
    internal EntityId Strike { get; }
    internal EntityId Bite { get; }
    internal TrustedCommandDelivery Delivery { get; }

    internal static EntityId Id(ulong value) => new(0xee12300000000000, value);
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

    internal WorldActionCommand Attack(WorldSnapshot state, EntityId actor, EntityId target,
        EntityId? ability = null, ulong? timingRevision = null) => Command(state, actor,
            new QueueEncounterAbilityIntent(state.Pacing!.Encounter!.EncounterId,
                timingRevision ?? state.Pacing.Encounter.Revision, ability ?? (actor == Wolf ? Bite : Strike), target));

    internal WorldActionCommand Wait(WorldSnapshot state, EntityId actor, ulong? timingRevision = null) => Command(state, actor,
        new QueueEncounterWaitIntent(state.Pacing!.Encounter!.EncounterId, timingRevision ?? state.Pacing.Encounter.Revision));

    internal WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind,
        IEnumerable<WorldActionCommand>? actions = null) => new(NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, kind, [], actions ?? [], state.ProfileVersion);

    private WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        var binding = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextId(), binding.PrincipalId, binding.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }
}

using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Encounters;

internal static class EncounterActionFixture
{
    internal static EntityId Id(ulong id) => new(0, id);
    internal static EncounterTimingScope Scope => new(Id(1), Id(2), Id(3));
    internal static EntityId Seed => Id(4);
    internal static ContentHash Source => ContentHash.Compute("physical abilities fixture"u8);
    internal static EncounterAbilityDefinition Ability(ulong id = 100, uint power = 10, uint numerator = 1,
        uint denominator = 1, uint mana = 2, BodyFunction function = BodyFunction.Manipulation) =>
        new(Id(id), $"ability-{id}", "Physical strike", Source, power, 512, numerator, denominator, mana, 1, function);
    internal static EncounterActionSnapshot Begin() => EncounterActionSnapshot.Begin(Scope, Seed, [Ability()],
        [new(Id(10), 1, [Id(100)]), new(Id(20), 2, [Id(100)]), new(Id(30), 0, [])]);
    internal static WorldActionCommand Command(ulong actor = 10, ulong target = 20, ulong command = 200, ulong ability = 100,
        EncounterTimingScope? scope = null)
    {
        scope ??= Scope;
        return new WorldActionCommand(Id(command), Id(1_000 + actor), Id(2_000 + actor), Id(actor),
            scope.WorldId, scope.BranchId, 0, new QueueEncounterAbilityIntent(scope.EncounterId, 0, Id(ability), Id(target)));
    }

    internal static EncounterActionResolution Damaged(EncounterActionSnapshot state, out RandomCounterState next,
        ulong worldSequence = 2, ulong timingRevision = 2, IEnumerable<EntityId>? cancelled = null)
    {
        CounterRandomV1 random = state.CreateRandom();
        uint variation = checked((uint)(900 + random.NextUInt64(201)));
        next = random.CaptureState();
        return new EncounterActionResolution(worldSequence, timingRevision, Id(10), Id(200), Id(20), Id(100),
            EncounterActionResolutionStatus.Damaged, 9, 20, 11, 2, 100, variation, cancelled ?? []);
    }
}

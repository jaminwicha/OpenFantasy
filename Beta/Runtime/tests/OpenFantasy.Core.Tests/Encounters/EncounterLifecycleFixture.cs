using System.Buffers.Binary;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Encounters;

internal static class EncounterLifecycleFixture
{
    internal static EntityId Id(ulong value) => EncounterActionFixture.Id(value);
    internal static EntityId World => Id(1);
    internal static EntityId Branch => Id(2);
    internal static EntityId Seed => Id(4);
    internal static EntityId[] Actors => [Id(10), Id(20), Id(30), Id(40)];
    internal static EncounterAbilityDefinition[] Abilities => [EncounterActionFixture.Ability(), EncounterActionFixture.Ability(101)];

    internal static EncounterScenarioDefinition Scenario(ulong definition = 500, ulong opponent = 20) => new(Id(definition),
        $"scenario-{definition}", "Authored encounter opportunity", Id(10), Id(opponent), 768,
        Actors.Select(actor => actor == Id(10) ? new EncounterCombatant(actor, 1, [Id(100)])
            : actor == Id(opponent) ? new EncounterCombatant(actor, 2, [Id(101)]) : new EncounterCombatant(actor, 0, [])));

    internal static EncounterLifecycleSnapshot Fresh() => EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed,
        Abilities, [Scenario(), Scenario(501, 30)], Actors);

    internal static EncounterTimingSnapshot Timing(EncounterLifecycleSnapshot lifecycle, ulong revision = 0) =>
        new(lifecycle.Actions!.Scope, revision,
            checked((lifecycle.TacticalTicks - lifecycle.EntryTacticalTicks) * EncounterTimingSnapshot.UnitsPerTick),
            lifecycle.Debts.Select(debt => new EncounterActorTiming(debt.ActorId, 100, debt.RemainingDelay,
                debt.ActorId == Id(10) ? EncounterActorControl.RequiredHuman : EncounterActorControl.Automatic)), []);

    internal static EncounterLifecycleSnapshot Active() => Fresh().Begin(Id(1_000), Id(500)).AdvanceStaging().AdvanceStaging();

    internal static (EncounterLifecycleSnapshot Lifecycle, EncounterTimingSnapshot Timing) Queued()
    {
        var state = Active();
        var timing = Timing(state);
        var command = EncounterActionFixture.Command(scope: timing.Scope);
        timing = timing.QueueDecision(timing.Revision, new(command.ActorId, command.CommandId, EncounterDecisionKind.Action, command.ContentHash));
        var actions = state.Actions!.WithPending([command]);
        return (state.ObserveActive(timing, actions, tacticalTick: false, isOutcome: false), timing);
    }

    internal static EncounterLifecycleSnapshot Complete(EncounterLifecycleSnapshot queued, EncounterTimingSnapshot timing)
    {
        var confirmed = timing.TryConfirmNext(timing.Revision).State;
        var actions = queued.Actions!;
        var random = actions.CreateRandom();
        uint variation = 900U + (uint)random.NextUInt64(201);
        var result = new EncounterActionResolution(queued.Revision + 1, confirmed.Revision, Id(10), Id(200), Id(20), Id(100),
            EncounterActionResolutionStatus.Damaged, 20, 20, 0, 2, 100, variation, []);
        actions = actions.WithResolution([], random.CaptureState(), result);
        // The component accepts the caller's outcome finding; an actual world must prove HP and eligibility separately.
        return queued.ObserveActive(confirmed, actions, tacticalTick: false, isOutcome: true);
    }

    internal static EncounterLifecycleSnapshot Outcome()
    {
        var (state, timing) = Queued();
        return Complete(state, timing);
    }

    internal static EncounterLifecycleSnapshot Copy(EncounterLifecycleSnapshot state, ulong? revision = null,
        ulong? exploration = null, ulong? tactical = null, ulong? entry = null, ulong? count = null,
        EncounterLifecyclePhase? phase = null, IEnumerable<EncounterAbilityDefinition>? abilities = null,
        IEnumerable<EncounterScenarioDefinition>? scenarios = null, IEnumerable<EncounterReadinessDebt>? debts = null,
        EncounterActionSnapshot? actions = null) => new(state.WorldId, state.BranchId, state.Seed,
        revision ?? state.Revision, exploration ?? state.ExplorationSteps, tactical ?? state.TacticalTicks,
        entry ?? state.EntryTacticalTicks, count ?? state.EncounterCount, phase ?? state.Phase,
        abilities ?? state.Abilities, scenarios ?? state.Scenarios, debts ?? state.Debts, state.ScenarioId, actions ?? state.Actions);

    internal static EncounterLifecycleSnapshot Restore(EncounterLifecycleSnapshot state) =>
        EncounterLifecycleCodecV1.Decode(EncounterLifecycleCodecV1.Encode(state), World, Branch, TestContext.Current.CancellationToken);

    internal static List<byte[]> Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        int offset = 12;
        List<byte[]> fields = [];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    internal static byte[] Frame(IReadOnlyList<byte[]> fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, 1); stream.Write(word);
        BinaryPrimitives.WriteUInt32BigEndian(word, checked((uint)fields.Count)); stream.Write(word);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(word, checked((uint)field.Length)); stream.Write(word); stream.Write(field);
        }
        return stream.ToArray();
    }

    internal static void Number(Span<byte> field, ulong value) => BinaryPrimitives.WriteUInt64BigEndian(field, value);
}

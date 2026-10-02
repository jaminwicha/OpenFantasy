using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// Complete canonical lifecycle archive. Preserves version-one bytes and explicitly dispatches the
/// version-two catalog envelope; accepted transition history belongs to the world journal.
/// </summary>
public static class EncounterLifecycleCodecV1
{
    public const int MaximumEncodedByteLength = 512 * 1_024;
    private const int MaximumScenarioByteLength = 48 * 1_024;
    private const int MaximumAbilityByteLength = 1_024;
    private const int MaximumCombatantByteLength = 640;
    private const int MaximumDebtByteLength = 128;

    public static byte[] Encode(EncounterLifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string tag = snapshot.CatalogVersion == 1 ? "encounter.lifecycle.v1" : "encounter.lifecycle.v2";
        byte[] encoded = Frame(tag, Number((ulong)snapshot.CatalogVersion), snapshot.WorldId.ToByteArray(), snapshot.BranchId.ToByteArray(),
            snapshot.Seed.ToByteArray(), Number(snapshot.Revision), Number(snapshot.ExplorationSteps), Number(snapshot.TacticalTicks),
            Number(snapshot.EntryTacticalTicks), Number(snapshot.EncounterCount), Number((ulong)snapshot.Phase),
            Frame("encounter.lifecycle-abilities.v1", snapshot.Abilities.Select(ability =>
                (ReadOnlyMemory<byte>)EncounterActionCodecV1.EncodeAbility(ability)).ToArray()),
            Frame("encounter.scenarios.v1", snapshot.Scenarios.Select(scenario => (ReadOnlyMemory<byte>)EncodeScenario(scenario)).ToArray()),
            Frame("encounter.readiness-debts.v1", snapshot.Debts.Select(debt => (ReadOnlyMemory<byte>)EncodeDebt(debt)).ToArray()),
            snapshot.ScenarioId?.ToByteArray() ?? [], snapshot.Actions?.Scope.EncounterId.ToByteArray() ?? [],
            snapshot.Actions is { } actions ? EncounterActionCodecV1.Encode(actions) : []);
        if (encoded.Length > MaximumEncodedByteLength)
            throw new ArgumentException("The complete lifecycle exceeds its retained byte bound.", nameof(snapshot));
        return encoded;
    }

    public static EncounterLifecycleSnapshot Decode(ReadOnlySpan<byte> bytes, EntityId expectedWorldId,
        EntityId expectedBranchId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 17);
            ReadOnlySpan<byte> tag = reader.Field("encounter.lifecycle.v1"u8.Length);
            int catalogVersion = tag.SequenceEqual("encounter.lifecycle.v1"u8) ? 1
                : tag.SequenceEqual("encounter.lifecycle.v2"u8) ? 2
                : throw new FormatException("The lifecycle archive has an unsupported catalog tag.");
            if (reader.Number() != (ulong)catalogVersion)
                throw new FormatException("The lifecycle catalog tag and version disagree.");
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            if (world != expectedWorldId || branch != expectedBranchId)
                throw new FormatException("The lifecycle belongs to a different complete world or branch.");
            EntityId seed = reader.Identity();
            ulong revision = reader.Number();
            ulong exploration = reader.Number();
            ulong tactical = reader.Number();
            ulong entry = reader.Number();
            ulong count = reader.Number();
            var phase = (EncounterLifecyclePhase)checked((int)reader.Number());
            var abilities = Entries(reader.Field(MaximumEncodedByteLength), "encounter.lifecycle-abilities.v1"u8,
                EncounterLifecycleSnapshot.MaximumAbilities, MaximumAbilityByteLength,
                value => EncounterActionCodecV1.DecodeAbility(value), cancellationToken);
            var scenarios = Entries(reader.Field(MaximumEncodedByteLength), "encounter.scenarios.v1"u8,
                EncounterLifecycleSnapshot.ScenarioLimit(catalogVersion), MaximumScenarioByteLength,
                value => DecodeScenario(value, cancellationToken), cancellationToken);
            var debts = Entries(reader.Field(MaximumEncodedByteLength), "encounter.readiness-debts.v1"u8,
                EncounterLifecycleSnapshot.MaximumActors, MaximumDebtByteLength, value => DecodeDebt(value), cancellationToken);
            EntityId? scenarioId = OptionalIdentity(reader.Field(EntityId.ByteLength));
            EntityId? encounterId = OptionalIdentity(reader.Field(EntityId.ByteLength));
            ReadOnlySpan<byte> actionBytes = reader.Field(EncounterActionCodecV1.MaximumEncodedByteLength);
            if ((encounterId is null) != actionBytes.IsEmpty)
                throw new FormatException("A retained action archive requires its exact encounter decode context.");
            var actions = encounterId is { } encounter
                ? EncounterActionCodecV1.Decode(actionBytes, new(world, branch, encounter), cancellationToken) : null;
            reader.Complete();
            var result = new EncounterLifecycleSnapshot(world, branch, seed, revision, exploration, tactical, entry,
                count, phase, abilities, scenarios, debts, scenarioId, actions, catalogVersion);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
                throw new FormatException("The lifecycle archive is not canonical.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The lifecycle archive violates its retained invariants.", error);
        }
    }

    internal static byte[] EncodeScenario(EncounterScenarioDefinition scenario) => Frame("encounter.scenario.v1",
        scenario.DefinitionId.ToByteArray(), CanonicalEncodingV1.Text(scenario.Key, nameof(scenario.Key)),
        CanonicalEncodingV1.Text(scenario.DisplayName, nameof(scenario.DisplayName)), scenario.InitiatingActorId.ToByteArray(),
        scenario.TargetActorId.ToByteArray(), Number(checked((ulong)scenario.EntryRangeSubunits)),
        Frame("encounter.scenario-combatants.v1", scenario.Combatants.Select(actor =>
            (ReadOnlyMemory<byte>)EncounterActionCodecV1.EncodeCombatant(actor)).ToArray()));

    private static EncounterScenarioDefinition DecodeScenario(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumScenarioByteLength, 8);
        reader.Tag("encounter.scenario.v1"u8);
        EntityId definition = reader.Identity();
        string key = reader.Text(EncounterScenarioDefinition.MaximumKeyLength);
        string name = reader.Text(EncounterScenarioDefinition.MaximumDisplayNameBytes);
        EntityId initiator = reader.Identity();
        EntityId target = reader.Identity();
        long range = checked((long)reader.Number());
        var actors = Entries(reader.Field(MaximumScenarioByteLength), "encounter.scenario-combatants.v1"u8,
            EncounterScenarioDefinition.MaximumCombatants, MaximumCombatantByteLength,
            value => EncounterActionCodecV1.DecodeCombatant(value), cancellationToken);
        reader.Complete();
        return new(definition, key, name, initiator, target, range, actors);
    }

    private static byte[] EncodeDebt(EncounterReadinessDebt debt) =>
        Frame("encounter.readiness-debt.v1", debt.ActorId.ToByteArray(), Number(debt.RemainingDelay));

    private static EncounterReadinessDebt DecodeDebt(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumDebtByteLength, 3);
        reader.Tag("encounter.readiness-debt.v1"u8);
        var result = new EncounterReadinessDebt(reader.Identity(), reader.Number());
        reader.Complete();
        return result;
    }

    private static List<T> Entries<T>(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> tag, int maximumCount,
        int maximumEntryBytes, Func<byte[], T> decode, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, maximumCount + 1);
        reader.Tag(tag);
        List<T> result = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(decode(reader.Field(maximumEntryBytes).ToArray()));
        }
        reader.Complete();
        return result;
    }

    private static EntityId? OptionalIdentity(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? null : EntityId.FromBytes(bytes);
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}

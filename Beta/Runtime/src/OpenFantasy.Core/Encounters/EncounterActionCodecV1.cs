using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Canonical complete action-owner archive; exact world/journal admission still authenticates its history.</summary>
public static class EncounterActionCodecV1
{
    public const int MaximumEncodedByteLength = 128 * 1_024;
    private const int MaximumAbilityByteLength = 1_024;
    private const int MaximumCombatantByteLength = 640;
    private const int MaximumResolutionByteLength = 2_048;

    public static byte[] Encode(EncounterActionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] encoded = Frame("encounter.actions.v1", Number(1), snapshot.RulesVersion.ToByteArray(),
            snapshot.Scope.WorldId.ToByteArray(), snapshot.Scope.BranchId.ToByteArray(), snapshot.Scope.EncounterId.ToByteArray(),
            snapshot.Seed.ToByteArray(), Number(snapshot.Revision),
            Frame("encounter.abilities.v1", snapshot.Abilities.Select(ability => (ReadOnlyMemory<byte>)EncodeAbility(ability)).ToArray()),
            Frame("encounter.combatants.v1", snapshot.Combatants.Select(actor => (ReadOnlyMemory<byte>)EncodeCombatant(actor)).ToArray()),
            Frame("encounter.pending-actions.v1", snapshot.Pending.Select(command => (ReadOnlyMemory<byte>)WorldActionCodec.Encode(command)).ToArray()),
            EncodeRandom(snapshot.Random), snapshot.LastResolution is { } resolution ? EncodeResolution(resolution) : []);
        if (encoded.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The complete encounter action archive exceeds its retained byte bound.", nameof(snapshot));
        }
        return encoded;
    }

    public static EncounterActionSnapshot Decode(ReadOnlySpan<byte> bytes, EncounterTimingScope expectedScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedScope);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 13);
            reader.Tag("encounter.actions.v1"u8);
            reader.Version();
            if (Hash(reader.Exact(32)) != EncounterActionSnapshot.RulesPin)
            {
                throw new FormatException("The physical action rules pin is unsupported.");
            }
            var scope = new EncounterTimingScope(reader.Identity(), reader.Identity(), reader.Identity());
            if (scope != expectedScope)
            {
                throw new FormatException("The action archive belongs to another world, branch or encounter.");
            }
            EntityId seed = reader.Identity();
            ulong revision = reader.Number();
            var abilities = ReadEntries(reader.Field(MaximumEncodedByteLength), "encounter.abilities.v1"u8,
                EncounterActionSnapshot.MaximumAbilities, MaximumAbilityByteLength, value => DecodeAbility(value), cancellationToken);
            var combatants = ReadEntries(reader.Field(MaximumEncodedByteLength), "encounter.combatants.v1"u8,
                EncounterActionSnapshot.MaximumCombatants, MaximumCombatantByteLength, value => DecodeCombatant(value), cancellationToken);
            var pending = ReadEntries(reader.Field(MaximumEncodedByteLength), "encounter.pending-actions.v1"u8,
                EncounterActionSnapshot.MaximumPending, EncounterActionSnapshot.MaximumPendingCommandByteLength,
                value => WorldActionCodec.Decode(value), cancellationToken);
            RandomCounterState random = DecodeRandom(reader.Field(256));
            ReadOnlySpan<byte> last = reader.Field(MaximumResolutionByteLength);
            EncounterActionResolution? resolution = last.IsEmpty ? null : DecodeResolution(last);
            reader.Complete();
            var result = new EncounterActionSnapshot(scope, seed, revision, abilities, combatants, pending, random, resolution);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The action archive is not canonical.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The physical action archive violates its retained invariants.", error);
        }
    }

    internal static byte[] EncodeAbility(EncounterAbilityDefinition ability) => Frame("encounter.ability.v1",
        ability.DefinitionId.ToByteArray(), CanonicalEncodingV1.Text(ability.Key, nameof(ability.Key)),
        CanonicalEncodingV1.Text(ability.DisplayName, nameof(ability.DisplayName)), ability.SourceVersion.ToByteArray(),
        Number(ability.PowerTenths), Number(checked((ulong)ability.RangeSubunits)), Number(ability.ReadinessCostNumerator),
        Number(ability.ReadinessCostDenominator), Number(ability.ManaCost), Number(ability.MinimumLevel), Number((ulong)ability.RequiredFunction));

    internal static EncounterAbilityDefinition DecodeAbility(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumAbilityByteLength, 12);
        reader.Tag("encounter.ability.v1"u8);
        var result = new EncounterAbilityDefinition(reader.Identity(), reader.Text(EncounterAbilityDefinition.MaximumKeyLength),
            reader.Text(EncounterAbilityDefinition.MaximumDisplayNameBytes), Hash(reader.Exact(32)),
            checked((uint)reader.Number()), checked((long)reader.Number()), checked((uint)reader.Number()),
            checked((uint)reader.Number()), checked((uint)reader.Number()), checked((uint)reader.Number()),
            (BodyFunction)checked((int)reader.Number()));
        reader.Complete();
        return result;
    }

    internal static byte[] EncodeCombatant(EncounterCombatant actor) => Frame("encounter.combatant.v1",
        actor.ActorId.ToByteArray(), Number(actor.Side), Identities(actor.AbilityIds));

    internal static EncounterCombatant DecodeCombatant(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumCombatantByteLength, 4);
        reader.Tag("encounter.combatant.v1"u8);
        EntityId actor = reader.Identity();
        ushort side = checked((ushort)reader.Number());
        EntityId[] grants = ReadIdentities(reader.Field(EncounterCombatant.MaximumAbilities * EntityId.ByteLength));
        reader.Complete();
        return new EncounterCombatant(actor, side, grants);
    }

    private static byte[] EncodeRandom(RandomCounterState random) => Frame("encounter.action-random.v1",
        Number(random.AlgorithmVersion), new ContentHash(random.StreamFingerprint).ToByteArray(),
        Number(random.NextCounter), Number(random.Exhausted ? 1UL : 0UL));

    private static RandomCounterState DecodeRandom(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 256, 5);
        reader.Tag("encounter.action-random.v1"u8);
        uint algorithm = checked((uint)reader.Number());
        string fingerprint = Hash(reader.Exact(32)).Value;
        ulong counter = reader.Number();
        ulong exhausted = reader.Number();
        if (exhausted > 1) { throw new FormatException("The random exhaustion flag must be canonical."); }
        reader.Complete();
        return new RandomCounterState(algorithm, fingerprint, counter, exhausted == 1);
    }

    private static byte[] EncodeResolution(EncounterActionResolution resolution) => Frame("encounter.action-resolution.v1",
        Number(resolution.WorldSequence), Number(resolution.TimingRevision), resolution.ActorId.ToByteArray(),
        resolution.DecisionCommandId.ToByteArray(), resolution.TargetActorId?.ToByteArray() ?? [],
        resolution.AbilityDefinitionId?.ToByteArray() ?? [], Number((ulong)resolution.Status), Number(resolution.Damage),
        Number(resolution.TargetHpBefore), Number(resolution.TargetHpAfter), Number(resolution.ManaSpent), Number(resolution.Delay),
        Number(resolution.VariationPermille), Identities(resolution.CancelledDecisionIds));

    private static EncounterActionResolution DecodeResolution(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumResolutionByteLength, 15);
        reader.Tag("encounter.action-resolution.v1"u8);
        var result = new EncounterActionResolution(reader.Number(), reader.Number(), reader.Identity(), reader.Identity(),
            OptionalIdentity(reader.Field(16)), OptionalIdentity(reader.Field(16)),
            (EncounterActionResolutionStatus)checked((int)reader.Number()), checked((uint)reader.Number()),
            checked((uint)reader.Number()), checked((uint)reader.Number()), checked((uint)reader.Number()), reader.Number(),
            checked((uint)reader.Number()), ReadIdentities(reader.Field(EncounterActionResolution.MaximumCancelledDecisions * EntityId.ByteLength)));
        reader.Complete();
        return result;
    }

    private static List<T> ReadEntries<T>(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> tag, int maximumCount,
        int maximumEntryBytes, Func<byte[], T> decode, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, maximumCount + 1);
        reader.Tag(tag);
        List<T> entries = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(decode(reader.Field(maximumEntryBytes).ToArray()));
        }
        reader.Complete();
        return entries;
    }

    private static byte[] Identities(IReadOnlyList<EntityId> identities)
    {
        byte[] bytes = new byte[checked(identities.Count * EntityId.ByteLength)];
        for (int index = 0; index < identities.Count; index++)
        {
            identities[index].WriteBytes(bytes.AsSpan(index * EntityId.ByteLength));
        }
        return bytes;
    }

    private static EntityId[] ReadIdentities(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % EntityId.ByteLength != 0) { throw new FormatException("The identity row is incomplete."); }
        EntityId[] result = new EntityId[bytes.Length / EntityId.ByteLength];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = EntityId.FromBytes(bytes.Slice(index * EntityId.ByteLength, EntityId.ByteLength));
        }
        return result;
    }

    private static EntityId? OptionalIdentity(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? null : EntityId.FromBytes(bytes);
    private static ContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(bytes).ToLowerInvariant());
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}

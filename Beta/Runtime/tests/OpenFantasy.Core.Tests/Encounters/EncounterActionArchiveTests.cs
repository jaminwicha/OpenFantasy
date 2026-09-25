using System.Buffers.Binary;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterActionFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterActionArchiveTests
{
    [Fact]
    public void Complete_maximum_catalog_grants_pending_and_resolution_fit_the_reserved_bound()
    {
        EncounterAbilityDefinition[] abilities = Enumerable.Range(1_000, 32).Select(id =>
            new EncounterAbilityDefinition(Id((ulong)id), "a" + new string('x', 59) + id, new string('n', 128), Source,
                1_000, 768, 1_000_000, 1_000_000, 1_000_000, 256, BodyFunction.NaturalWeapon)).ToArray();
        EncounterCombatant[] actors = Enumerable.Range(100, 64).Select(id =>
            new EncounterCombatant(Id((ulong)id), (ushort)(id % 2 + 1), abilities.Select(ability => ability.DefinitionId).Reverse())).ToArray();
        var initial = EncounterActionSnapshot.Begin(Scope, Seed, abilities.Reverse(), actors.Reverse());
        var pending = actors.Select((actor, index) => Command(actor.ActorId.Low, index % 2 == 0 ? 101UL : 100UL,
            (ulong)(10_000 + index), ability: 1_000)).ToArray();
        var resolution = new EncounterActionResolution(ulong.MaxValue, ulong.MaxValue, Id(100), Id(99_999), Id(101), Id(1_000),
            EncounterActionResolutionStatus.Damaged, 1, uint.MaxValue, uint.MaxValue - 1, 1_000_000, ulong.MaxValue, 1_100,
            Enumerable.Range(50_000, 64).Select(id => Id((ulong)id)).Reverse());
        var maximum = new EncounterActionSnapshot(Scope, Seed, ulong.MaxValue, initial.Abilities, initial.Combatants,
            pending.Reverse(), initial.Random with { NextCounter = ulong.MaxValue, Exhausted = true }, resolution);
        byte[] encoded = EncounterActionCodecV1.Encode(maximum);
        Assert.True(encoded.Length < EncounterActionCodecV1.MaximumEncodedByteLength);
        var decoded = EncounterActionCodecV1.Decode(encoded, Scope, TestContext.Current.CancellationToken);
        Assert.Equal(32, decoded.Abilities.Count);
        Assert.Equal(64, decoded.Combatants.Count);
        Assert.Equal(64, decoded.Pending.Count);
        Assert.Equal(64, decoded.LastResolution!.CancelledDecisionIds.Count);
        Assert.Equal(BodyFunction.NaturalWeapon, decoded.Abilities[0].RequiredFunction);
        Assert.Equal(encoded, EncounterActionCodecV1.Encode(decoded));
    }

    [Fact]
    public void Canonical_order_is_independent_of_supplied_catalog_and_grant_order()
    {
        var abilities = new[] { Ability(100), Ability(101) };
        var left = EncounterActionSnapshot.Begin(Scope, Seed, abilities,
            [new(Id(10), 1, [Id(100), Id(101)]), new(Id(20), 2, [Id(101), Id(100)])]);
        var right = EncounterActionSnapshot.Begin(Scope, Seed, abilities.Reverse(),
            [new(Id(20), 2, [Id(100), Id(101)]), new(Id(10), 1, [Id(101), Id(100)])]);
        Assert.Equal(EncounterActionCodecV1.Encode(left), EncounterActionCodecV1.Encode(right));
        Assert.Equal(left.Abilities[0].Version, right.Abilities[0].Version);
    }

    [Theory]
    [InlineData("rules")]
    [InlineData("seed")]
    [InlineData("ability-range")]
    [InlineData("ability-function")]
    [InlineData("ability-cost")]
    [InlineData("bystander-grant")]
    [InlineData("unknown-grant")]
    [InlineData("random-key")]
    [InlineData("random-algorithm")]
    [InlineData("random-exhausted")]
    [InlineData("unknown-pending-target")]
    [InlineData("hp-delta")]
    [InlineData("status")]
    [InlineData("zero-delay")]
    public void Malformed_full_archives_reject_inconsistent_fields(string corruption)
    {
        var pending = Begin().WithPending([Command()]);
        var resolution = Damaged(pending, out var random);
        var completed = pending.WithResolution([], random, resolution);
        // Keep another actor's valid pending payload in the completed archive for command tampering.
        completed = completed.WithPending([Command(actor: 20, target: 10, command: 201)]);
        var fields = Read(EncounterActionCodecV1.Encode(completed));
        var abilities = Read(fields[8]);
        var ability = Read(abilities[1]);
        var combatants = Read(fields[9]);
        var firstActor = Read(combatants[1]);
        var requests = Read(fields[10]);
        var request = Read(requests[1]);
        var intent = Read(request[9]);
        var randomFields = Read(fields[11]);
        var outcome = Read(fields[12]);
        switch (corruption)
        {
            case "rules": fields[2][0] ^= 1; break;
            case "seed": fields[6][0] ^= 1; break;
            case "ability-range": Number(ability[6], 769); break;
            case "ability-function": Number(ability[11], 999); break;
            case "ability-cost": Number(ability[8], 0); break;
            case "bystander-grant": Number(firstActor[2], 0); break;
            case "unknown-grant": Id(999).WriteBytes(firstActor[3]); break;
            case "random-key": randomFields[2][0] ^= 1; break;
            case "random-algorithm": Number(randomFields[1], 2); break;
            case "random-exhausted": Number(randomFields[4], 1); break;
            case "unknown-pending-target": Id(999).WriteBytes(intent[^1]); break;
            case "hp-delta": Number(outcome[10], 10); break;
            case "status": Number(outcome[7], 99); break;
            case "zero-delay": Number(outcome[12], 0); break;
            default: throw new InvalidOperationException();
        }
        abilities[1] = Frame(ability); fields[8] = Frame(abilities);
        combatants[1] = Frame(firstActor); fields[9] = Frame(combatants);
        request[9] = Frame(intent); requests[1] = Frame(request); fields[10] = Frame(requests);
        fields[11] = Frame(randomFields); fields[12] = Frame(outcome);
        Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(Frame(fields), Scope, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Noncanonical_order_duplicate_rows_scope_truncation_and_cancellation_refuse()
    {
        var state = Begin().WithPending([Command(), Command(actor: 20, target: 10, command: 201)]);
        byte[] bytes = EncounterActionCodecV1.Encode(state);
        var fields = Read(bytes);
        var actors = Read(fields[9]);
        (actors[1], actors[2]) = (actors[2], actors[1]);
        fields[9] = Frame(actors);
        Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(Frame(fields), Scope, TestContext.Current.CancellationToken));
        actors[2] = actors[1]; fields[9] = Frame(actors);
        Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(Frame(fields), Scope, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(bytes, new(Id(1), Id(2), Id(99)), TestContext.Current.CancellationToken));
        foreach (int length in new[] { 0, 11, 12, 50, bytes.Length / 2, bytes.Length - 1 })
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(truncated, Scope, TestContext.Current.CancellationToken));
        }
        Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(bytes.Concat(new byte[] { 0 }).ToArray(), Scope, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => EncounterActionCodecV1.Decode(new byte[EncounterActionCodecV1.MaximumEncodedByteLength + 1], Scope, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => EncounterActionCodecV1.Decode(bytes, Scope, cancellation.Token));
    }

    private static List<byte[]> Read(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        int offset = 12;
        List<byte[]> result = [];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            result.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return result;
    }

    private static byte[] Frame(IReadOnlyList<byte[]> fields)
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

    private static void Number(Span<byte> target, ulong value) => BinaryPrimitives.WriteUInt64BigEndian(target, value);
}

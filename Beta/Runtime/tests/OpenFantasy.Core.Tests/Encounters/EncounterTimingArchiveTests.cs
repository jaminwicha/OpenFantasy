using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterTimingArchiveTests
{
    private static EntityId Id(ulong value) => new(0, value);
    private static EncounterTimingScope Scope => new(Id(1), Id(2), Id(3));
    private static ContentHash ActionPin => ContentHash.Compute("retained decision payload"u8);
    private static EncounterActorTiming Actor(ulong id, ulong debt = 0) =>
        new(Id(id), 137, debt, EncounterActorControl.RequiredHuman);
    private static EncounterDecision Decision(ulong actor, ulong id) =>
        new(Id(actor), Id(id), EncounterDecisionKind.Action, ActionPin);
    private static EncounterTimingSnapshot Sample() => new(Scope, 7, 35,
        [Actor(20), Actor(10)], [Decision(20, 120), Decision(10, 110)]);

    [Fact]
    public void Every_queue_confirmation_and_tick_resumes_to_the_same_complete_bytes()
    {
        var direct = new EncounterTimingSnapshot(Scope, 0, 0,
            [new(Id(10), 137, 0, EncounterActorControl.RequiredHuman),
                new(Id(20), 71, 7, EncounterActorControl.Automatic)], []);
        var restored = Restore(direct);
        ulong nextDecisionId = 1_000;
        for (int step = 0; step < 90; step++)
        {
            foreach (var actor in direct.DueActors)
            {
                var decision = Decision(actor.ActorId.Low, nextDecisionId++);
                direct = direct.QueueDecision(direct.Revision, decision);
                restored = Restore(restored.QueueDecision(restored.Revision, decision));
                Assert.Equal(EncounterTimingCodecV1.Encode(direct), EncounterTimingCodecV1.Encode(restored));
            }
            while (direct.DueActors.Count != 0)
            {
                var one = direct.TryConfirmNext(direct.Revision);
                var two = restored.TryConfirmNext(restored.Revision);
                Assert.Equal(one.Status, two.Status);
                Assert.Equal(one.ConfirmedDecision, two.ConfirmedDecision);
                direct = one.State;
                restored = Restore(two.State);
                Assert.Equal(EncounterTimingCodecV1.Encode(direct), EncounterTimingCodecV1.Encode(restored));
            }
            direct = direct.TryAdvance(direct.Revision).State;
            restored = Restore(restored.TryAdvance(restored.Revision).State);
            Assert.Equal(EncounterTimingCodecV1.Encode(direct), EncounterTimingCodecV1.Encode(restored));
        }
        Assert.Equal(450UL, restored.ElapsedTacticalUnits);
        Assert.Equal(direct.ContentHash, restored.ContentHash);
    }

    [Fact]
    public void Ordering_is_canonical_and_the_maximum_complete_actor_and_decision_sets_fit()
    {
        var sample = Sample();
        var permuted = new EncounterTimingSnapshot(Scope, sample.Revision, sample.ElapsedTacticalUnits,
            sample.Actors.Reverse(), sample.Decisions.Reverse());
        Assert.Equal(EncounterTimingCodecV1.Encode(sample), EncounterTimingCodecV1.Encode(permuted));
        var maximum = new EncounterTimingSnapshot(Scope, ulong.MaxValue, ulong.MaxValue,
            Enumerable.Range(100, 128).Select(id => Actor((ulong)id)),
            Enumerable.Range(100, 128).Select(id => Decision((ulong)id, (ulong)(id + 1_000))));
        byte[] bytes = EncounterTimingCodecV1.Encode(maximum);
        Assert.True(bytes.Length < EncounterTimingCodecV1.MaximumEncodedByteLength);
        var decoded = EncounterTimingCodecV1.Decode(bytes, Scope, TestContext.Current.CancellationToken);
        Assert.Equal(128, decoded.Actors.Count);
        Assert.Equal(128, decoded.Decisions.Count);
        Assert.Equal(ulong.MaxValue, decoded.ElapsedTacticalUnits);
        Assert.Equal(bytes, EncounterTimingCodecV1.Encode(decoded));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Expected_world_branch_and_encounter_scope_cannot_be_substituted(int component)
    {
        var expected = new EncounterTimingScope(component == 0 ? Id(4) : Id(1),
            component == 1 ? Id(4) : Id(2), component == 2 ? Id(4) : Id(3));
        Assert.Throws<FormatException>(() => EncounterTimingCodecV1.Decode(EncounterTimingCodecV1.Encode(Sample()),
            expected, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("elapsed")]
    [InlineData("elapsed-revision")]
    [InlineData("speed-zero")]
    [InlineData("speed-wide")]
    [InlineData("control")]
    [InlineData("kind")]
    [InlineData("duplicate-actor")]
    [InlineData("duplicate-decision")]
    [InlineData("decision-actor-alias")]
    [InlineData("decision-scope-alias")]
    [InlineData("decision-not-due")]
    [InlineData("actor-order")]
    [InlineData("decision-order")]
    [InlineData("partial-actor")]
    [InlineData("partial-decision")]
    public void Malformed_or_noncanonical_retained_fields_are_rejected(string corruption)
    {
        var fields = Fields(EncounterTimingCodecV1.Encode(Sample()));
        var actors = Fields(fields[7]);
        var decisions = Fields(fields[8]);
        switch (corruption)
        {
            case "version": WriteNumber(fields[1], 2); break;
            case "elapsed": WriteNumber(fields[6], 36); break;
            case "elapsed-revision": WriteNumber(fields[6], 40); break;
            case "speed-zero": WriteNumber(actors[1].AsSpan(16), 0); break;
            case "speed-wide": WriteNumber(actors[1].AsSpan(16), (ulong)uint.MaxValue + 1); break;
            case "control": WriteNumber(actors[1].AsSpan(32), 3); break;
            case "kind": WriteNumber(decisions[1].AsSpan(32), 3); break;
            case "duplicate-actor": actors[1].AsSpan(0, 16).CopyTo(actors[1].AsSpan(40, 16)); break;
            case "duplicate-decision": decisions[1].AsSpan(16, 16).CopyTo(decisions[1].AsSpan(88, 16)); break;
            case "decision-actor-alias": Id(20).WriteBytes(decisions[1].AsSpan(16)); break;
            case "decision-scope-alias": Id(3).WriteBytes(decisions[1].AsSpan(16)); break;
            case "decision-not-due": WriteNumber(actors[1].AsSpan(24), 1); break;
            case "actor-order": actors[1] = actors[1][40..].Concat(actors[1][..40]).ToArray(); break;
            case "decision-order": decisions[1] = decisions[1][72..].Concat(decisions[1][..72]).ToArray(); break;
            case "partial-actor": actors[1] = actors[1][..^1]; break;
            case "partial-decision": decisions[1] = decisions[1][..^1]; break;
            default: throw new InvalidOperationException();
        }
        fields[7] = Frame(actors);
        fields[8] = Frame(decisions);
        byte[] malformed = Frame(fields);
        Assert.Throws<FormatException>(() => EncounterTimingCodecV1.Decode(malformed, Scope,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Truncation_trailing_data_count_limits_and_cancellation_fail_closed()
    {
        byte[] bytes = EncounterTimingCodecV1.Encode(Sample());
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<FormatException>(() => EncounterTimingCodecV1.Decode(truncated, Scope,
                TestContext.Current.CancellationToken));
        }
        Assert.Throws<FormatException>(() => EncounterTimingCodecV1.Decode(bytes.Concat(new byte[] { 0 }).ToArray(),
            Scope, TestContext.Current.CancellationToken));
        byte[] excessive = new byte[EncounterTimingCodecV1.MaximumEncodedByteLength + 1];
        Assert.Throws<FormatException>(() => EncounterTimingCodecV1.Decode(excessive, Scope,
            TestContext.Current.CancellationToken));
        var fields = Fields(bytes);
        var actors = Fields(fields[7]);
        actors[1] = new byte[129 * 40];
        fields[7] = Frame(actors);
        Assert.Throws<FormatException>(() => EncounterTimingCodecV1.Decode(Frame(fields), Scope,
            TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => EncounterTimingCodecV1.Decode(bytes, Scope, cancellation.Token));
    }

    private static EncounterTimingSnapshot Restore(EncounterTimingSnapshot snapshot) =>
        EncounterTimingCodecV1.Decode(EncounterTimingCodecV1.Encode(snapshot), Scope, TestContext.Current.CancellationToken);

    private static List<byte[]> Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        List<byte[]> fields = [];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Frame(IReadOnlyList<byte[]> fields)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("OFC\0"));
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, 1);
        stream.Write(word);
        BinaryPrimitives.WriteUInt32BigEndian(word, checked((uint)fields.Count));
        stream.Write(word);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(word, checked((uint)field.Length));
            stream.Write(word);
            stream.Write(field);
        }
        return stream.ToArray();
    }

    private static void WriteNumber(Span<byte> target, ulong value) => BinaryPrimitives.WriteUInt64BigEndian(target, value);
}

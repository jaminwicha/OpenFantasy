using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class StructuralArchiveTests
{
    private static readonly ContentHash DependencyPin = ContentHash.Compute("structural-archive-dependencies"u8);

    [Fact]
    public void Graph_bytes_are_canonical_across_input_order_and_roundtrip_exactly()
    {
        StructuralLoadGraph graph = Cascade();
        var reordered = new StructuralLoadGraph(graph.Elements.Reverse(), graph.Bearings.Reverse());
        byte[] bytes = StructuralLoadCodec.EncodeGraph(graph);
        Assert.Equal(bytes, StructuralLoadCodec.EncodeGraph(reordered));
        StructuralLoadGraph restored = StructuralLoadCodec.DecodeGraph(bytes, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, StructuralLoadCodec.EncodeGraph(restored));
        Assert.Equal(graph.TopologicalOrder, restored.TopologicalOrder);
        Assert.Equal(graph.TotalInputMass, restored.TotalInputMass);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(64)]
    [InlineData(256)]
    public void Every_archived_slice_resumes_to_the_same_exact_completed_bytes(int budget)
    {
        StructuralLoadGraph graph = Cascade();
        StructuralLoadResolution reference = Finish(StructuralLoadResolution.Begin(graph, []), 256);
        byte[] expected = StructuralLoadCodec.Encode(reference, DependencyPin);
        StructuralLoadResolution state = StructuralLoadResolution.Begin(
            new StructuralLoadGraph(graph.Elements.Reverse(), graph.Bearings.Reverse()), []);
        int archives = 0;
        while (true)
        {
            Assert.InRange(archives++, 0, 32);
            byte[] archive = StructuralLoadCodec.Encode(state, DependencyPin);
            StructuralLoadResolution restored = Decode(archive, graph);
            Assert.Equal(archive, StructuralLoadCodec.Encode(restored, DependencyPin));
            Assert.Equal(state.Pass, restored.Pass);
            Assert.Equal(state.Cursor, restored.Cursor);
            Assert.Equal(state.TotalEvaluations, restored.TotalEvaluations);
            Assert.Equal(state.CurrentPassLoads, restored.CurrentPassLoads);
            Assert.Equal(state.Failures, restored.Failures);
            if (restored.IsComplete)
            {
                Assert.Equal(expected, archive);
                Assert.Equal(4U, restored.Pass);
                Assert.Equal(6UL, restored.TotalEvaluations);
                Assert.Equal((UInt128)80, restored.FailedInputMass);
                Assert.Equal((UInt128)0, restored.AcceptedFoundationLoad);
                Assert.Equal(3, restored.Failures.Count);
                break;
            }
            Assert.Throws<InvalidOperationException>(() => restored.ActiveLoads);
            state = restored.Advance(budget);
        }
        Assert.True(archives >= 4);
    }

    [Fact]
    public void Zero_evaluation_boundaries_preserve_their_distinct_exact_continuations()
    {
        var graph = new StructuralLoadGraph([Foundation(1, 100), Foundation(2, 100)], []);
        StructuralLoadResolution initial = StructuralLoadResolution.Begin(graph, [Id(2)]);
        StructuralLoadResolution partial = initial.Advance(1);
        StructuralLoadResolution complete = initial.Advance(2);
        Assert.Equal(partial.TotalEvaluations, complete.TotalEvaluations);
        Assert.False(Decode(StructuralLoadCodec.Encode(partial, DependencyPin), graph).IsComplete);
        Assert.True(Decode(StructuralLoadCodec.Encode(complete, DependencyPin), graph).IsComplete);
        byte[] completedBytes = StructuralLoadCodec.Encode(complete, DependencyPin);
        Assert.Equal(completedBytes, StructuralLoadCodec.Encode(
            Decode(StructuralLoadCodec.Encode(partial, DependencyPin), graph).Advance(1), DependencyPin));

        StructuralLoadResolution removed = StructuralLoadResolution.Begin(graph, [Id(2), Id(1)]).Advance(1);
        StructuralLoadResolution restored = Decode(StructuralLoadCodec.Encode(removed, DependencyPin), graph);
        Assert.True(restored.IsComplete);
        Assert.Equal(0UL, restored.TotalEvaluations);
        Assert.Equal(new[] { Id(1), Id(2) }, restored.RemovedIds);
        Assert.Equal(StructuralLoadCodec.Encode(removed, DependencyPin), StructuralLoadCodec.Encode(restored, DependencyPin));
    }

    [Fact]
    public void A_different_dependency_pin_cannot_resume_an_otherwise_valid_archive()
    {
        byte[] bytes = StructuralLoadCodec.Encode(StructuralLoadResolution.Begin(Cascade(), []).Advance(2), DependencyPin);
        ContentHash other = ContentHash.Compute("other-structural-dependencies"u8);
        Assert.Throws<FormatException>(() => StructuralLoadCodec.Decode(bytes, other, Cascade(), TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 1, other.ToByteArray())));
        Assert.Equal(bytes, StructuralLoadCodec.Encode(Decode(bytes), DependencyPin));
    }

    [Fact]
    public void Copied_dependency_pin_cannot_authorize_a_different_graph_mass_or_capacity()
    {
        StructuralLoadGraph original = Cascade();
        StructuralLoadGraph[] differentGraphs =
        [
            new([Element(1, 80, 100), Foundation(2, 31), Foundation(3, 60)], original.Bearings),
            new([Element(1, 79, 100), Foundation(2, 30), Foundation(3, 60)], original.Bearings)
        ];
        foreach (StructuralLoadGraph different in differentGraphs)
        {
            StructuralLoadResolution validButDifferent = StructuralLoadResolution.Begin(different, []).Advance(2);
            byte[] copiedPinArchive = StructuralLoadCodec.Encode(validButDifferent, DependencyPin);
            Assert.Equal(copiedPinArchive, StructuralLoadCodec.Encode(Decode(copiedPinArchive, different), DependencyPin));
            Assert.Throws<FormatException>(() => Decode(copiedPinArchive, original));
        }
    }

    [Fact]
    public void Canonical_frames_cannot_forge_pending_loads_first_failures_or_progress()
    {
        StructuralLoadResolution state = StructuralLoadResolution.Begin(Cascade(), []).Advance(2);
        Assert.Equal(1U, state.Pass);
        Assert.Equal(2, state.Cursor);
        Assert.Single(state.Failures);
        byte[] bytes = StructuralLoadCodec.Encode(state, DependencyPin);
        byte[] loads = Fields(bytes)[9];
        byte[] firstLoad = Fields(loads)[1];
        byte[] forgedLoads = ReplaceField(loads, 1, ReplaceField(firstLoad, 2, Wide(79)));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 9, forgedLoads)));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 9,
            ReplaceField(loads, 1, ReplaceField(firstLoad, 2, Wide(UInt128.MaxValue))))));

        byte[] failures = Fields(bytes)[8];
        byte[] firstFailure = Fields(failures)[1];
        byte[] forgedFailureLoad = ReplaceField(failures, 1, ReplaceField(firstFailure, 3, Wide(39)));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 8, forgedFailureLoad)));
        byte[] forgedReason = ReplaceField(failures, 1,
            ReplaceField(firstFailure, 2, Number((ulong)StructuralFailureReason.ConditionLost)));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 8, forgedReason)));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 8, Frame([Fields(failures)[0]]))));

        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 4, Number(2))));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 5, Number(1))));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 6, Number(3))));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 7, Number(1))));
        Assert.Throws<FormatException>(() => Decode(ReplaceField(bytes, 4, Number(ulong.MaxValue))));
    }

    [Fact]
    public void Noncanonical_graph_and_pending_row_orders_are_rejected()
    {
        byte[] graph = StructuralLoadCodec.EncodeGraph(Cascade());
        byte[][] elements = Fields(Fields(graph)[1]);
        (elements[1], elements[2]) = (elements[2], elements[1]);
        byte[] unorderedElements = ReplaceField(graph, 1, Frame(elements));
        Assert.Throws<FormatException>(() => StructuralLoadCodec.DecodeGraph(unorderedElements, TestContext.Current.CancellationToken));
        byte[][] bearings = Fields(Fields(graph)[2]);
        (bearings[1], bearings[2]) = (bearings[2], bearings[1]);
        byte[] unorderedBearings = ReplaceField(graph, 2, Frame(bearings));
        Assert.Throws<FormatException>(() => StructuralLoadCodec.DecodeGraph(unorderedBearings, TestContext.Current.CancellationToken));

        byte[] archive = StructuralLoadCodec.Encode(StructuralLoadResolution.Begin(Cascade(), []), DependencyPin);
        Assert.Throws<FormatException>(() => Decode(ReplaceField(archive, 2, unorderedElements)));
        byte[][] loads = Fields(Fields(archive)[9]);
        (loads[1], loads[2]) = (loads[2], loads[1]);
        Assert.Throws<FormatException>(() => Decode(ReplaceField(archive, 9, Frame(loads))));
    }

    [Fact]
    public void Every_truncated_prefix_and_any_trailing_byte_are_rejected()
    {
        StructuralLoadResolution state = StructuralLoadResolution.Begin(Cascade(), []).Advance(2);
        byte[] archive = StructuralLoadCodec.Encode(state, DependencyPin);
        byte[] graph = StructuralLoadCodec.EncodeGraph(state.Graph);
        for (int length = 0; length < archive.Length; length++)
        {
            byte[] prefix = archive[..length];
            Assert.Throws<FormatException>(() => Decode(prefix));
        }
        for (int length = 0; length < graph.Length; length++)
        {
            byte[] prefix = graph[..length];
            Assert.Throws<FormatException>(() => StructuralLoadCodec.DecodeGraph(prefix, TestContext.Current.CancellationToken));
        }
        Assert.Throws<FormatException>(() => Decode([.. archive, 0]));
        Assert.Throws<FormatException>(() => StructuralLoadCodec.DecodeGraph([.. graph, 0], TestContext.Current.CancellationToken));
        byte[] excessiveCount = (byte[])archive.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(excessiveCount.AsSpan(8), uint.MaxValue);
        Assert.Throws<FormatException>(() => Decode(excessiveCount));
    }

    [Fact]
    public void Canceled_decode_does_not_publish_a_graph_or_continuation()
    {
        StructuralLoadGraph graph = Cascade();
        byte[] graphBytes = StructuralLoadCodec.EncodeGraph(graph);
        byte[] archive = StructuralLoadCodec.Encode(StructuralLoadResolution.Begin(graph, []).Advance(2), DependencyPin);
        var canceled = new CancellationToken(canceled: true);
        Assert.Throws<OperationCanceledException>(() => StructuralLoadCodec.DecodeGraph(graphBytes, canceled));
        Assert.Throws<OperationCanceledException>(() => StructuralLoadCodec.Decode(archive, DependencyPin, graph, canceled));
        Assert.Equal(archive, StructuralLoadCodec.Encode(Decode(archive), DependencyPin));
    }

    [Fact]
    public void Wide_pending_and_failed_loads_preserve_both_halves_without_truncation()
    {
        var graph = new StructuralLoadGraph(
            [Element(1, ulong.MaxValue, ulong.MaxValue), Element(2, ulong.MaxValue, ulong.MaxValue),
                new StructuralElement(Id(3), new(ulong.MaxValue), new(ulong.MaxValue), new(ulong.MaxValue), 10_000, true, 0)],
            [new(Id(10), Id(1), Id(3), 1), new(Id(20), Id(2), Id(3), 1)]);
        UInt128 expected = (UInt128)ulong.MaxValue * 4;
        StructuralLoadResolution partial = StructuralLoadResolution.Begin(graph, []).Advance(2);
        Assert.Equal(expected, partial.CurrentPassLoads[2]);
        Assert.True(expected > ulong.MaxValue);
        byte[] partialBytes = StructuralLoadCodec.Encode(partial, DependencyPin);
        byte[] widePending = Fields(Fields(Fields(partialBytes)[9])[3])[2];
        Assert.Equal(16, widePending.Length);
        Assert.Equal(3UL, BinaryPrimitives.ReadUInt64BigEndian(widePending));
        Assert.Equal(ulong.MaxValue - 3, BinaryPrimitives.ReadUInt64BigEndian(widePending.AsSpan(8)));
        Assert.Equal(expected, Decode(partialBytes, graph).CurrentPassLoads[2]);

        StructuralLoadResolution final = Finish(Decode(partialBytes, graph), 1);
        StructuralFailure foundationFailure = final.Failures.Single(value => value.ElementId == Id(3));
        Assert.Equal(expected, foundationFailure.Load);
        Assert.Equal(expected, final.FailedInputMass);
        Assert.Equal(expected, graph.TotalInputMass);
        byte[] finalBytes = StructuralLoadCodec.Encode(final, DependencyPin);
        byte[] wideFailure = Fields(Fields(Fields(finalBytes)[8])[3])[3];
        Assert.Equal(widePending, wideFailure);
        StructuralLoadResolution restored = Decode(finalBytes, graph);
        Assert.Equal(expected, restored.Failures.Single(value => value.ElementId == Id(3)).Load);
        Assert.Equal(expected, restored.FailedInputMass);
        Assert.Equal(finalBytes, StructuralLoadCodec.Encode(restored, DependencyPin));
        Assert.Equal(finalBytes, StructuralLoadCodec.Encode(Finish(StructuralLoadResolution.Begin(graph, []), 256), DependencyPin));
    }

    private static StructuralLoadResolution Decode(byte[] bytes, StructuralLoadGraph? expectedGraph = null) =>
        StructuralLoadCodec.Decode(bytes, DependencyPin, expectedGraph ?? Cascade(), TestContext.Current.CancellationToken);

    private static StructuralLoadResolution Finish(StructuralLoadResolution state, int budget)
    {
        int calls = 0;
        while (!state.IsComplete)
        {
            Assert.InRange(calls++, 0, 32);
            state = state.Advance(budget);
        }
        return state;
    }

    private static StructuralLoadGraph Cascade() => new(
        [Element(1, 80, 100), Foundation(2, 30), Foundation(3, 60)],
        [new(Id(10), Id(1), Id(2), 1), new(Id(20), Id(1), Id(3), 1)]);

    private static EntityId Id(ulong value) => new(0x61726368697665, value);
    private static StructuralElement Element(ulong id, ulong mass, ulong capacity) =>
        new(Id(id), new(mass), new(0), new(capacity), 10_000, false);
    private static StructuralElement Foundation(ulong id, ulong capacity) =>
        new(Id(id), new(0), new(0), new(capacity), 10_000, true, 0);

    // Independent framing helpers mutate retained bytes rather than constructing invalid domain objects.
    // Field ordinals include the UTF-8 tag at index zero.
    private static byte[][] Fields(byte[] frame)
    {
        Assert.True(frame.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(4)));
        uint count = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8));
        byte[][] result = new byte[checked((int)count)][];
        int offset = 12;
        for (int index = 0; index < result.Length; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(offset)));
            offset = checked(offset + 4);
            result[index] = frame.AsSpan(offset, length).ToArray();
            offset = checked(offset + length);
        }
        Assert.Equal(frame.Length, offset);
        return result;
    }

    private static byte[] ReplaceField(byte[] frame, int index, byte[] value)
    {
        byte[][] fields = Fields(frame);
        fields[index] = value;
        return Frame(fields);
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] frame = new byte[checked(12 + fields.Sum(field => checked(4 + field.Length)))];
        "OFC\0"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), checked((uint)field.Length));
            offset = checked(offset + 4);
            field.CopyTo(frame, offset);
            offset = checked(offset + field.Length);
        }
        return frame;
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Wide(UInt128 value)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, (ulong)(value >> 64));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), (ulong)(value & ulong.MaxValue));
        return bytes;
    }
}

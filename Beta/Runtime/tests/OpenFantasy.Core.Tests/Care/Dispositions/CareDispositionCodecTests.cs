using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.Dispositions.CareDispositionFixture;

namespace OpenFantasy.Core.Tests.Care.Dispositions;

public sealed class CareDispositionCodecTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void V5_roundtrips_explicit_inline_and_continued_owners_including_empty_capability(bool continued, bool withClosure)
    {
        var acknowledgment = Acknowledgment();
        var state = withClosure ? CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)])
            : CareDispositionFixture.Create();
        if (continued) state = CareContinuationContext.Capture(state).Continue();
        byte[] bytes = CareCodec.Encode(state);
        Assert.Equal(Number(5), Fields(bytes)[1]);
        var restored = CareCodec.Decode(bytes, state.Continuation);
        Assert.True(restored.SupportsDispositions);
        Assert.Equal(state.TotalDispositions, restored.TotalDispositions);
        Assert.Equal(bytes, CareCodecV5.Encode(restored));
        Assert.Throws<ArgumentException>(() => CareCodecV1.Encode(state));
        Assert.Throws<ArgumentException>(() => CareCodecV2.Encode(state));
        Assert.Throws<ArgumentException>(() => CareCodecV3.Encode(state));
        Assert.Throws<ArgumentException>(() => CareCodecV4.Encode(state));
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(bytes));
        Assert.Throws<FormatException>(() => CareCodecV2.Decode(bytes));
        Assert.Throws<FormatException>(() => CareCodecV4.Decode(bytes));
        if (state.Continuation is { } context)
        {
            Assert.Throws<FormatException>(() => CareCodecV3.Decode(bytes, context));
            Assert.Throws<FormatException>(() => CareCodecV4.Decode(bytes, context));
            Assert.Throws<FormatException>(() => CareCodec.Decode(bytes));
            Assert.Throws<ArgumentException>(() => CareCodecV3.EncodeContinuation(context));
            Assert.Throws<ArgumentException>(() => CareCodecV4.EncodeContinuation(context));
            Assert.Equal(context.ToCommitment(), CareCodecV5.EncodeContinuation(context));
        }
        else Assert.Throws<FormatException>(() => CareCodecV5.Decode(bytes, CareContinuationContext.Capture(state)));
    }

    [Fact]
    public void Legacy_constructor_and_bytes_keep_the_old_interpretation()
    {
        var legacy = OrdinaryCareFixture.CreateOrdinary();
        byte[] bytes = CareCodec.Encode(legacy);
        Assert.False(legacy.SupportsDispositions);
        Assert.Empty(legacy.Dispositions);
        Assert.Equal(0UL, legacy.TotalDispositions);
        Assert.Equal("9cff0a1d0e415851e069bbd2836bd81781ab40bc6917704bbf8fb6eb56131e8f", ContentHash.Compute(bytes).ToString());
        Assert.Equal(bytes, CareCodecV2.Encode(CareCodec.Decode(bytes)));
        Assert.Throws<ArgumentException>(() => CareCodecV5.Encode(legacy));
        Assert.Throws<FormatException>(() => CareCodecV5.Decode(bytes));
        Assert.Throws<InvalidOperationException>(() => legacy.CloseRequest(Disposition(Acknowledgment())));
        var context = CareContinuationContext.Capture(legacy);
        Assert.False(context.SupportsDispositions);
        Assert.Empty(context.ClosedDefeatWitnesses);
        Assert.Throws<ArgumentException>(() => CareCodecV5.EncodeContinuation(context));
        Assert.Equal(4_096, CareCodecV3.MaximumContinuationByteLength);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void V5_refuses_foreign_version_policy_revision_or_totals(int field)
    {
        var acknowledgment = Acknowledgment();
        byte[] bytes = CareCodec.Encode(CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)]));
        byte[] changed = field == 5 ? new byte[32] : Number(99);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Replace(bytes, field, changed)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void V5_refuses_forged_disposition_marker_identity_request_actor_kind_or_sequence(int field)
    {
        var acknowledgment = Acknowledgment();
        byte[] bytes = CareCodec.Encode(CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)]));
        var root = Fields(bytes);
        var records = Fields(root[14]);
        var record = Fields(records[1]);
        record[field] = field switch { 0 => Text("care.disposition.v2"), 5 => Number(0), 6 => Number(11), _ => Id(999).ToByteArray() };
        records[1] = Frame(record);
        root[14] = Frame(records);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
    }

    [Fact]
    public void V5_requires_exact_context_and_refuses_malformed_context_counters_markers_and_trailing_data()
    {
        var acknowledgment = Acknowledgment();
        var closed = CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)]);
        var context = CareContinuationContext.Capture(closed);
        byte[] bytes = CareCodec.Encode(context.Continue());
        var foreign = CareContinuationContext.Capture(CareDispositionFixture.Create([acknowledgment],
            dispositions: [Disposition(acknowledgment, CareDispositionKind.HelperDeclined)]));
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes, foreign));
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes[..^1], context));
        Assert.Throws<FormatException>(() => CareCodec.Decode([.. bytes, 0], context));
        foreach (int field in new[] { 0, 3, 7, 8, 9, 11 })
        {
            byte[] corrupted = ChangeNested(bytes, 9, fields =>
            {
                fields[field] = field == 0 ? Text("care.continuation.v1") : field == 3 ? new byte[32] : Number(99);
                return fields;
            });
            Assert.Throws<FormatException>(() => CareCodec.Decode(corrupted, context));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V5_preserves_ordinary_or_recovery_interpretation_with_and_without_continuation(bool recovery)
    {
        var ordinary = recovery ? Recovery.RecoveryCareFixture.Create().Ordinary! : OrdinaryCareFixture.Ordinary();
        var acknowledgment = Acknowledgment();
        var state = CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)], ordinary: ordinary);
        foreach (var candidate in new[] { state, CareContinuationContext.Capture(state).Continue() })
        {
            byte[] bytes = CareCodec.Encode(candidate);
            var restored = CareCodec.Decode(bytes, candidate.Continuation);
            Assert.Equal(recovery, restored.Ordinary!.SupportsIncapacitatedAssistance);
            Assert.Equal(bytes, CareCodec.Encode(restored));
            var fields = Fields(bytes);
            fields[1] = Number(4);
            Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(fields), candidate.Continuation));
        }
    }
}

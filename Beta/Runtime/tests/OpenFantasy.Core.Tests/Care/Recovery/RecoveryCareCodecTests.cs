using System.Text;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.Recovery.RecoveryCareFixture;

namespace OpenFantasy.Core.Tests.Care.Recovery;

public sealed class RecoveryCareCodecTests
{
    [Fact]
    public void Mixed_recovery_and_living_uses_keep_their_separate_record_markers()
    {
        var state = Create([Use(), OrdinaryCareFixture.Use(self: true, sequence: 21, command: 50_002)]);
        byte[] bytes = CareCodec.Encode(state);
        var restored = CareCodec.Decode(bytes);
        Assert.Equal(new[] { 0, 10 }, restored.Ordinary!.Uses.OrderBy(value => value.WorldSequence).Select(value => value.HpBefore));
        Assert.Equal(bytes, CareCodec.Encode(restored));
        List<byte[]> root = Fields(bytes);
        List<byte[]> ordinary = Fields(root[9]);
        string[] definitionMarkers = Fields(ordinary[3]).Skip(1)
            .Select(frame => Encoding.UTF8.GetString(Fields(frame)[0])).ToArray();
        string[] useMarkers = Fields(ordinary[5]).Skip(1)
            .Select(frame => Encoding.UTF8.GetString(Fields(frame)[0])).ToArray();
        Assert.Equal(new[] { "care.use-definition.v1", "care.use-definition.v2" }, definitionMarkers);
        Assert.Equal(new[] { "care.ordinary-use.v1", "care.ordinary-use.v2" }, useMarkers.Order());
        Assert.Throws<ArgumentException>(() => CareCodecV2.Encode(state));
        // Matching the old outer versions cannot make its reader admit recovery record interpretations.
        root[1] = Number(2);
        ordinary[1] = Number(1);
        root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodecV2.Decode(Frame(root)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V4_roundtrips_only_its_explicit_inline_or_context_bound_recovery_owner(bool continued)
    {
        CareSnapshot state = Create([Use()]);
        if (continued) state = CareContinuationContext.Capture(state).Continue();
        byte[] bytes = CareCodec.Encode(state);
        Assert.Equal(Number(4), Fields(bytes)[1]);
        Assert.Equal(bytes, CareCodecV4.Encode(state));
        CareSnapshot restored = CareCodec.Decode(bytes, state.Continuation);
        Assert.True(restored.Ordinary!.SupportsIncapacitatedAssistance);
        Assert.Equal(bytes, CareCodec.Encode(restored));
        Assert.Equal(state.TotalOrdinaryUses, restored.TotalOrdinaryUses);
        Assert.Throws<ArgumentException>(() => CareCodecV1.Encode(state));
        Assert.Throws<ArgumentException>(() => CareCodecV2.Encode(state));
        Assert.Throws<ArgumentException>(() => CareCodecV3.Encode(state));
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(bytes));
        Assert.Throws<FormatException>(() => CareCodecV2.Decode(bytes));
        if (state.Continuation is { } context)
        {
            Assert.Throws<FormatException>(() => CareCodec.Decode(bytes));
            Assert.Throws<FormatException>(() => CareCodecV3.Decode(bytes, context));
            Assert.Throws<ArgumentException>(() => CareCodecV3.EncodeContinuation(context));
            Assert.Equal("care.continuation.v2", Encoding.UTF8.GetString(Fields(CareCodecV4.EncodeContinuation(context))[0]));
            Assert.Throws<FormatException>(() => CareCodec.Decode(CareCodec.Encode(Create()), context));
        }
        else
        {
            var inlineContext = CareContinuationContext.Capture(state);
            Assert.Throws<FormatException>(() => CareCodecV4.Decode(bytes, inlineContext));
        }
    }

    [Theory]
    [InlineData("root-version")]
    [InlineData("ordinary-version")]
    [InlineData("definition-marker")]
    [InlineData("delivery")]
    [InlineData("use-marker")]
    [InlineData("hp-before")]
    [InlineData("permission")]
    public void V4_markers_and_catalog_health_interpretation_cannot_be_downgraded(string corruption)
    {
        List<byte[]> root = Fields(CareCodec.Encode(Create([Use()])));
        List<byte[]> ordinary = Fields(root[9]);
        List<byte[]> definitions = Fields(ordinary[3]);
        // The self-use definition sorts first; the recovery definition is explicitly pinned second.
        List<byte[]> definition = Fields(definitions[2]);
        List<byte[]> uses = Fields(ordinary[5]);
        List<byte[]> use = Fields(uses[1]);
        switch (corruption)
        {
            case "root-version": root[1] = Number(2); break;
            case "ordinary-version": ordinary[1] = Number(1); break;
            case "definition-marker": definition[0] = "care.use-definition.v1"u8.ToArray(); break;
            case "delivery": definition[4] = Number(2); break;
            case "use-marker": use[0] = "care.ordinary-use.v1"u8.ToArray(); break;
            case "hp-before": use[15] = Number(1); break;
            case "permission": use[7] = []; break;
        }
        definitions[2] = Frame(definition);
        ordinary[3] = Frame(definitions);
        uses[1] = Frame(use);
        ordinary[5] = Frame(uses);
        root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
    }

    [Fact]
    public void Legacy_catalog_bytes_and_digest_remain_exact_and_are_not_promoted_to_V4()
    {
        CareSnapshot legacy = OrdinaryCareFixture.CreateOrdinary();
        byte[] bytes = CareCodec.Encode(legacy);
        Assert.Equal("9cff0a1d0e415851e069bbd2836bd81781ab40bc6917704bbf8fb6eb56131e8f", ContentHash.Compute(bytes).ToString());
        Assert.Equal(bytes, CareCodecV2.Encode(CareCodec.Decode(bytes)));
        Assert.Throws<ArgumentException>(() => CareCodecV4.Encode(legacy));
        Assert.Throws<FormatException>(() => CareCodecV4.Decode(bytes));
        var context = CareContinuationContext.Capture(legacy);
        Assert.Throws<ArgumentException>(() => CareCodecV4.Encode(context.Continue()));
        Assert.Throws<ArgumentException>(() => CareCodecV4.EncodeContinuation(context));
        Assert.Equal("care.continuation.v1", Encoding.UTF8.GetString(Fields(context.ToCommitment())[0]));
    }

    [Fact]
    public void Continued_recovery_refuses_changed_commitment_truncation_trailing_data_and_wrong_totals()
    {
        var state = CareContinuationContext.Capture(Create([Use()])).Continue();
        var context = state.Continuation!;
        byte[] bytes = CareCodec.Encode(state);
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes[..^1], context));
        Assert.Throws<FormatException>(() => CareCodec.Decode([.. bytes, 0], context));
        Assert.Throws<FormatException>(() => CareCodec.Decode(ChangeNested(bytes, 5, fields =>
        {
            fields[0] = "care.continuation.v1"u8.ToArray();
            return fields;
        }), context));
        Assert.Throws<FormatException>(() => CareCodec.Decode(ChangeNested(bytes, 9, fields =>
        {
            fields[3] = Number(2);
            return fields;
        }), context));
    }
}

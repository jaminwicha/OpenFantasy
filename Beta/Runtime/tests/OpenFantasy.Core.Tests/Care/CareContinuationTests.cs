using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.OrdinaryCareFixture;

namespace OpenFantasy.Core.Tests.Care;

/// <summary>Care structural evidence only; no world, dose or host authentication is exercised here.</summary>
public sealed class CareContinuationTests
{
    private static CareSnapshot Cut(CareSnapshot closing) => CareContinuationContext.Capture(closing).Continue();

    private static CareSnapshot SelfUse(CareSnapshot state, ulong index) => state.WithOrdinary(state.Ordinary!.RecordUse(
        Use(self: true, sequence: 100 + index, command: 20_000 + index, patientRevision: 4 + index)));

    private static CareSnapshot Pair(CareSnapshot state, ulong index)
    {
        var acknowledgment = Acknowledgment(11 + index * 3, 2_000 + index, Defeat(10 + index * 3, 7 + index * 2, 1_000 + index));
        return state.Acknowledge(acknowledgment).Treat(Treatment(acknowledgment, 12 + index * 3, 3_000 + index, 3 + index * 2));
    }

    [Fact]
    public void Ordinary_uses_keep_exact_totals_and_bounded_history_across_two_closures()
    {
        var state = CreateOrdinary();
        for (ulong index = 0; index < 520; index++)
        {
            if (index is 200 or 400) state = Cut(state);
            state = SelfUse(state, index);
        }
        Assert.Equal((520UL, 520UL, 2UL), (state.TotalOrdinaryUses, state.Revision, state.Continuation!.Ordinal));
        Assert.Equal(120, state.Ordinary!.Uses.Count);
        Assert.Equal(400UL, state.Continuation.ArchivedOrdinaryUses);
        Assert.Throws<ArgumentException>(() => SelfUse(state, 399));
        Assert.Throws<InvalidOperationException>(() => CareContinuationContext.Capture(Cut(state)));
    }

    [Fact]
    public void Aftermath_pairs_pending_aid_and_patient_witness_survive_closures()
    {
        var state = Create();
        for (ulong index = 0; index < 520; index++)
        {
            if (index is 200 or 400) state = Cut(state);
            state = Pair(state, index);
        }
        Assert.Equal((520UL, 520UL, 1_040UL), (state.TotalAcknowledgments, state.TotalTreatments, state.Revision));
        Assert.Equal(120, state.Treatments.Count);
        var pending = Acknowledgment(1_571, 2_520, Defeat(1_570, 1_047, 1_520));
        var context = CareContinuationContext.Capture(state.Acknowledge(pending));
        var continued = context.Continue();
        Assert.Equal((521UL, 520UL, 1_041UL), (continued.TotalAcknowledgments, continued.TotalTreatments, continued.Revision));
        Assert.Same(pending, continued.Pending);
        Assert.InRange(context.ToCommitment().Length, 1, CareCodecV3.MaximumContinuationByteLength);
        Assert.Equal(1_569UL, Assert.Single(context.PatientWitnesses).Treatment.WorldSequence);
        var treated = continued.Treat(Treatment(pending, 1_572, 3_520, 1_043));
        Assert.Equal(1_042UL, treated.Revision);
        continued.ValidateSuccessor(treated);
        Assert.Throws<ArgumentException>(() => continued.ValidateSuccessor(Create()));
        Assert.Throws<ArgumentException>(() => continued.Treat(Treatment(pending, 1_572, 3_520, 1_042)));
        Assert.Throws<ArgumentException>(() => treated.Acknowledge(Acknowledgment(1_580, 2_521, Defeat(1_572, 1_049, 1_521))));
        Assert.Throws<ArgumentException>(() => treated.Acknowledge(Acknowledgment(1_500, 2_521, Defeat(1_499, 1_049, 1_521))));
    }

    [Fact]
    public void Revocation_counts_once_and_denies_aid_after_repeated_closures()
    {
        var state = CreateOrdinary(Ordinary().RecordUse(Use()).Revoke(Revocation()));
        state = SelfUse(Cut(state), 0);
        state = Cut(state);
        Assert.Equal((3UL, 2UL), (state.Revision, state.TotalOrdinaryUses));
        Assert.False(Assert.Single(state.Ordinary!.Permissions).IsActive);
        Assert.Throws<ArgumentException>(() => state.Ordinary.RecordUse(Use(sequence: 500, command: 30_000)));
        Assert.Throws<InvalidOperationException>(() => state.Ordinary.Revoke(Revocation(501, 0, 30_001)));
        Assert.Throws<ArgumentException>(() => state.Ordinary.RecordUse(Use(self: true, sequence: 100, command: 30_002)));
        Assert.Throws<ArgumentException>(() => new CareSnapshot(World, Branch, 3, [Remedy()], [Agreement()], [], [], state.Ordinary));
    }

    [Fact]
    public void Continued_bytes_require_their_exact_context_and_fail_closed()
    {
        var state = SelfUse(Cut(Pair(CreateOrdinary(), 0)), 0);
        var context = state.Continuation!;
        byte[] bytes = CareCodec.Encode(state);
        Assert.Equal(Number(3), Fields(bytes)[1]);
        Assert.Equal(bytes, CareCodec.Encode(CareCodec.Decode(bytes, context)));
        Assert.Throws<ArgumentException>(() => CareCodecV2.Encode(state));
        Assert.Throws<ArgumentException>(() => CareCodecV1.Encode(Cut(Create())));
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes));
        Assert.Throws<FormatException>(() => CareCodecV2.Decode(bytes));
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes, CareContinuationContext.Capture(state)));
        Assert.Throws<FormatException>(() => CareCodec.Decode(CareCodec.Encode(CreateOrdinary()), context));
        Assert.Throws<FormatException>(() => CareCodec.Decode(bytes[..^1], context));
        Assert.Throws<FormatException>(() => CareCodec.Decode([.. bytes, 0], context));
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame([.. Fields(bytes), []]), context));
        Assert.Throws<FormatException>(() => CareCodec.Decode(Replace(bytes, 4, Number(ulong.MaxValue)), context));
        for (int field = 3; field <= 9; field++)
        {
            int index = field;
            Assert.Throws<FormatException>(() => CareCodec.Decode(ChangeNested(bytes, 5, fields =>
            {
                fields[index] = Number(7);
                return fields;
            }), context));
        }
        Assert.Throws<FormatException>(() => CareCodec.Decode(ChangeNested(bytes, 8, fields =>
        {
            fields[2] = Number(9);
            return fields;
        }), context));
        Assert.Throws<FormatException>(() => CareCodec.Decode(ChangeNested(bytes, 9, fields =>
        {
            fields[3] = Number(9);
            return fields;
        }), context));
    }
}

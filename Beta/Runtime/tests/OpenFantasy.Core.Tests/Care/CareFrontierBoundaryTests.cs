using System.Reflection;
using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.OrdinaryCareFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class CareFrontierBoundaryTests
{
    [Fact]
    public void Ordinary_commands_can_share_a_later_world_sequence_in_strict_ordinal_order()
    {
        var state = CareContinuationContext.Capture(CreateOrdinary()).Continue();
        var first = state.WithOrdinary(state.Ordinary!.RecordUse(Use(self: true, sequence: 20, ordinal: 0)));
        var second = first.WithOrdinary(first.Ordinary!.RecordUse(Use(self: true, sequence: 20, ordinal: 1, command: 10_001)));
        first.ValidateSuccessor(second);
        Assert.Equal(2UL, second.Revision);
        Assert.Throws<ArgumentException>(() => second.WithOrdinary(second.Ordinary!.RecordUse(
            Use(self: true, sequence: 20, ordinal: 0, command: 10_002))));
    }

    [Fact]
    public void Full_active_windows_fit_the_unchanged_byte_bound_and_still_refuse_overflow()
    {
        var state = CareContinuationContext.Capture(CreateOrdinary()).Continue();
        for (ulong index = 0; index < 256; index++)
        {
            var ack = Acknowledgment(11 + index * 3, 2_000 + index, Defeat(10 + index * 3, 7 + index * 2, 1_000 + index));
            state = state.Acknowledge(ack).Treat(Treatment(ack, 12 + index * 3, 3_000 + index, 3 + index * 2));
        }
        for (ulong index = 0; index < 256; index++)
            state = state.WithOrdinary(state.Ordinary!.RecordUse(Use(self: true, sequence: 1_000 + index, command: 60_000 + index)));
        Assert.Equal(768UL, state.Revision);
        byte[] encoded = CareCodec.Encode(state);
        Assert.InRange(encoded.Length, 1, CareCodecV3.MaximumEncodedByteLength);
        Assert.Equal(encoded, CareCodec.Encode(CareCodec.Decode(encoded, state.Continuation)));
        Assert.Throws<ArgumentException>(() => state.Ordinary!.RecordUse(Use(self: true, sequence: 2_000, command: 70_000)));
        var next = CareContinuationContext.Capture(state).Continue();
        Assert.Empty(next.Ordinary!.Uses);
        Assert.Empty(next.Treatments);
        Assert.Equal(state.Revision, next.Revision);
    }

    [Fact]
    public void Successor_validation_refuses_backfilled_completed_aftermath_pairs()
    {
        var basis = CareContinuationContext.Capture(CreateOrdinary()).Continue();
        var before = AddPair(basis, 1);
        var backfilled = AddPair(AddPair(basis, 0), 1);
        Assert.Throws<ArgumentException>(() => before.ValidateSuccessor(backfilled));
        before.ValidateSuccessor(AddPair(before, 2));

        static CareSnapshot AddPair(CareSnapshot state, ulong index)
        {
            var ack = Acknowledgment(11 + index * 3, 2_000 + index,
                Defeat(10 + index * 3, 7 + index * 2, 1_000 + index));
            return state.Acknowledge(ack).Treat(Treatment(ack, 12 + index * 3, 3_000 + index, 3 + index * 2));
        }
    }

    [Fact]
    public void Ordinary_append_cannot_backdate_itself_before_completed_aftermath()
    {
        var state = CareContinuationContext.Capture(CreateOrdinary()).Continue();
        var ack = Acknowledgment();
        state = state.Acknowledge(ack).Treat(Treatment(ack));
        var backdated = state.Ordinary!.RecordUse(Use(self: true, sequence: 10, command: 40_000));
        Assert.Throws<ArgumentException>(() => state.WithOrdinary(backdated));
        var later = state.WithOrdinary(state.Ordinary.RecordUse(Use(self: true, sequence: 13, command: 40_001)));
        state.ValidateSuccessor(later);
        Assert.Equal(3UL, later.Revision);
    }

    [Fact]
    public void Pending_aid_blocks_ordinary_care_and_cannot_be_removed_from_the_archive()
    {
        var ack = Acknowledgment();
        var closing = CreateOrdinary().Acknowledge(ack);
        var context = CareContinuationContext.Capture(closing);
        var state = context.Continue();
        context.ValidateClosure(closing, state);
        Assert.Throws<ArgumentException>(() => state.WithOrdinary(state.Ordinary!.RecordUse(
            Use(self: true, sequence: 20, command: 40_010))));
        var frame = Fields(CareCodec.Encode(state));
        var aftermath = Fields(frame[8]);
        aftermath[2] = Number(0);
        aftermath[4] = Frame([Fields(aftermath[4])[0]]);
        frame[4] = Number(0);
        frame[8] = Frame(aftermath);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(frame), context));
        var treated = state.Treat(Treatment(ack));
        Assert.Throws<ArgumentException>(() => context.ValidateClosure(closing, treated));
        Assert.Null(treated.Pending);
    }

    [Fact]
    public void Every_admitted_patient_keeps_their_own_latest_witness_in_a_bounded_context()
    {
        var agreements = Enumerable.Range(0, CareSnapshot.MaximumAgreements)
            .Select(index => Agreement(200 + (ulong)index, Id(20 + (ulong)index))).ToArray();
        var state = CareModelFixture.Create(agreements: agreements);
        for (int index = 0; index < agreements.Length; index++)
        {
            var agreement = agreements[index];
            ulong offset = (ulong)index * 3;
            var ack = Acknowledgment(11 + offset, 2_000 + (ulong)index,
                Defeat(10 + offset, 7, 1_000 + (ulong)index, agreement.PatientActorId), agreement: agreement);
            state = state.Acknowledge(ack).Treat(Treatment(ack, 12 + offset, 3_000 + (ulong)index));
        }
        var context = CareContinuationContext.Capture(state);
        Assert.Equal(CareSnapshot.MaximumAgreements, context.PatientWitnesses.Count);
        Assert.Equal(agreements.Select(value => value.PatientActorId).Order(), context.PatientWitnesses.Select(value => value.PatientActorId));
        Assert.InRange(context.ToCommitment().Length, 1, CareCodecV3.MaximumContinuationByteLength);
        var continued = context.Continue();
        Assert.Empty(continued.Treatments);
        Assert.Throws<ArgumentException>(() => continued.Acknowledge(Acknowledgment(100, 50_000,
            Defeat(12, 9, 50_001, agreements[0].PatientActorId), agreement: agreements[0])));
        foreach (var field in typeof(CareContinuationContext).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            object? value = field.GetValue(context);
            Assert.IsNotType<CareSnapshot>(value);
            Assert.IsNotType<CareContinuationContext>(value);
            if (value is System.Collections.IEnumerable collection)
                foreach (object? entry in collection)
                {
                    Assert.IsNotType<CareSnapshot>(entry);
                    Assert.IsNotType<CareContinuationContext>(entry);
                }
        }
        Assert.Equal(CareCodec.Encode(continued), CareCodec.Encode(CareCodec.Decode(CareCodec.Encode(continued), context)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reordered_or_duplicate_retained_use_rows_fail_canonical_decoding(bool duplicate)
    {
        var state = CareContinuationContext.Capture(CreateOrdinary()).Continue();
        for (ulong index = 0; index < 2; index++)
            state = state.WithOrdinary(state.Ordinary!.RecordUse(Use(self: true, sequence: 20 + index, command: 60_000 + index)));
        var root = Fields(CareCodec.Encode(state));
        var ordinary = Fields(root[9]);
        var rows = Fields(ordinary[6]);
        ordinary[6] = duplicate ? Frame([rows[0], rows[1], rows[1]]) : Frame([rows[0], rows[2], rows[1]]);
        root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root), state.Continuation));
    }

    [Fact]
    public void A_consistent_rewritten_catalog_cannot_replace_the_captured_catalog()
    {
        var context = CareContinuationContext.Capture(CareModelFixture.Create()).Continue().Continuation!;
        var changedRemedy = Remedy(hp: 41);
        var forgedCatalog = CareModelFixture.Create(remedies: [changedRemedy], agreements: [Agreement(remedy: changedRemedy)]);
        var replacements = Fields(CareCodec.Encode(forgedCatalog));
        var encoded = Fields(CareCodec.Encode(context.Continue()));
        encoded[6] = replacements[5];
        encoded[7] = replacements[6];
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(encoded), context));
    }

    [Fact]
    public void Empty_first_closure_is_valid_but_repeated_no_progress_and_context_changes_are_refused()
    {
        var closing = CreateOrdinary();
        var context = CareContinuationContext.Capture(closing);
        var continued = context.Continue();
        context.ValidateClosure(closing, continued);
        Assert.False(CareContinuationContext.CanCapture(continued));
        Assert.Throws<InvalidOperationException>(() => CareContinuationContext.Capture(continued));
        var used = continued.WithOrdinary(continued.Ordinary!.RecordUse(Use(self: true)));
        var next = CareContinuationContext.Capture(used);
        Assert.Throws<ArgumentException>(() => continued.ValidateSuccessor(next.Continue()));
        Assert.Throws<ArgumentException>(() => next.ValidateClosure(closing, next.Continue()));
        Assert.Throws<ArgumentException>(() => continued.ValidateSuccessor(closing));
    }
}

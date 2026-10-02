using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.Dispositions.CareDispositionFixture;

namespace OpenFantasy.Core.Tests.Care.Dispositions;

public sealed class CareDispositionBoundsTests
{
    [Fact]
    public void Maximum_composite_frontier_retains_eight_exact_defeats_treatments_and_a_pending_request()
    {
        var remedies = Enumerable.Range(0, CareSnapshot.MaximumRemedies).Select(index => Remedy(index == 0 ? 100UL : (ulong)(110 + index))).ToArray();
        var agreements = Enumerable.Range(0, CareSnapshot.MaximumAgreements)
            .Select(index => Agreement((ulong)(200 + index), Id((ulong)(100_000 + index)), remedies[index])).ToArray();
        var cancelled = Enumerable.Range(0, 64).Select(index => Id((ulong)(10_000 + index))).ToArray();
        List<CareAcknowledgment> acknowledgments = [];
        List<CareTreatment> treatments = [];
        for (int index = 0; index < agreements.Length; index++)
        {
            ulong sequence = (ulong)(10 + index * 3);
            var acknowledgment = Acknowledgment(sequence + 1, (ulong)(2_000 + index),
                Defeat(sequence, command: (ulong)(1_000 + index), patient: agreements[index].PatientActorId, cancelled: cancelled), agreement: agreements[index]);
            acknowledgments.Add(acknowledgment);
            treatments.Add(Treatment(acknowledgment, sequence + 2, (ulong)(3_000 + index)));
        }
        var pending = Acknowledgment(51, 2_100, Defeat(50, command: 1_100, patient: agreements[0].PatientActorId, cancelled: cancelled), agreement: agreements[0]);
        acknowledgments.Add(pending);
        var ordinary = OrdinaryCareFixture.Ordinary();
        var state = CareSnapshot.CreateWithDispositions(World, Branch, 17, remedies, agreements, acknowledgments, treatments, [], ordinary);
        var context = CareContinuationContext.Capture(state);
        Assert.Equal(8, context.PatientWitnesses.Count);
        Assert.Equal(8, context.ClosedDefeatWitnesses.Count);
        Assert.All(context.ClosedDefeatWitnesses, value => Assert.Equal(64, value.Acknowledgment.Defeat.CancelledDecisionIds.Count));
        Assert.Same(pending, context.Pending);
        byte[] commitment = context.ToCommitment();
        Assert.True(commitment.Length > CareCodecV3.MaximumContinuationByteLength);
        Assert.True(commitment.Length < 32_768);
        Assert.True(commitment.Length <= CareCodecV5.MaximumContinuationByteLength);
        var continued = context.Continue();
        byte[] bytes = CareCodec.Encode(continued);
        Assert.Equal(bytes, CareCodec.Encode(CareCodec.Decode(bytes, context)));
        var closed = continued.CloseRequest(Disposition(pending, sequence: 52, actor: agreements[0].PatientActorId));
        var recut = CareContinuationContext.Capture(closed);
        Assert.Equal(8, recut.PatientWitnesses.Count);
        Assert.Equal(8, recut.ClosedDefeatWitnesses.Count);
        Assert.Equal(1UL, recut.ArchivedDispositions);
        Assert.Null(recut.Pending);
    }

    [Fact]
    public void Full_disposition_window_cuts_without_losing_cumulative_counts_or_terminal_headroom()
    {
        List<CareAcknowledgment> acknowledgments = [];
        List<CareDisposition> dispositions = [];
        for (ulong index = 0; index < CareSnapshot.MaximumDispositions; index++)
        {
            var acknowledgment = Acknowledgment(11 + index * 3, 2_000 + index, Defeat(10 + index * 3, command: 1_000 + index));
            acknowledgments.Add(acknowledgment);
            dispositions.Add(Disposition(acknowledgment, sequence: 12 + index * 3, command: 4_000 + index));
        }
        var full = CareDispositionFixture.Create(acknowledgments, dispositions: dispositions);
        Assert.Equal(512UL, full.Revision);
        byte[] bytes = CareCodec.Encode(full);
        Assert.True(bytes.Length < CareCodecV5.MaximumEncodedByteLength);
        Assert.Equal(bytes, CareCodec.Encode(CareCodec.Decode(bytes)));
        var extra = Acknowledgment(1_001, 9_000, Defeat(1_000, command: 8_000));
        Assert.Throws<ArgumentException>(() => full.Acknowledge(extra));
        var context = CareContinuationContext.Capture(full);
        var next = context.Continue().Acknowledge(extra).CloseRequest(Disposition(extra, sequence: 1_002, command: 9_001));
        Assert.Equal(257UL, next.TotalDispositions);
        Assert.Equal(257UL, next.TotalAcknowledgments);
        Assert.Equal(0UL, next.TotalTreatments);
        Assert.Single(CareContinuationContext.Capture(next).ClosedDefeatWitnesses);
    }
}

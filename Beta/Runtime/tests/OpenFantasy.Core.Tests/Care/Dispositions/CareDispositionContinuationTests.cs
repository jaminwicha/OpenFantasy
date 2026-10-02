using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.Dispositions.CareDispositionFixture;

namespace OpenFantasy.Core.Tests.Care.Dispositions;

public sealed class CareDispositionContinuationTests
{
    [Fact]
    public void Pending_request_survives_cut_then_closure_and_recut_with_exact_original_identities()
    {
        var acknowledgment = Acknowledgment();
        var pending = CareDispositionFixture.Create([acknowledgment]);
        var first = CareContinuationContext.Capture(pending);
        var carried = first.Continue();
        Assert.True(carried.SupportsDispositions);
        Assert.Same(acknowledgment, carried.Pending);
        Assert.Equal(0UL, first.ArchivedAcknowledgments);
        var disposition = Disposition(acknowledgment);
        var closed = carried.CloseRequest(disposition);
        var second = CareContinuationContext.Capture(closed);
        var continued = second.Continue();
        second.ValidateClosure(closed, continued);
        Assert.Equal(1UL, second.ArchivedAcknowledgments);
        Assert.Equal(1UL, second.ArchivedDispositions);
        Assert.Equal(0UL, second.ArchivedTreatments);
        Assert.Equal(2UL, continued.Revision);
        Assert.Empty(continued.Acknowledgments);
        Assert.Empty(continued.Dispositions);
        Assert.Null(continued.Pending);
        var witness = Assert.Single(second.ClosedDefeatWitnesses);
        Assert.Same(acknowledgment, witness.Acknowledgment);
        Assert.Same(disposition, witness.Disposition);
        Assert.Null(witness.Treatment);
        Assert.Equal(disposition.WorldSequence, witness.TerminalSequence);
        Assert.Empty(second.PatientWitnesses);
        Assert.Throws<InvalidOperationException>(() => CareContinuationContext.Capture(continued));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Same_defeat_cannot_be_reacknowledged_after_cut_or_via_another_agreement(bool otherAgreement)
    {
        var firstAgreement = Agreement();
        var secondAgreement = Agreement(201);
        var acknowledgment = Acknowledgment(agreement: firstAgreement);
        var closed = CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)],
            agreements: [firstAgreement, secondAgreement]);
        var continued = CareContinuationContext.Capture(closed).Continue();
        var repeated = Acknowledgment(21, 2_001, acknowledgment.Defeat,
            agreement: otherAgreement ? secondAgreement : firstAgreement);
        Assert.False(continued.CanAcknowledgeDefeat(Patient, repeated.Defeat.WorldSequence));
        Assert.Throws<ArgumentException>(() => continued.Acknowledge(repeated));
        var actualLater = Acknowledgment(21, 2_002, Defeat(20, command: 1_001), agreement: secondAgreement);
        Assert.NotNull(continued.Acknowledge(actualLater).Pending);
    }

    [Fact]
    public void Later_cancellation_keeps_latest_real_treatment_and_its_resource_revision_guard()
    {
        var first = Acknowledgment();
        var treatment = Treatment(first);
        var treated = CareDispositionFixture.Create([first], [treatment]);
        var boundary = CareContinuationContext.Capture(treated);
        var second = Acknowledgment(21, 2_001, Defeat(20, command: 1_001));
        var disposition = Disposition(second, sequence: 22, command: 4_001);
        var cancelled = boundary.Continue().Acknowledge(second).CloseRequest(disposition);
        var next = CareContinuationContext.Capture(cancelled);
        Assert.Same(treatment, Assert.Single(next.PatientWitnesses).Treatment);
        Assert.Same(disposition, Assert.Single(next.ClosedDefeatWitnesses).Disposition);
        Assert.Equal(2UL, next.ArchivedAcknowledgments);
        Assert.Equal(1UL, next.ArchivedTreatments);
        Assert.Equal(1UL, next.ArchivedDispositions);
        var third = Acknowledgment(31, 2_002, Defeat(30, command: 1_002));
        var pending = next.Continue().Acknowledge(third);
        Assert.Throws<ArgumentException>(() => pending.Treat(Treatment(third, 32, 3_002, treatment.PatientRevisionAfter)));
        var retreated = pending.Treat(Treatment(third, 32, 3_002, treatment.PatientRevisionAfter + 1));
        Assert.Equal(2UL, retreated.TotalTreatments);
        Assert.Equal(1UL, retreated.TotalDispositions);
    }

    [Fact]
    public void Recovery_use_and_revocation_follow_disposition_across_repeated_boundaries()
    {
        var acknowledgment = Acknowledgment();
        var recovery = Recovery.RecoveryCareFixture.Create().Ordinary!;
        var closed = CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)], ordinary: recovery);
        var cut = CareContinuationContext.Capture(closed).Continue();
        var used = cut.WithOrdinary(cut.Ordinary!.RecordUse(Recovery.RecoveryCareFixture.Use()));
        var next = CareContinuationContext.Capture(used).Continue();
        var revoked = next.WithOrdinary(next.Ordinary!.Revoke(Recovery.RecoveryCareFixture.Revoke()));
        var final = CareContinuationContext.Capture(revoked).Continue();
        Assert.Equal(1UL, final.TotalOrdinaryUses);
        Assert.Equal(1UL, final.TotalDispositions);
        Assert.NotNull(final.Ordinary!.Permissions.Single().Revocation);
        Assert.Throws<ArgumentException>(() => final.Ordinary.RecordUse(Recovery.RecoveryCareFixture.Use(sequence: 22, command: 50_002)));
        Assert.Equal(CareCodec.Encode(final), CareCodec.Encode(CareCodec.Decode(CareCodec.Encode(final), final.Continuation)));
    }
}

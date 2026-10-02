using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.Dispositions.CareDispositionFixture;

namespace OpenFantasy.Core.Tests.Care.Dispositions;

public sealed class CareDispositionModelTests
{
    [Theory]
    [InlineData(CareDispositionKind.PatientCancelled)]
    [InlineData(CareDispositionKind.HelperDeclined)]
    public void Exact_patient_or_helper_closes_request_without_treatment(CareDispositionKind kind)
    {
        var acknowledgment = Acknowledgment();
        var pending = CareDispositionFixture.Create([acknowledgment]);
        var disposition = Disposition(acknowledgment, kind);
        var closed = pending.CloseRequest(disposition);
        pending.ValidateSuccessor(closed);
        Assert.Null(closed.Pending);
        Assert.Same(disposition, Assert.Single(closed.Dispositions));
        Assert.Empty(closed.Treatments);
        Assert.Equal(2UL, closed.Revision);
        Assert.Equal(1UL, closed.TotalAcknowledgments);
        Assert.Equal(1UL, closed.TotalDispositions);
        Assert.Equal(0UL, closed.TotalTreatments);
        Assert.Same(pending.Agreements[0], closed.Agreements[0]);
    }

    [Theory]
    [InlineData(CareDispositionKind.PatientCancelled)]
    [InlineData(CareDispositionKind.HelperDeclined)]
    public void Wrong_actor_unknown_request_and_nonpositive_sequence_are_refused(CareDispositionKind kind)
    {
        var acknowledgment = Acknowledgment();
        var state = CareDispositionFixture.Create([acknowledgment]);
        Assert.Throws<ArgumentException>(() => state.CloseRequest(Disposition(acknowledgment, kind, actor: Id(999))));
        Assert.Throws<ArgumentException>(() => state.CloseRequest(Disposition(Acknowledgment(command: 2_001), kind)));
        Assert.Throws<ArgumentException>(() => Disposition(acknowledgment, kind, sequence: 0));
        Assert.Throws<ArgumentException>(() => state.CloseRequest(Disposition(acknowledgment, kind, sequence: acknowledgment.WorldSequence)));
        Assert.Throws<ArgumentException>(() => Disposition(acknowledgment, (CareDispositionKind)3));
        Assert.Throws<ArgumentException>(() => new CareDisposition(Id(1), Id(4_000), acknowledgment.AcknowledgmentId, Patient, kind, 12));
    }

    [Fact]
    public void Treatment_and_disposition_are_exclusive_in_both_commit_orders()
    {
        var acknowledgment = Acknowledgment();
        var pending = CareDispositionFixture.Create([acknowledgment]);
        var treatment = Treatment(acknowledgment);
        var disposition = Disposition(acknowledgment, sequence: 13);
        Assert.Throws<ArgumentException>(() => pending.Treat(treatment).CloseRequest(disposition));
        Assert.Throws<ArgumentException>(() => pending.CloseRequest(disposition).Treat(Treatment(acknowledgment, sequence: 14)));
        Assert.Throws<ArgumentException>(() => pending.CloseRequest(disposition).CloseRequest(Disposition(acknowledgment, sequence: 14, command: 4_001)));
    }

    [Fact]
    public void A_disposition_must_close_the_sole_active_request_in_chronological_order()
    {
        var first = Acknowledgment();
        var second = Acknowledgment(21, 2_001, Defeat(20, command: 1_001));
        Assert.Throws<ArgumentException>(() => CareDispositionFixture.Create([first, second],
            dispositions: [Disposition(first, sequence: 22), Disposition(second, sequence: 23, command: 4_001)]));
        var closed = CareDispositionFixture.Create([first]).CloseRequest(Disposition(first));
        Assert.False(closed.CanAcknowledgeDefeat(Patient, first.Defeat.WorldSequence));
        Assert.Throws<ArgumentException>(() => closed.Acknowledge(Acknowledgment(21, 2_001, first.Defeat)));
        Assert.True(closed.CanAcknowledgeDefeat(Patient, 20));
        Assert.NotNull(closed.Acknowledge(second).Pending);
    }

    [Fact]
    public void Revision_commands_and_world_boundaries_include_dispositions()
    {
        var acknowledgment = Acknowledgment();
        var disposition = Disposition(acknowledgment);
        Assert.Throws<ArgumentException>(() => CareDispositionFixture.Create([acknowledgment], dispositions: [disposition], revision: 1));
        Assert.Throws<ArgumentException>(() => CareDispositionFixture.Create([acknowledgment],
            dispositions: [Disposition(acknowledgment, command: 2_000)]));
        var ordinary = OrdinaryCareFixture.Ordinary([OrdinaryCareFixture.Use(sequence: 12)]);
        Assert.Throws<ArgumentException>(() => CareDispositionFixture.Create([acknowledgment], dispositions: [disposition], ordinary: ordinary));
        Assert.Throws<ArgumentException>(() => CareDispositionFixture.Create([acknowledgment],
            dispositions: [Disposition(acknowledgment, command: 10_000)], ordinary: OrdinaryCareFixture.Ordinary([OrdinaryCareFixture.Use(sequence: 20)])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_interval_ends_at_either_terminal_and_new_operations_append(bool treat)
    {
        var acknowledgment = Acknowledgment();
        var pending = CareDispositionFixture.Create([acknowledgment], ordinary: OrdinaryCareFixture.Ordinary());
        Assert.Throws<ArgumentException>(() => pending.WithOrdinary(pending.Ordinary!.RecordUse(OrdinaryCareFixture.Use())));
        var closed = treat ? pending.Treat(Treatment(acknowledgment, sequence: 15))
            : pending.CloseRequest(Disposition(acknowledgment, sequence: 15));
        Assert.Throws<ArgumentException>(() => closed.WithOrdinary(closed.Ordinary!.RecordUse(OrdinaryCareFixture.Use(sequence: 14))));
        var used = closed.WithOrdinary(closed.Ordinary!.RecordUse(OrdinaryCareFixture.Use(sequence: 20)));
        closed.ValidateSuccessor(used);
        Assert.Equal(1UL, used.TotalOrdinaryUses);
        Assert.Equal(closed.TotalTreatments, used.TotalTreatments);
        Assert.Equal(closed.TotalDispositions, used.TotalDispositions);
        Assert.Throws<ArgumentException>(() => used.Acknowledge(Acknowledgment(19, 2_001, Defeat(18, command: 1_001))));
    }

    [Fact]
    public void Accepted_disposition_cannot_be_rewritten_or_removed_by_a_successor()
    {
        var acknowledgment = Acknowledgment();
        var closed = CareDispositionFixture.Create([acknowledgment], dispositions: [Disposition(acknowledgment)], ordinary: OrdinaryCareFixture.Ordinary());
        var ordinary = closed.Ordinary!.RecordUse(OrdinaryCareFixture.Use());
        var rewritten = CareDispositionFixture.Create([acknowledgment],
            dispositions: [Disposition(acknowledgment, CareDispositionKind.HelperDeclined)], ordinary: ordinary);
        Assert.Throws<ArgumentException>(() => closed.ValidateSuccessor(rewritten));
        var replaced = CareDispositionFixture.Create([acknowledgment], [Treatment(acknowledgment)], ordinary: ordinary);
        Assert.Throws<ArgumentException>(() => closed.ValidateSuccessor(replaced));
    }
}

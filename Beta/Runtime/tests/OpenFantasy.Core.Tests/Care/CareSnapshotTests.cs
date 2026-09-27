using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class CareSnapshotTests
{
    [Fact]
    public void Completed_history_and_latest_obligation_are_canonical_and_independent_of_input_order()
    {
        var completed = CareModelFixture.History(2);
        var pending = Acknowledgment(21, 2_002, Defeat(20, 11, 1_002));
        CareAcknowledgment[] acknowledgments = [.. completed.Acknowledgments, pending];
        CareTreatment[] treatments = completed.Treatments.ToArray();
        var state = Create(acknowledgments, treatments);
        byte[] bytes = CareCodecV1.Encode(state);
        var reversed = Create(acknowledgments.Reverse(), treatments.Reverse());
        Assert.Equal(bytes, CareCodecV1.Encode(reversed));
        Assert.Equal(pending.AcknowledgmentId, state.Pending!.AcknowledgmentId);
        Assert.Equal(5UL, state.Revision);
        acknowledgments[0] = pending;
        treatments[0] = treatments[1];
        Assert.Equal(bytes, CareCodecV1.Encode(state));
        Assert.Null(completed.Pending);
    }

    [Fact]
    public void Overlapping_obligations_reject_even_when_every_acknowledgment_has_a_treatment()
    {
        var first = Acknowledgment();
        var second = Acknowledgment(14, 2_001, Defeat(13, 9, 1_001));
        var firstTreatment = Treatment(first, sequence: 15);
        var secondTreatment = Treatment(second, 16, 3_001, patientRevision: 5);
        Assert.Throws<ArgumentException>(() => Create([first, second], [firstTreatment, secondTreatment]));
    }

    [Fact]
    public void Two_pending_acknowledgments_and_treatment_of_a_different_obligation_reject()
    {
        var first = Acknowledgment();
        var second = Acknowledgment(14, 2_001, Defeat(13, 9, 1_001));
        Assert.Throws<ArgumentException>(() => Create([first, second]));
        Assert.Throws<ArgumentException>(() => Create([first, second], [Treatment(second, 15, 3_001)]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Relabeling_the_same_lethal_boundary_or_command_as_another_encounter_rejects(bool changeSequence)
    {
        var first = Acknowledgment();
        var reused = changeSequence ? Defeat(20, 9, 1_000) : Defeat();
        var second = Acknowledgment(21, 2_001, reused, encounter: Id(91));
        Assert.Throws<ArgumentException>(() => Create([first, second],
            [Treatment(first), Treatment(second, 22, 3_001, patientRevision: 5)]));
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(9UL)]
    [InlineData(ulong.MaxValue)]
    public void Treatment_requires_the_exact_next_timing_revision(ulong timingRevision)
    {
        var acknowledgment = Acknowledgment();
        Assert.Throws<ArgumentException>(() => Create([acknowledgment],
            [Treatment(acknowledgment, timingRevision: timingRevision)]));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void Another_treatment_requires_patient_revision_after_prior_healing_and_new_damage(ulong patientRevision)
    {
        var first = Acknowledgment();
        var second = Acknowledgment(21, 2_001, Defeat(20, 9, 1_001));
        Assert.Throws<ArgumentException>(() => Create([first, second],
            [Treatment(first), Treatment(second, 22, 3_001, patientRevision)]));
    }

    [Theory]
    [InlineData(9UL)]
    [InlineData(12UL)]
    public void Another_acknowledgment_cannot_reuse_a_defeat_before_or_at_the_previous_treatment(ulong defeatSequence)
    {
        var first = Acknowledgment();
        var second = Acknowledgment(21, 2_001, Defeat(defeatSequence, 9, 1_001));
        Assert.Throws<ArgumentException>(() => Create([first, second], [Treatment(first)]));
    }

    [Fact]
    public void One_acknowledgment_cannot_have_two_distinct_treatment_commands()
    {
        var acknowledgment = Acknowledgment();
        Assert.Throws<ArgumentException>(() => Create([acknowledgment],
            [Treatment(acknowledgment), Treatment(acknowledgment, 13, 3_001)]));
    }

    [Theory]
    [InlineData(10UL)]
    [InlineData(11UL)]
    public void Treatment_must_follow_the_acknowledgment_in_world_sequence(ulong sequence)
    {
        var acknowledgment = Acknowledgment();
        Assert.Throws<ArgumentException>(() => Create([acknowledgment], [Treatment(acknowledgment, sequence)]));
    }

    [Theory]
    [InlineData(100, 39)]
    [InlineData(100, 41)]
    [InlineData(20, 19)]
    public void Positive_in_range_hp_is_insufficient_without_exact_remedy_capping(int maximumHp, int hpAfter)
    {
        var acknowledgment = Acknowledgment();
        Assert.Throws<ArgumentException>(() => Create([acknowledgment],
            [Treatment(acknowledgment, maximumHp: maximumHp, hpAfter: hpAfter)]));
    }

    [Theory]
    [InlineData(100, 40)]
    [InlineData(20, 20)]
    public void Treatment_retains_the_exact_remedy_or_patient_maximum_cap(int maximumHp, int hpAfter)
    {
        var acknowledgment = Acknowledgment();
        var state = Create([acknowledgment], [Treatment(acknowledgment, maximumHp: maximumHp, hpAfter: hpAfter)]);
        Assert.Equal(hpAfter, Assert.Single(state.Treatments).HpAfter);
        Assert.Null(state.Pending);
    }

    [Fact]
    public void Unknown_or_mismatched_permission_patient_and_revision_reject()
    {
        var acknowledgment = Acknowledgment();
        var otherPatient = Acknowledgment(defeat: Defeat(patient: Id(99)));
        var unknownAgreement = Acknowledgment(agreement: Agreement(201));
        Assert.Throws<ArgumentException>(() => Create([otherPatient]));
        Assert.Throws<ArgumentException>(() => Create([unknownAgreement]));
        Assert.Throws<ArgumentException>(() => Create(treatments: [Treatment(acknowledgment)]));
        Assert.Throws<ArgumentException>(() => Create([acknowledgment], revision: 0));
        Assert.Throws<ArgumentException>(() => Create([acknowledgment], revision: 2));
        Assert.Throws<ArgumentException>(() => Create([acknowledgment],
            [Treatment(acknowledgment, command: acknowledgment.CommandId.Low)]));
    }
}

using System.Buffers.Binary;
using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class CareCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Canonical_roundtrip_retains_exact_catalogs_lethal_result_and_resource_witness(bool completed)
    {
        var acknowledgment = Acknowledgment(defeat: Defeat(cancelled: [Id(7_001), Id(7_000)]));
        var state = Create([acknowledgment], completed ? [Treatment(acknowledgment)] : []);
        byte[] bytes = CareCodecV1.Encode(state);
        var restored = CareCodecV1.Decode(bytes);
        Assert.Equal(bytes, CareCodecV1.Encode(restored));
        Assert.Equal(state.ContentHash, restored.ContentHash);
        var actual = Assert.Single(restored.Acknowledgments);
        Assert.Equal((acknowledgment.AcknowledgmentId, acknowledgment.CommandId, acknowledgment.EncounterId,
            acknowledgment.AgreementId, acknowledgment.WorldSequence, ActionHash, TimingHash),
            (actual.AcknowledgmentId, actual.CommandId, actual.EncounterId, actual.AgreementId,
                actual.WorldSequence, actual.ActionHash, actual.TimingHash));
        Assert.Equal((10UL, 7UL, 40U, 0U, Id(1_000)),
            (actual.Defeat.WorldSequence, actual.Defeat.TimingRevision, actual.Defeat.TargetHpBefore,
                actual.Defeat.TargetHpAfter, actual.Defeat.DecisionCommandId));
        Assert.Equal(new[] { Id(7_000), Id(7_001) }, actual.Defeat.CancelledDecisionIds);
        Assert.Equal(state.GetRemedy(Id(100)).Version, restored.GetRemedy(Id(100)).Version);
        Assert.Equal(state.GetAgreement(Id(200)).Version, restored.GetAgreement(Id(200)).Version);
        if (completed)
        {
            var treatment = Assert.Single(restored.Treatments);
            Assert.Equal((12UL, 8UL, 3UL, 4UL, 100, 40, 80U, 125UL),
                (treatment.WorldSequence, treatment.TimingRevision, treatment.PatientRevisionBefore,
                    treatment.PatientRevisionAfter, treatment.MaximumHp, treatment.HpAfter,
                    treatment.HelperNormalizedSpeed, treatment.HelperDelay));
            Assert.Equal(EntityId.DeriveSpawned(treatment.CommandId, 0), treatment.ConsumptionId);
            Assert.Equal(EntityId.DeriveSpawned(treatment.CommandId, 1), treatment.TreatmentId);
            Assert.Null(restored.Pending);
        }
        else Assert.Equal(acknowledgment.AcknowledgmentId, restored.Pending!.AcknowledgmentId);
    }

    [Fact]
    public void Complete_maximum_catalogs_and_history_with_full_cancellations_fit_the_reserved_archive_bound()
    {
        var history = CareModelFixture.History(CareSnapshot.MaximumTreatments, maximumCancelled: true);
        RestorativeRemedyDefinition[] remedies = [Remedy(), .. Enumerable.Range(0, CareSnapshot.MaximumRemedies - 1)
            .Select(index => Remedy(110 + (ulong)index))];
        CareAgreement[] agreements = [Agreement(), .. remedies.Skip(1)
            .Select((remedy, index) => Agreement(210 + (ulong)index, remedy: remedy))];
        var state = Create(history.Acknowledgments, history.Treatments, remedies, agreements);
        byte[] bytes = CareCodecV1.Encode(state);
        Assert.True(bytes.Length < CareCodecV1.MaximumEncodedByteLength);
        var restored = CareCodecV1.Decode(bytes);
        Assert.Equal(CareSnapshot.MaximumRemedies, restored.Remedies.Count);
        Assert.Equal(CareSnapshot.MaximumAgreements, restored.Agreements.Count);
        Assert.Equal(CareSnapshot.MaximumAcknowledgments, restored.Acknowledgments.Count);
        Assert.Equal(CareSnapshot.MaximumTreatments, restored.Treatments.Count);
        Assert.All(restored.Acknowledgments, value => Assert.Equal(64, value.Defeat.CancelledDecisionIds.Count));
        Assert.Equal(bytes, CareCodecV1.Encode(restored));
        var next = Acknowledgment(1_001, 2_999, Defeat(1_000, 600, 1_999));
        Assert.Throws<ArgumentException>(() => Create(state.Acknowledgments.Append(next), state.Treatments));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("revision")]
    [InlineData("remedy-pin")]
    [InlineData("acknowledgment-id")]
    [InlineData("wrong-patient")]
    [InlineData("nonlethal-result")]
    [InlineData("consumption-id")]
    [InlineData("treatment-id")]
    [InlineData("timing-gap")]
    [InlineData("timing-overflow")]
    [InlineData("hp-cap")]
    [InlineData("helper-debt")]
    [InlineData("resource-overflow")]
    public void Inconsistent_canonical_fields_fail_closed_on_restore(string corruption)
    {
        var acknowledgment = Acknowledgment();
        var root = Fields(CareCodecV1.Encode(Create([acknowledgment], [Treatment(acknowledgment)])));
        var remedies = Fields(root[5]);
        var remedy = Fields(remedies[1]);
        var acknowledgments = Fields(root[7]);
        var accepted = Fields(acknowledgments[1]);
        var defeat = Fields(accepted[8]);
        var treatments = Fields(root[8]);
        var treatment = Fields(treatments[1]);
        switch (corruption)
        {
            case "version": root[1] = Number(2); break;
            case "revision": root[4] = Number(1); break;
            case "remedy-pin": remedy[3][0] ^= 1; break;
            case "acknowledgment-id": accepted[1] = Id(99).ToByteArray(); break;
            case "wrong-patient": defeat[5] = Id(99).ToByteArray(); break;
            case "nonlethal-result": defeat[8] = Number(39); defeat[10] = Number(1); break;
            case "consumption-id": treatment[4] = Id(99).ToByteArray(); break;
            case "treatment-id": treatment[1] = Id(99).ToByteArray(); break;
            case "timing-gap": treatment[6] = Number(9); break;
            case "timing-overflow": defeat[2] = Number(ulong.MaxValue); break;
            case "hp-cap": treatment[9] = Number(39); break;
            case "helper-debt": treatment[11] = Number(124); break;
            case "resource-overflow": treatment[7] = Number(ulong.MaxValue); break;
            default: throw new InvalidOperationException("Unknown care archive mutation.");
        }
        remedies[1] = Frame(remedy); root[5] = Frame(remedies);
        accepted[8] = Frame(defeat); acknowledgments[1] = Frame(accepted); root[7] = Frame(acknowledgments);
        treatments[1] = Frame(treatment); root[8] = Frame(treatments);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame(root)));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void Reordered_or_duplicate_history_rows_are_not_canonical(int field)
    {
        var root = Fields(CareCodecV1.Encode(CareModelFixture.History(2)));
        var rows = Fields(root[field]);
        root[field] = Frame([rows[0], rows[2], rows[1]]);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame(root)));
        root[field] = Frame([rows[0], rows[1], rows[1]]);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame(root)));
    }

    [Fact]
    public void Archive_cannot_move_a_completed_treatment_after_the_next_acknowledgment()
    {
        var state = CareModelFixture.History(2);
        var first = state.Treatments.MinBy(value => value.WorldSequence)!;
        var root = Fields(CareCodecV1.Encode(state));
        var rows = Fields(root[8]);
        int index = rows.FindIndex(1, value => EntityId.FromBytes(Fields(value)[1]) == first.TreatmentId);
        rows[index] = Replace(rows[index], 5, Number(16));
        root[8] = Frame(rows);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame(root)));
    }

    [Fact]
    public void Malformed_frame_versions_lengths_counts_and_trailing_bytes_refuse()
    {
        byte[] bytes = CareCodecV1.Encode(CareModelFixture.History(1));
        foreach (int length in new[] { 0, 11, 12, 50, bytes.Length / 2, bytes.Length - 1 })
            Assert.Throws<FormatException>(() => CareCodecV1.Decode(bytes[..length]));
        Assert.Throws<FormatException>(() => CareCodecV1.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(new byte[CareCodecV1.MaximumEncodedByteLength + 1]));
        byte[] wrongFrameVersion = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(wrongFrameVersion.AsSpan(4), 2);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(wrongFrameVersion));
        byte[] missingFields = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(missingFields.AsSpan(8), 8);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(missingFields));
        var root = Fields(bytes);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame([.. root, []])));
        var acknowledgments = Fields(root[7]);
        root[7] = Frame([acknowledgments[0], .. Enumerable.Repeat(acknowledgments[1], CareSnapshot.MaximumAcknowledgments + 1)]);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame(root)));
        root[7] = Frame([acknowledgments[0], new byte[2_561]]);
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(Frame(root)));
    }
}

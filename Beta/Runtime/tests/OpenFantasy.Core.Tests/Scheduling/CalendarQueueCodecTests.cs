using OpenFantasy.Core.Scheduling;

namespace OpenFantasy.Core.Tests.Scheduling;

public sealed class CalendarQueueCodecTests
{
    [Fact]
    public void CanonicalSnapshotHashAndOrderingMatchIndependentPythonReference()
    {
        DueWorkQueueSnapshot snapshot = SchedulingFixture.Create();
        Assert.Equal(SchedulingFixture.Bytes("snapshotHex"), CalendarQueueCodecV1.Encode(snapshot));
        Assert.Equal(SchedulingFixture.Read().GetProperty("snapshotSha256").GetString(), snapshot.ComputeSnapshotHash());
        Assert.Equal(SchedulingFixture.Read().GetProperty("canonicalEventOrder").EnumerateArray()
            .Select(value => value.GetString()), snapshot.Entries.Select(entry => entry.EventId.ToString()));
        Assert.Equal(SchedulingFixture.Bytes("snapshotHex"), CalendarQueueCodecV1.Encode(
            CalendarQueueCodecV1.Decode(SchedulingFixture.Bytes("snapshotHex"))));
    }

    [Fact]
    public void SelectionAndCandidateReplacementMatchIndependentReference()
    {
        DueWorkQueueSnapshot snapshot = SchedulingFixture.Create();
        DueWorkSelection selection = snapshot.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.StateTransition, 2);
        var expected = SchedulingFixture.Read().GetProperty("selection");
        Assert.Equal(expected.GetProperty("selectedEventIds").EnumerateArray().Select(value => value.GetString()),
            selection.SelectedWork.Select(entry => entry.EventId.ToString()));
        Assert.Equal(expected.GetProperty("pendingCount").GetInt32(), selection.PendingCount);
        Assert.Equal(expected.GetProperty("hasMoreEligibleWork").GetBoolean(), selection.HasMoreEligibleWork);
        DueWorkQueueSnapshot replacement = snapshot.ApplySelection(selection);
        Assert.Equal(SchedulingFixture.Bytes("replacementHex"), CalendarQueueCodecV1.Encode(replacement));
        Assert.Equal(SchedulingFixture.Read().GetProperty("replacementSha256").GetString(), replacement.ComputeSnapshotHash());
        Assert.Equal(SchedulingFixture.Bytes("snapshotHex"), CalendarQueueCodecV1.Encode(snapshot));
    }

    [Fact]
    public void IndependentMalformedVectorsRejectWithSpecificDiagnostics()
    {
        foreach (var value in SchedulingFixture.Read().GetProperty("invalidSnapshots").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(value.GetProperty("hex").GetString()!);
            var failure = Assert.Throws<SchedulingFormatException>(() => CalendarQueueCodecV1.Decode(bytes));
            Assert.True(failure.Failure.ToString() == value.GetProperty("failure").GetString(),
                $"{value.GetProperty("name").GetString()}: {failure.Failure}");
        }
    }

    [Fact]
    public void EveryTruncatedSnapshotPrefixAndOversizedFrameReject()
    {
        byte[] bytes = SchedulingFixture.Bytes("snapshotHex");
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.Throws<SchedulingFormatException>(() => CalendarQueueCodecV1.Decode(prefix));
        }
        var failure = Assert.Throws<SchedulingFormatException>(() => CalendarQueueCodecV1.Decode(
            new byte[CalendarQueueCodecV1.MaximumSnapshotByteLength + 1]));
        Assert.Equal(SchedulingFormatFailure.LimitExceeded, failure.Failure);
    }

    [Fact]
    public void EmptySnapshotHasCanonicalBytesAndNoDueWork()
    {
        DueWorkQueueSnapshot empty = new(SchedulingFixture.Scope(), 0, []);
        Assert.Equal(SchedulingFixture.Bytes("emptySnapshotHex"), CalendarQueueCodecV1.Encode(empty));
        DueWorkQueueSnapshot restored = CalendarQueueCodecV1.Decode(CalendarQueueCodecV1.Encode(empty));
        DueWorkSelection selection = restored.SelectDue(SchedulingFixture.Cutoff(), ScheduledWorkPhase.Planning, 1);
        Assert.Empty(selection.SelectedWork);
        Assert.False(selection.HasMoreEligibleWork);
        Assert.Equal(0, selection.PendingCount);
        Assert.Same(restored, restored.ApplySelection(selection));
    }

    [Fact]
    public void DecodingAndEncodingCannotExposeMutableBackingData()
    {
        byte[] bytes = SchedulingFixture.Bytes("snapshotHex");
        DueWorkQueueSnapshot restored = CalendarQueueCodecV1.Decode(bytes);
        Array.Fill(bytes, (byte)0xff);
        byte[] output = CalendarQueueCodecV1.Encode(restored);
        Array.Fill(output, (byte)0);
        Assert.Equal(SchedulingFixture.Bytes("snapshotHex"), CalendarQueueCodecV1.Encode(restored));
        Assert.Throws<NotSupportedException>(() => ((IList<ScheduledWorkEntry>)restored.Entries).Clear());
    }
}

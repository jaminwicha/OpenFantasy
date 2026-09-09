using System.Buffers.Binary;

namespace OpenFantasy.Core.Scheduling;

/// <summary>Canonical bounded persistence bytes for pending calendar work, without storage behavior.</summary>
public static class CalendarQueueCodecV1
{
    public const int MaximumSnapshotByteLength = 256 * 1024;
    private const int MaximumEntryByteLength = 256;
    private const ulong ContractVersion = 1;

    public static byte[] Encode(DueWorkQueueSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        List<ReadOnlyMemory<byte>> fields =
        [
            CanonicalEncodingV1.Unsigned64(ContractVersion),
            snapshot.Scope.WorldId.ToByteArray(), snapshot.Scope.BranchId.ToByteArray(),
            snapshot.Scope.ClockId.ToByteArray(), CanonicalEncodingV1.Unsigned64(snapshot.Revision)
        ];
        foreach (ScheduledWorkEntry entry in snapshot.Entries)
        {
            fields.Add(EncodeEntry(entry));
        }
        return CanonicalEncodingV1.Frame("scheduler.calendar-queue", fields.ToArray());
    }

    public static DueWorkQueueSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        CanonicalSchedulingReader reader = new(bytes, MaximumSnapshotByteLength,
            DueWorkQueueSnapshot.MaximumEntryCount + 6);
        reader.ExpectTag("scheduler.calendar-queue"u8);
        reader.ExpectVersion(ContractVersion);
        CalendarQueueScope scope = new(reader.ReadIdentity(), reader.ReadIdentity(), reader.ReadIdentity());
        ulong revision = reader.ReadUnsigned64();
        List<ScheduledWorkEntry> entries = [];
        HashSet<EntityId> identities = [];
        while (reader.RemainingFields > 0)
        {
            ScheduledWorkEntry entry = DecodeEntry(reader.ReadField(MaximumEntryByteLength));
            if (!identities.Add(entry.EventId) || entries.Count > 0 && entries[^1].CompareTo(entry) >= 0)
            {
                throw CanonicalSchedulingReader.Failure(SchedulingFormatFailure.NonCanonical,
                    "Pending work must be strictly sorted with unique event identities.");
            }
            entries.Add(entry);
        }
        reader.Complete();
        return new DueWorkQueueSnapshot(scope, revision, entries);
    }

    private static byte[] EncodeEntry(ScheduledWorkEntry entry) => CanonicalEncodingV1.Frame(
        "scheduler.calendar-work", CanonicalEncodingV1.Unsigned64(ContractVersion),
        entry.EventId.ToByteArray(), Signed64(entry.DueAt.Days),
        CanonicalEncodingV1.Unsigned64((ulong)entry.DueAt.MillisecondsWithinDay),
        CanonicalEncodingV1.Unsigned64((ulong)entry.Phase), entry.WorkDomainId.ToByteArray(),
        entry.ActorId.ToByteArray(), entry.WorkItemId.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(entry.ContinuationOrdinal));

    private static ScheduledWorkEntry DecodeEntry(ReadOnlySpan<byte> bytes)
    {
        CanonicalSchedulingReader reader = new(bytes, MaximumEntryByteLength, 11);
        reader.ExpectTag("scheduler.calendar-work"u8);
        reader.ExpectVersion(ContractVersion);
        EntityId eventId = reader.ReadIdentity();
        long days = reader.ReadSigned64();
        ulong milliseconds = reader.ReadUnsigned64();
        if (milliseconds >= WorldInstant.MillisecondsPerDay)
        {
            throw CanonicalSchedulingReader.Failure(SchedulingFormatFailure.InvalidValue,
                "Calendar milliseconds are outside their canonical day range.");
        }
        ulong phase = reader.ReadUnsigned64();
        if (phase is not (ulong)ScheduledWorkPhase.StateTransition and not (ulong)ScheduledWorkPhase.Observation
            and not (ulong)ScheduledWorkPhase.Planning and not (ulong)ScheduledWorkPhase.FollowUp)
        {
            throw CanonicalSchedulingReader.Failure(SchedulingFormatFailure.UnknownType,
                "The scheduled work phase is unsupported.");
        }
        EntityId domainId = reader.ReadIdentity();
        EntityId actorId = reader.ReadIdentity();
        EntityId workItemId = reader.ReadIdentity();
        ulong continuationOrdinal = reader.ReadUnsigned64();
        reader.Complete();
        return new ScheduledWorkEntry(eventId, new WorldInstant(days, (int)milliseconds),
            (ScheduledWorkPhase)phase, domainId, actorId, workItemId, continuationOrdinal);
    }

    private static byte[] Signed64(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}

namespace OpenFantasy.Core.Care;

public sealed partial class CareSnapshot
{
    /// <summary>Continued owner: retained records after the exact closed boundary named by its context.</summary>
    internal CareSnapshot(CareContinuationContext continuation, ulong revision,
        IEnumerable<RestorativeRemedyDefinition> remedies, IEnumerable<CareAgreement> agreements,
        IEnumerable<CareAcknowledgment> acknowledgments, IEnumerable<CareTreatment> treatments,
        OrdinaryCareSnapshot? ordinary)
        : this(continuation ?? throw new ArgumentNullException(nameof(continuation)), continuation.WorldId,
            continuation.BranchId, revision, remedies, agreements, acknowledgments, treatments, ordinary, continuation.SupportsDispositions)
    {
    }

    /// <summary>Null for an inline owner whose retained records are its complete history.</summary>
    public CareContinuationContext? Continuation { get; }
    public ulong TotalAcknowledgments => checked((Continuation?.ArchivedAcknowledgments ?? 0) + (ulong)Acknowledgments.Count);
    public ulong TotalTreatments => checked((Continuation?.ArchivedTreatments ?? 0) + (ulong)Treatments.Count);
    public ulong TotalDispositions => checked((Continuation?.ArchivedDispositions ?? 0) + (ulong)Dispositions.Count);
    public ulong TotalOrdinaryUses => Ordinary?.TotalUses ?? 0;

    private void ValidateContinuation(OrdinaryCareSnapshot? ordinary)
    {
        if (Continuation is not { } continuation)
        {
            if (ordinary?.Continuation is not null)
                throw new ArgumentException("Inline care cannot adopt an ordinary owner continued from a closed boundary.");
            return;
        }
        if (SupportsDispositions != continuation.SupportsDispositions || (ordinary is not null) != continuation.SupportsOrdinary
            || ordinary is not null && !continuation.Matches(ordinary.Continuation))
            throw new ArgumentException("Continued care and its ordinary owner share one exact closed boundary.");
        continuation.RequireCatalogs(Remedies, Agreements);
        bool carried = continuation.Pending is null;
        foreach (var acknowledgment in Acknowledgments)
        {
            if (continuation.Pending is { } pending && pending.AcknowledgmentId == acknowledgment.AcknowledgmentId)
            {
                carried = CareCodecV1.EncodeAcknowledgment(pending).AsSpan()
                    .SequenceEqual(CareCodecV1.EncodeAcknowledgment(acknowledgment));
                if (!carried) break;
            }
            else if (continuation.Frontier.Closes(acknowledgment.WorldSequence))
                throw new ArgumentException("Only the carried pending acknowledgment may predate the closed boundary.");
        }
        if (!carried || Treatments.Any(value => continuation.Frontier.Closes(value.WorldSequence))
            || Dispositions.Any(value => continuation.Frontier.Closes(value.WorldSequence)))
            throw new ArgumentException("Continued care keeps its exact pending acknowledgment and terminal records follow the closed boundary.");
    }
}

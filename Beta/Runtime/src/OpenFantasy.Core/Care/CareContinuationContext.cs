using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Bounded immutable frontier of one closed care boundary, captured from the actual closing owner. It keeps
/// exact catalog and permission references, the sole pending acknowledgment, cumulative archived counters,
/// a position watermark, the latest completed treatment and an optional exact closed request per patient.
/// It retains no closing owner, earlier context or complete closed history, and it cannot prove historical identity uniqueness: the world's
/// permanent identity index and the host's authenticated closing world remain separate obligations.
/// </summary>
public sealed class CareContinuationContext
{
    public const int MaximumPatientWitnesses = CareSnapshot.MaximumAgreements;

    private readonly byte[] _commitment;

    private CareContinuationContext(CareSnapshot closing)
    {
        var previous = closing.Continuation;
        WorldId = closing.WorldId;
        BranchId = closing.BranchId;
        Ordinal = checked((previous?.Ordinal ?? 0) + 1);
        ClosingCareHash = closing.ContentHash;
        ClosingRevision = closing.Revision;
        Pending = closing.Pending;
        // The pending acknowledgment stays an active retained record, so it is not archived or counted twice.
        ArchivedAcknowledgments = checked(closing.TotalAcknowledgments - (Pending is null ? 0UL : 1UL));
        ArchivedTreatments = closing.TotalTreatments;
        ArchivedDispositions = closing.TotalDispositions;
        SupportsDispositions = closing.SupportsDispositions;
        ArchivedOrdinaryUses = closing.TotalOrdinaryUses;
        if (ArchivedAcknowledgments != checked(ArchivedTreatments + ArchivedDispositions))
            throw new ArgumentException("A closed care boundary archives only requests with exactly one terminal treatment or disposition.");
        Frontier = new(LatestSequence(closing, previous?.Frontier.WorldSequence ?? 0));
        Remedies = closing.Remedies;
        Agreements = closing.Agreements;
        Definitions = closing.Ordinary?.Definitions;
        Permissions = closing.Ordinary?.Permissions;
        PatientWitnesses = LatestTreatments(closing, previous);
        ClosedDefeatWitnesses = LatestClosedDefeats(closing, previous);
        _commitment = SupportsDispositions ? CareCodecV5.ComputeContinuation(this) : CareCodecV3.ComputeContinuation(this);
        ContentHash = ContentHash.Compute(_commitment);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    /// <summary>One for the first closure of an inline owner, then one more per later closure.</summary>
    public ulong Ordinal { get; }
    public ContentHash ClosingCareHash { get; }
    public ulong ClosingRevision { get; }
    public ulong ArchivedAcknowledgments { get; }
    public ulong ArchivedTreatments { get; }
    public ulong ArchivedDispositions { get; }
    public bool SupportsDispositions { get; }
    public ReadOnlyCollection<CareClosedDefeatWitness> ClosedDefeatWitnesses { get; }
    public ulong ArchivedOrdinaryUses { get; }
    public CareContinuationPosition Frontier { get; }
    public bool SupportsOrdinary => Definitions is not null;
    public CareAcknowledgment? Pending { get; }
    public ReadOnlyCollection<CarePatientWitness> PatientWitnesses { get; }
    public ContentHash ContentHash { get; }

    internal ReadOnlyCollection<RestorativeRemedyDefinition> Remedies { get; }
    internal ReadOnlyCollection<CareAgreement> Agreements { get; }
    internal ReadOnlyCollection<RestorativeUseDefinition>? Definitions { get; }
    internal ReadOnlyCollection<CarePermission>? Permissions { get; }
    internal ReadOnlyMemory<byte> Commitment => _commitment;

    /// <summary>
    /// No-progress rule: every accepted operation advances the cumulative revision by exactly one, so a
    /// continued owner whose revision equals its context's closing revision is semantically that context's
    /// own continuation and closing it would archive nothing. An inline owner can always be closed, even
    /// when empty; a coordinated closure that finds care without progress keeps the existing context.
    /// </summary>
    public static bool CanCapture(CareSnapshot closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        return closing.Continuation is not { } previous || closing.Revision > previous.ClosingRevision;
    }

    public static CareContinuationContext Capture(CareSnapshot closing)
    {
        if (!CanCapture(closing))
            throw new InvalidOperationException("A continued care owner without any accepted operation cannot be closed again.");
        return new(closing);
    }

    /// <summary>The closed boundary's untouched successor: same cumulative revision, no archived record.</summary>
    public CareSnapshot Continue()
    {
        CareAcknowledgment[] carried = Pending is null ? [] : [Pending];
        var ordinary = Definitions is null ? null : new OrdinaryCareSnapshot(Definitions, Permissions!, [], this);
        return new(this, ClosingRevision, Remedies, Agreements, carried, [], ordinary);
    }

    /// <summary>Explicit closure transition, separate from the append-only successor rule.</summary>
    public void ValidateClosure(CareSnapshot closing, CareSnapshot continued)
    {
        ArgumentNullException.ThrowIfNull(continued);
        if (Capture(closing).ContentHash != ContentHash || !Matches(continued.Continuation)
            || continued.ContentHash != Continue().ContentHash)
            throw new ArgumentException("A care closure continues exactly the captured boundary without any other change.");
    }

    public byte[] ToCommitment() => (byte[])_commitment.Clone();

    internal bool Matches(CareContinuationContext? other) => other is not null && other.ContentHash == ContentHash;

    internal static bool Same(CareContinuationContext? left, CareContinuationContext? right) =>
        left is null ? right is null : left.Matches(right);

    internal void RequireCatalogs(IReadOnlyList<RestorativeRemedyDefinition> remedies, IReadOnlyList<CareAgreement> agreements)
    {
        if (!Exact(Remedies, remedies, CareCodecV1.EncodeRemedy) || !Exact(Agreements, agreements, CareCodecV1.EncodeAgreement))
            throw new ArgumentException("Continued care retains the exact remedies and agreements of its closed boundary.");
    }

    internal void RequireOrdinary(IReadOnlyList<RestorativeUseDefinition> definitions,
        IReadOnlyList<CarePermission> permissions, IReadOnlyList<RestorativeUse> uses)
    {
        if (Definitions is null || Permissions is null
            || !Exact(Definitions, definitions, CareCodec.EncodeUseDefinition)
            || !Exact(Permissions, permissions, CareCodecV2.EncodePermissionGrant))
            throw new ArgumentException("Continued ordinary care retains the exact definitions and grants of its closed boundary.");
        for (int index = 0; index < Permissions.Count; index++)
        {
            var closed = Permissions[index].Revocation;
            var current = permissions[index].Revocation;
            bool rewritten = closed is not null && (current is null
                || !CareCodecV2.EncodeRevocation(closed).AsSpan().SequenceEqual(CareCodecV2.EncodeRevocation(current)));
            if (rewritten || closed is null && current is not null && Frontier.Closes(current.WorldSequence))
                throw new ArgumentException("A closed revocation persists exactly and a later one must follow the closed boundary.");
        }
        if (uses.Any(value => Frontier.Closes(value.WorldSequence)))
            throw new ArgumentException("A retained ordinary use must follow the closed boundary.");
    }

    private static bool Exact<T>(IReadOnlyList<T> closed, IReadOnlyList<T> current, Func<T, byte[]> encode) where T : class
    {
        if (closed.Count != current.Count) return false;
        for (int index = 0; index < closed.Count; index++)
        {
            if (!ReferenceEquals(closed[index], current[index])
                && !encode(closed[index]).AsSpan().SequenceEqual(encode(current[index])))
                return false;
        }
        return true;
    }

    private static ulong LatestSequence(CareSnapshot closing, ulong frontier)
    {
        var sequences = closing.Acknowledgments.Select(value => value.WorldSequence)
            .Concat(closing.Treatments.Select(value => value.WorldSequence))
            .Concat(closing.Dispositions.Select(value => value.WorldSequence));
        if (closing.Ordinary is { } ordinary)
        {
            sequences = sequences.Concat(ordinary.Uses.Select(value => value.WorldSequence))
                .Concat(ordinary.Permissions.Select(value => value.Revocation).OfType<CarePermissionRevocation>()
                    .Select(value => value.WorldSequence));
        }
        return sequences.Append(frontier).Max();
    }

    private static ReadOnlyCollection<CareClosedDefeatWitness> LatestClosedDefeats(CareSnapshot closing,
        CareContinuationContext? previous)
    {
        SortedDictionary<EntityId, CareClosedDefeatWitness> latest = [];
        if (!closing.SupportsDispositions) return Array.AsReadOnly(Array.Empty<CareClosedDefeatWitness>());
        if (previous is not null)
            foreach (var witness in previous.ClosedDefeatWitnesses) latest[witness.PatientActorId] = witness;
        foreach (var acknowledgment in closing.Acknowledgments)
        {
            var treatment = closing.Treatments.SingleOrDefault(value => value.AcknowledgmentId == acknowledgment.AcknowledgmentId);
            var disposition = closing.Dispositions.SingleOrDefault(value => value.AcknowledgmentId == acknowledgment.AcknowledgmentId);
            if (treatment is null && disposition is null) continue;
            EntityId patient = closing.GetAgreement(acknowledgment.AgreementId).PatientActorId;
            var witness = new CareClosedDefeatWitness(patient, acknowledgment, treatment, disposition);
            if (!latest.TryGetValue(patient, out var prior) || witness.TerminalSequence > prior.TerminalSequence)
                latest[patient] = witness;
        }
        return Array.AsReadOnly(latest.Values.ToArray());
    }

    private static ReadOnlyCollection<CarePatientWitness> LatestTreatments(CareSnapshot closing,
        CareContinuationContext? previous)
    {
        SortedDictionary<EntityId, CareTreatment> latest = [];
        if (previous is not null)
        {
            foreach (var witness in previous.PatientWitnesses) latest[witness.PatientActorId] = witness.Treatment;
        }
        // Retained treatments all follow the previous frontier, so ascending order leaves the latest per patient.
        foreach (var treatment in closing.Treatments.OrderBy(value => value.WorldSequence))
        {
            var acknowledgment = closing.GetAcknowledgment(treatment.AcknowledgmentId);
            latest[closing.GetAgreement(acknowledgment.AgreementId).PatientActorId] = treatment;
        }
        return Array.AsReadOnly(latest.Select(value => new CarePatientWitness(value.Key, value.Value)).ToArray());
    }
}

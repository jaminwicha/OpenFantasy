namespace OpenFantasy.Core.Care;

public sealed partial class CareSnapshot
{
    /// <summary>Explicitly selects the fixed voluntary-closure interpretation, including an empty history.</summary>
    public static CareSnapshot CreateWithDispositions(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<RestorativeRemedyDefinition> remedies, IEnumerable<CareAgreement> agreements,
        IEnumerable<CareAcknowledgment> acknowledgments, IEnumerable<CareTreatment> treatments,
        IEnumerable<CareDisposition> dispositions, OrdinaryCareSnapshot? ordinary = null)
    {
        ArgumentNullException.ThrowIfNull(dispositions);
        return new(null, worldId, branchId, revision, remedies, agreements, acknowledgments, treatments, ordinary, true, dispositions);
    }

    internal static CareSnapshot RestoreWithDispositions(CareContinuationContext? context, EntityId worldId,
        EntityId branchId, ulong revision, IEnumerable<RestorativeRemedyDefinition> remedies,
        IEnumerable<CareAgreement> agreements, IEnumerable<CareAcknowledgment> acknowledgments,
        IEnumerable<CareTreatment> treatments, IEnumerable<CareDisposition> dispositions, OrdinaryCareSnapshot? ordinary) =>
        new(context, worldId, branchId, revision, remedies, agreements, acknowledgments, treatments, ordinary, true, dispositions);

    public CareSnapshot CloseRequest(CareDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(disposition);
        if (!SupportsDispositions) throw new InvalidOperationException("Voluntary closure requires explicit disposition admission.");
        return new(Continuation, WorldId, BranchId, checked(Revision + 1), Remedies, Agreements,
            Acknowledgments, Treatments, Ordinary, true, Dispositions.Append(disposition));
    }

    /// <summary>Chronological eligibility only; the world still authenticates the actual defeat and agreement.</summary>
    public bool CanAcknowledgeDefeat(EntityId patientActorId, ulong defeatWorldSequence)
    {
        if (Pending is not null) return false;
        ulong closed = 0;
        if (Continuation is { } context)
        {
            closed = context.PatientWitnesses.Where(value => value.PatientActorId == patientActorId)
                .Select(value => value.Treatment.WorldSequence).DefaultIfEmpty(0UL).Max();
            if (SupportsDispositions)
                closed = Math.Max(closed, context.ClosedDefeatWitnesses.Where(value => value.PatientActorId == patientActorId)
                    .Select(value => value.TerminalSequence).DefaultIfEmpty(0UL).Max());
        }
        foreach (var treatment in Treatments)
            if (GetAgreement(GetAcknowledgment(treatment.AcknowledgmentId).AgreementId).PatientActorId == patientActorId)
                closed = Math.Max(closed, treatment.WorldSequence);
        foreach (var disposition in Dispositions)
            if (GetAgreement(GetAcknowledgment(disposition.AcknowledgmentId).AgreementId).PatientActorId == patientActorId)
                closed = Math.Max(closed, disposition.WorldSequence);
        return defeatWorldSequence > closed;
    }
}

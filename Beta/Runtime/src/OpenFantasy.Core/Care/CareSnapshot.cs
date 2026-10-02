using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>Bounded permissions and exact request/terminal history; world authority owns their truth.</summary>
public sealed partial class CareSnapshot
{
    public const int MaximumRemedies = 8;
    public const int MaximumAgreements = 8;
    public const int MaximumAcknowledgments = 256;
    public const int MaximumTreatments = 256;
    public const int MaximumDispositions = 256;

    public CareSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<RestorativeRemedyDefinition> remedies, IEnumerable<CareAgreement> agreements,
        IEnumerable<CareAcknowledgment> acknowledgments, IEnumerable<CareTreatment> treatments,
        OrdinaryCareSnapshot? ordinary = null)
        : this(null, worldId, branchId, revision, remedies, agreements, acknowledgments, treatments, ordinary)
    {
    }

    private CareSnapshot(CareContinuationContext? continuation, EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<RestorativeRemedyDefinition> remedies, IEnumerable<CareAgreement> agreements,
        IEnumerable<CareAcknowledgment> acknowledgments, IEnumerable<CareTreatment> treatments,
        OrdinaryCareSnapshot? ordinary, bool supportsDispositions = false,
        IEnumerable<CareDisposition>? dispositions = null)
    {
        if (worldId == branchId) throw new ArgumentException("Care requires distinct world and branch identities.");
        Remedies = Copy(remedies, value => value.DefinitionId, MaximumRemedies);
        Agreements = Copy(agreements, value => value.AgreementId, MaximumAgreements);
        Acknowledgments = Copy(acknowledgments, value => value.AcknowledgmentId, MaximumAcknowledgments);
        Treatments = Copy(treatments, value => value.TreatmentId, MaximumTreatments);
        SupportsDispositions = supportsDispositions;
        Dispositions = Copy(dispositions ?? [], value => value.DispositionId, MaximumDispositions);
        if (!supportsDispositions && Dispositions.Count != 0)
            throw new ArgumentException("Voluntary closure requires explicit disposition admission.");
        Continuation = continuation;
        ValidateContinuation(ordinary);
        if (Remedies.Count == 0 || ordinary is null && Agreements.Count == 0
            || revision != checked(TotalAcknowledgments + TotalTreatments + TotalDispositions + (ordinary?.Revision ?? 0)))
            throw new ArgumentException("Care requires authored catalogs and exactly one revision per accepted care operation.");
        foreach (var agreement in Agreements)
        {
            var remedy = GetRemedy(agreement.RemedyDefinitionId);
            if (remedy.Version != agreement.RemedyVersion)
                throw new ArgumentException("An agreement requires the exact admitted remedy.");
        }
        HashSet<EntityId> commands = [];
        HashSet<ulong> sequences = [];
        HashSet<ulong> defeats = [];
        HashSet<EntityId> lethalCommands = [];
        foreach (var acknowledgment in Acknowledgments)
        {
            var agreement = GetAgreement(acknowledgment.AgreementId);
            if (acknowledgment.Defeat.TargetActorId != agreement.PatientActorId
                || !commands.Add(acknowledgment.CommandId) || !sequences.Add(acknowledgment.WorldSequence)
                || !defeats.Add(acknowledgment.Defeat.WorldSequence) || !lethalCommands.Add(acknowledgment.Defeat.DecisionCommandId))
                throw new ArgumentException("One actual patient defeat admits at most one acknowledged care obligation.");
        }
        HashSet<EntityId> completed = [];
        foreach (var treatment in Treatments)
        {
            var acknowledgment = GetAcknowledgment(treatment.AcknowledgmentId);
            var remedy = GetRemedy(GetAgreement(acknowledgment.AgreementId).RemedyDefinitionId);
            if (!completed.Add(treatment.AcknowledgmentId) || !commands.Add(treatment.CommandId)
                || !sequences.Add(treatment.WorldSequence) || treatment.WorldSequence <= acknowledgment.WorldSequence
                || treatment.TimingRevision != checked(acknowledgment.Defeat.TimingRevision + 1)
                || treatment.HpAfter != Math.Min((int)remedy.RestoredHp, treatment.MaximumHp))
                throw new ArgumentException("Treatment must follow one unused acknowledgment with the exact capped health restoration.");
        }
        foreach (var disposition in Dispositions)
        {
            var acknowledgment = GetAcknowledgment(disposition.AcknowledgmentId);
            var agreement = GetAgreement(acknowledgment.AgreementId);
            EntityId requiredActor = disposition.Kind == CareDispositionKind.PatientCancelled
                ? agreement.PatientActorId : agreement.HelperActorId;
            if (!completed.Add(disposition.AcknowledgmentId) || !commands.Add(disposition.CommandId)
                || !sequences.Add(disposition.WorldSequence) || disposition.WorldSequence <= acknowledgment.WorldSequence
                || disposition.DecidingActorId != requiredActor)
                throw new ArgumentException("A disposition closes one untreated request through its exact patient or helper.");
        }
        ValidateChronology();
        var pending = Acknowledgments.Where(value => !completed.Contains(value.AcknowledgmentId)).ToArray();
        if (pending.Length > 1 || pending.Length == 1 && sequences.Any(value => value > pending[0].WorldSequence))
            throw new ArgumentException("Care retains at most one latest pending obligation.");
        Pending = pending.SingleOrDefault();
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Ordinary = ordinary;
        ValidateOrdinary();
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<RestorativeRemedyDefinition> Remedies { get; }
    public ReadOnlyCollection<CareAgreement> Agreements { get; }
    public ReadOnlyCollection<CareAcknowledgment> Acknowledgments { get; }
    public ReadOnlyCollection<CareTreatment> Treatments { get; }
    public bool SupportsDispositions { get; }
    public ReadOnlyCollection<CareDisposition> Dispositions { get; }
    public CareAcknowledgment? Pending { get; }
    public OrdinaryCareSnapshot? Ordinary { get; }
    public bool SupportsOrdinary => Ordinary is not null;
    public ContentHash ContentHash => ContentHash.Compute(CareCodec.Encode(this));

    public RestorativeRemedyDefinition GetRemedy(EntityId id) => Remedies.SingleOrDefault(value => value.DefinitionId == id)
        ?? throw new ArgumentException("The remedy is outside this care owner.");
    public CareAgreement GetAgreement(EntityId id) => Agreements.SingleOrDefault(value => value.AgreementId == id)
        ?? throw new ArgumentException("The agreement is outside this care owner.");
    public CareAcknowledgment GetAcknowledgment(EntityId id) => Acknowledgments.SingleOrDefault(value => value.AcknowledgmentId == id)
        ?? throw new ArgumentException("The acknowledgment is outside this care owner.");

    // Structural append paths; the world still authenticates and publishes each operation.
    public CareSnapshot Acknowledge(CareAcknowledgment acknowledgment) => new(Continuation, WorldId, BranchId, checked(Revision + 1),
        Remedies, Agreements, Acknowledgments.Append(acknowledgment), Treatments, Ordinary, SupportsDispositions, Dispositions);
    public CareSnapshot Treat(CareTreatment treatment) => new(Continuation, WorldId, BranchId, checked(Revision + 1),
        Remedies, Agreements, Acknowledgments, Treatments.Append(treatment), Ordinary, SupportsDispositions, Dispositions);

    public CareSnapshot WithOrdinary(OrdinaryCareSnapshot ordinary)
    {
        ArgumentNullException.ThrowIfNull(ordinary);
        if (Ordinary is null) throw new InvalidOperationException("Ordinary care requires explicit admission.");
        Ordinary.ValidateSuccessor(ordinary);
        if (ordinary.Revision <= Ordinary.Revision) throw new ArgumentException("Ordinary care must append an actual operation.");
        var successor = new CareSnapshot(Continuation, WorldId, BranchId, checked(Revision + ordinary.Revision - Ordinary.Revision),
            Remedies, Agreements, Acknowledgments, Treatments, ordinary, SupportsDispositions, Dispositions);
        // The existing aftermath records are passed unchanged; validate the new cross-care
        // positions without repeatedly re-encoding the already preserved aftermath history.
        ValidateAddedPositions(successor);
        return successor;
    }

    public void ValidateSuccessor(CareSnapshot successor)
    {
        ArgumentNullException.ThrowIfNull(successor);
        if (!CareContinuationContext.Same(Continuation, successor.Continuation))
            throw new ArgumentException("A care successor stays within its exact continuation; closure is a separate transition.");
        if (successor.WorldId != WorldId || successor.BranchId != BranchId
            || successor.SupportsOrdinary != SupportsOrdinary || successor.SupportsDispositions != SupportsDispositions
            || (SupportsOrdinary ? successor.Revision <= Revision : successor.Revision != checked(Revision + 1))
            || !Remedies.Select(value => (value.DefinitionId, value.Version)).SequenceEqual(successor.Remedies.Select(value => (value.DefinitionId, value.Version)))
            || !Agreements.Select(value => (value.AgreementId, value.Version)).SequenceEqual(successor.Agreements.Select(value => (value.AgreementId, value.Version)))
            || Acknowledgments.Any(value => !successor.Acknowledgments.Any(next => CareCodecV1.EncodeAcknowledgment(value).AsSpan().SequenceEqual(CareCodecV1.EncodeAcknowledgment(next))))
            || Dispositions.Any(value => !successor.Dispositions.Any(next => CareCodecV5.EncodeDisposition(value).AsSpan().SequenceEqual(CareCodecV5.EncodeDisposition(next))))
            || Treatments.Any(value => !successor.Treatments.Any(next => CareCodecV1.EncodeTreatment(value).AsSpan().SequenceEqual(CareCodecV1.EncodeTreatment(next)))))
            throw new ArgumentException("A care successor preserves authored permission and every accepted operation exactly.");
        Ordinary?.ValidateSuccessor(successor.Ordinary!);
        ValidateAddedPositions(successor);
    }

    private void ValidateChronology()
    {
        CareAcknowledgment? active = null;
        Dictionary<EntityId, CareTreatment> priorByPatient = [];
        if (Continuation is { } continuation)
        {
            foreach (var witness in continuation.PatientWitnesses) priorByPatient[witness.PatientActorId] = witness.Treatment;
        }
        Dictionary<EntityId, ulong> closedByPatient = [];
        if (Continuation is { SupportsDispositions: true } closed)
            foreach (var witness in closed.ClosedDefeatWitnesses) closedByPatient[witness.PatientActorId] = witness.TerminalSequence;
        var dispositions = Dispositions.ToDictionary(value => value.WorldSequence);
        var acknowledgments = Acknowledgments.ToDictionary(value => value.WorldSequence);
        var treatments = Treatments.ToDictionary(value => value.WorldSequence);
        foreach (ulong sequence in acknowledgments.Keys.Concat(treatments.Keys).Concat(dispositions.Keys).Order())
        {
            if (acknowledgments.TryGetValue(sequence, out var acknowledgment))
            {
                EntityId patient = GetAgreement(acknowledgment.AgreementId).PatientActorId;
                if (active is not null || priorByPatient.TryGetValue(patient, out var prior)
                    && acknowledgment.Defeat.WorldSequence <= prior.WorldSequence
                    || closedByPatient.TryGetValue(patient, out ulong closedSequence) && acknowledgment.Defeat.WorldSequence <= closedSequence)
                    throw new ArgumentException("A care obligation cannot overlap another or reuse a defeat predating its patient's terminal frontier.");
                active = acknowledgment;
            }
            else if (dispositions.TryGetValue(sequence, out var disposition))
            {
                if (active?.AcknowledgmentId != disposition.AcknowledgmentId)
                    throw new ArgumentException("Disposition must close the sole outstanding care obligation.");
                closedByPatient[GetAgreement(active.AgreementId).PatientActorId] = disposition.WorldSequence;
                active = null;
            }
            else
            {
                var treatment = treatments[sequence];
                if (active?.AcknowledgmentId != treatment.AcknowledgmentId)
                    throw new ArgumentException("Treatment must close the sole outstanding care obligation.");
                EntityId patient = GetAgreement(active.AgreementId).PatientActorId;
                if (priorByPatient.TryGetValue(patient, out var prior) && treatment.PatientRevisionBefore <= prior.PatientRevisionAfter)
                    throw new ArgumentException("Another real defeat must advance the patient's resource revision before repeat aid.");
                priorByPatient[patient] = treatment;
                if (SupportsDispositions) closedByPatient[patient] = treatment.WorldSequence;
                active = null;
            }
        }
    }

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source, Func<T, EntityId> identity, int maximum) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        SortedDictionary<EntityId, T> values = [];
        foreach (T value in source)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (values.Count == maximum || !values.TryAdd(identity(value), value))
                throw new ArgumentException("Care catalogs and histories require bounded unique identities.");
        }
        return Array.AsReadOnly(values.Values.ToArray());
    }
}

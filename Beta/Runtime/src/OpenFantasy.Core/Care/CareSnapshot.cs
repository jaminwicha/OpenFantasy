using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>Bounded permissions and exact acknowledgment/treatment history; world authority owns their truth.</summary>
public sealed partial class CareSnapshot
{
    public const int MaximumRemedies = 8;
    public const int MaximumAgreements = 8;
    public const int MaximumAcknowledgments = 256;
    public const int MaximumTreatments = 256;

    public CareSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<RestorativeRemedyDefinition> remedies, IEnumerable<CareAgreement> agreements,
        IEnumerable<CareAcknowledgment> acknowledgments, IEnumerable<CareTreatment> treatments,
        OrdinaryCareSnapshot? ordinary = null)
    {
        if (worldId == branchId) throw new ArgumentException("Care requires distinct world and branch identities.");
        Remedies = Copy(remedies, value => value.DefinitionId, MaximumRemedies);
        Agreements = Copy(agreements, value => value.AgreementId, MaximumAgreements);
        Acknowledgments = Copy(acknowledgments, value => value.AcknowledgmentId, MaximumAcknowledgments);
        Treatments = Copy(treatments, value => value.TreatmentId, MaximumTreatments);
        if (Remedies.Count == 0 || ordinary is null && Agreements.Count == 0
            || revision != checked((ulong)(Acknowledgments.Count + Treatments.Count) + (ordinary?.Revision ?? 0)))
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

    internal CareSnapshot Acknowledge(CareAcknowledgment acknowledgment) => new(WorldId, BranchId, checked(Revision + 1),
        Remedies, Agreements, Acknowledgments.Append(acknowledgment), Treatments, Ordinary);
    internal CareSnapshot Treat(CareTreatment treatment) => new(WorldId, BranchId, checked(Revision + 1),
        Remedies, Agreements, Acknowledgments, Treatments.Append(treatment), Ordinary);

    internal CareSnapshot WithOrdinary(OrdinaryCareSnapshot ordinary)
    {
        ArgumentNullException.ThrowIfNull(ordinary);
        if (Ordinary is null) throw new InvalidOperationException("Ordinary care requires explicit admission.");
        Ordinary.ValidateSuccessor(ordinary);
        if (ordinary.Revision <= Ordinary.Revision) throw new ArgumentException("Ordinary care must append an actual operation.");
        return new(WorldId, BranchId, checked(Revision + ordinary.Revision - Ordinary.Revision),
            Remedies, Agreements, Acknowledgments, Treatments, ordinary);
    }

    internal void ValidateSuccessor(CareSnapshot successor)
    {
        if (successor.WorldId != WorldId || successor.BranchId != BranchId
            || successor.SupportsOrdinary != SupportsOrdinary
            || (SupportsOrdinary ? successor.Revision <= Revision : successor.Revision != checked(Revision + 1))
            || !Remedies.Select(value => (value.DefinitionId, value.Version)).SequenceEqual(successor.Remedies.Select(value => (value.DefinitionId, value.Version)))
            || !Agreements.Select(value => (value.AgreementId, value.Version)).SequenceEqual(successor.Agreements.Select(value => (value.AgreementId, value.Version)))
            || Acknowledgments.Any(value => !successor.Acknowledgments.Any(next => CareCodecV1.EncodeAcknowledgment(value).AsSpan().SequenceEqual(CareCodecV1.EncodeAcknowledgment(next))))
            || Treatments.Any(value => !successor.Treatments.Any(next => CareCodecV1.EncodeTreatment(value).AsSpan().SequenceEqual(CareCodecV1.EncodeTreatment(next)))))
            throw new ArgumentException("A care successor preserves authored permission and every accepted operation exactly.");
        Ordinary?.ValidateSuccessor(successor.Ordinary!);
        ValidateAddedOrdinaryPositions(successor);
    }

    private void ValidateChronology()
    {
        CareAcknowledgment? active = null;
        Dictionary<EntityId, CareTreatment> priorByPatient = [];
        var acknowledgments = Acknowledgments.ToDictionary(value => value.WorldSequence);
        var treatments = Treatments.ToDictionary(value => value.WorldSequence);
        foreach (ulong sequence in acknowledgments.Keys.Concat(treatments.Keys).Order())
        {
            if (acknowledgments.TryGetValue(sequence, out var acknowledgment))
            {
                EntityId patient = GetAgreement(acknowledgment.AgreementId).PatientActorId;
                if (active is not null || priorByPatient.TryGetValue(patient, out var prior)
                    && acknowledgment.Defeat.WorldSequence <= prior.WorldSequence)
                    throw new ArgumentException("A care obligation cannot overlap another or reuse defeat predating its patient's treatment.");
                active = acknowledgment;
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

using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Tests.Care;

internal static class CareModelFixture
{
    internal static EntityId Id(ulong value) => new(0xca4e2501, value);
    internal static readonly EntityId World = Id(1);
    internal static readonly EntityId Branch = Id(2);
    internal static readonly EntityId Patient = Id(10);
    internal static readonly EntityId Helper = Id(11);
    internal static readonly ContentHash ItemVersion = ContentHash.Compute("care model item"u8);
    internal static readonly ContentHash ActionHash = ContentHash.Compute("care model lethal action"u8);
    internal static readonly ContentHash TimingHash = ContentHash.Compute("care model outcome timing"u8);

    internal static RestorativeRemedyDefinition Remedy(ulong identity = 100, uint hp = 40, ulong range = 256) =>
        new(Id(identity), new CountedItemDefinition(Id(101), ItemVersion), hp, range);

    internal static CareAgreement Agreement(ulong identity = 200, EntityId? patient = null,
        RestorativeRemedyDefinition? remedy = null)
    {
        remedy ??= Remedy();
        return new(Id(identity), patient ?? Patient, Helper, remedy.DefinitionId, remedy.Version);
    }

    internal static EncounterActionResolution Defeat(ulong sequence = 10, ulong timingRevision = 7,
        ulong command = 1_000, EntityId? patient = null, uint afterHp = 0,
        IEnumerable<EntityId>? cancelled = null) =>
        new(sequence, timingRevision, Id(12), Id(command), patient ?? Patient, Id(102),
            EncounterActionResolutionStatus.Damaged, 40 - afterHp, 40, afterHp, 0, 100, 1_000, cancelled ?? []);

    internal static CareAcknowledgment Acknowledgment(ulong sequence = 11, ulong command = 2_000,
        EncounterActionResolution? defeat = null, EntityId? encounter = null, CareAgreement? agreement = null) =>
        new(EntityId.DeriveSpawned(Id(command), 0), Id(command), (agreement ?? Agreement()).AgreementId,
            encounter ?? Id(90), sequence, ActionHash, TimingHash, defeat ?? Defeat());

    internal static CareTreatment Treatment(CareAcknowledgment acknowledgment, ulong sequence = 12,
        ulong command = 3_000, ulong patientRevision = 3, ulong? timingRevision = null,
        int maximumHp = 100, int hpAfter = 40, uint speed = 80, ulong? delay = null) =>
        new(EntityId.DeriveSpawned(Id(command), 1), Id(command), acknowledgment.AcknowledgmentId,
            EntityId.DeriveSpawned(Id(command), 0), sequence,
            timingRevision ?? checked(acknowledgment.Defeat.TimingRevision + 1), patientRevision,
            maximumHp, hpAfter, speed, delay ?? (10_000UL + speed - 1) / speed);

    internal static CareSnapshot Create(IEnumerable<CareAcknowledgment>? acknowledgments = null,
        IEnumerable<CareTreatment>? treatments = null, IEnumerable<RestorativeRemedyDefinition>? remedies = null,
        IEnumerable<CareAgreement>? agreements = null, ulong? revision = null)
    {
        CareAcknowledgment[] accepted = acknowledgments?.ToArray() ?? [];
        CareTreatment[] completed = treatments?.ToArray() ?? [];
        return new(World, Branch, revision ?? (ulong)(accepted.Length + completed.Length),
            remedies ?? [Remedy()], agreements ?? [Agreement()], accepted, completed);
    }

    internal static CareSnapshot History(int count, bool maximumCancelled = false)
    {
        List<CareAcknowledgment> acknowledgments = [];
        List<CareTreatment> treatments = [];
        for (ulong index = 0; index < (ulong)count; index++)
        {
            var defeat = Defeat(10 + index * 3, 7 + index * 2, 1_000 + index,
                cancelled: maximumCancelled ? Enumerable.Range(0, 64).Select(value => Id(10_000 + (ulong)value)) : []);
            var acknowledgment = Acknowledgment(11 + index * 3, 2_000 + index, defeat);
            acknowledgments.Add(acknowledgment);
            treatments.Add(Treatment(acknowledgment, 12 + index * 3, 3_000 + index, 3 + index * 2));
        }
        return Create(acknowledgments, treatments);
    }
}

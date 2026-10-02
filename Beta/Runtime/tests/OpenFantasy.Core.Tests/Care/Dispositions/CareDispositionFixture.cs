using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;

namespace OpenFantasy.Core.Tests.Care.Dispositions;

internal static class CareDispositionFixture
{
    internal static CareSnapshot Create(IEnumerable<CareAcknowledgment>? acknowledgments = null,
        IEnumerable<CareTreatment>? treatments = null, IEnumerable<CareDisposition>? dispositions = null,
        IEnumerable<CareAgreement>? agreements = null, OrdinaryCareSnapshot? ordinary = null,
        ulong? revision = null)
    {
        CareAcknowledgment[] accepted = acknowledgments?.ToArray() ?? [];
        CareTreatment[] treated = treatments?.ToArray() ?? [];
        CareDisposition[] closed = dispositions?.ToArray() ?? [];
        return CareSnapshot.CreateWithDispositions(World, Branch,
            revision ?? checked((ulong)(accepted.Length + treated.Length + closed.Length) + (ordinary?.Revision ?? 0)),
            [Remedy()], agreements ?? [Agreement()], accepted, treated, closed, ordinary);
    }

    internal static CareDisposition Disposition(CareAcknowledgment acknowledgment,
        CareDispositionKind kind = CareDispositionKind.PatientCancelled, ulong sequence = 12, ulong command = 4_000,
        EntityId? actor = null) => new(EntityId.DeriveSpawned(Id(command), 0), Id(command),
            acknowledgment.AcknowledgmentId, actor ?? (kind == CareDispositionKind.PatientCancelled ? Patient : Helper), kind, sequence);
}

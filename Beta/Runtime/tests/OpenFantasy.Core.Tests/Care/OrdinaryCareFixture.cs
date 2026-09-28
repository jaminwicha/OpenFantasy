using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;

namespace OpenFantasy.Core.Tests.Care;

internal static class OrdinaryCareFixture
{
    internal static readonly RestorativeFormBinding Form = new(Id(900), ContentHash.Compute("ordinary living form"u8));

    internal static RestorativeUseDefinition Definition(bool self = false, ulong id = 500,
        IEnumerable<RestorativeFormBinding>? forms = null, ulong milliseconds = 1_800) =>
        new(Id(id), Remedy().DefinitionId, Remedy().Version,
            self ? RestorativeDeliveryMode.SelfUse : RestorativeDeliveryMode.LivingAssistance, forms ?? [Form], milliseconds);

    internal static CarePermission Permission(CarePermissionRevocation? revocation = null) =>
        new(Id(700), Patient, Helper, Definition().DefinitionId, Definition().Version, revocation);

    internal static CarePermissionRevocation Revocation(ulong sequence = 20, uint ordinal = 1, ulong command = 10_001) =>
        new(EntityId.DeriveSpawned(Id(command), 0), Id(command), Id(700), Patient, sequence, ordinal);

    internal static RestorativeUse Use(bool self = false, ulong sequence = 20, uint ordinal = 0,
        ulong command = 10_000, ulong patientRevision = 4, int hpBefore = 10, int hpAfter = 50,
        int maximumHp = 60, RestorativeUseDefinition? definition = null, CarePermission? permission = null)
    {
        definition ??= self ? Definition(true, 501) : Definition();
        permission ??= Permission();
        return new(EntityId.DeriveSpawned(Id(command), 1), Id(command), self ? Patient : Helper, Patient,
            definition.DefinitionId, definition.Version, self ? null : permission.PermissionId,
            self ? null : permission.GrantVersion, self ? null : 0UL, EntityId.DeriveSpawned(Id(command), 0),
            sequence, ordinal, patientRevision, maximumHp, hpBefore, hpAfter);
    }

    internal static OrdinaryCareSnapshot Ordinary(IEnumerable<RestorativeUse>? uses = null,
        CarePermissionRevocation? revocation = null) =>
        new([Definition(), Definition(true, 501)], [Permission(revocation)], uses ?? []);

    internal static CareSnapshot CreateOrdinary(OrdinaryCareSnapshot? ordinary = null, CareSnapshot? aftermath = null)
    {
        ordinary ??= Ordinary();
        aftermath ??= CareModelFixture.Create();
        return new(aftermath.WorldId, aftermath.BranchId, checked(aftermath.Revision + ordinary.Revision),
            aftermath.Remedies, aftermath.Agreements, aftermath.Acknowledgments, aftermath.Treatments, ordinary);
    }
}

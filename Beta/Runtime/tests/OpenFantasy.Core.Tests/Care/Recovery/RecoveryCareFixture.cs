using OpenFantasy.Core.Care;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;

namespace OpenFantasy.Core.Tests.Care.Recovery;

internal static class RecoveryCareFixture
{
    internal static RestorativeUseDefinition Definition() => RestorativeUseDefinition.CreateIncapacitatedAssistance(
        Id(502), Remedy().DefinitionId, Remedy().Version, [OrdinaryCareFixture.Form]);

    internal static CarePermission Permission(CarePermissionRevocation? revoked = null) =>
        new(Id(702), Patient, Helper, Definition().DefinitionId, Definition().Version, revoked);

    internal static RestorativeUse Use(ulong sequence = 20, uint ordinal = 0, ulong command = 50_000,
        int before = 0, int after = 40, int maximum = 60, bool self = false, bool permission = true,
        RestorativeUseDefinition? definition = null, CarePermission? grant = null, ulong duration = 1_800)
    {
        definition ??= Definition();
        grant ??= Permission();
        return RestorativeUse.CreateIncapacitatedAssistance(EntityId.DeriveSpawned(Id(command), 1), Id(command),
            self ? Patient : Helper, Patient, definition.DefinitionId, definition.Version,
            permission ? grant.PermissionId : null, permission ? grant.GrantVersion : null, permission ? 0UL : null,
            EntityId.DeriveSpawned(Id(command), 0), sequence, ordinal, 4, maximum, before, after, duration);
    }

    internal static CarePermissionRevocation Revoke(ulong sequence = 21, uint ordinal = 0, ulong command = 50_001) =>
        new(EntityId.DeriveSpawned(Id(command), 0), Id(command), Id(702), Patient, sequence, ordinal);

    internal static CareSnapshot Create(IEnumerable<RestorativeUse>? uses = null)
    {
        var ordinary = new OrdinaryCareSnapshot([Definition(), OrdinaryCareFixture.Definition(true, 501)], [Permission()], uses ?? []);
        return new(World, Branch, ordinary.Revision, [Remedy()], [], [], [], ordinary);
    }
}

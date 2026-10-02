using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Fresh opt-in to general assisted recovery; never an implicit upgrade of a lived world.</summary>
public static class WorldRecoveryAdmission
{
    public const ushort ProfileVersion = 37;

    public static WorldSnapshot CreateFresh(WorldSnapshot basis,
        IEnumerable<RestorativeUseDefinition> recoveryDefinitions, IEnumerable<CarePermission> recoveryPermissions)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(recoveryDefinitions);
        ArgumentNullException.ThrowIfNull(recoveryPermissions);
        if (basis.ProfileVersion != WorldControllerAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.Care is not { Revision: 0, Ordinary: { } ordinary }
            || ordinary.Revision != 0 || basis.Coordination is not { Anchor: null }
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 }
            || basis.Controllers!.Assignments.Any(value => value.Revision != 0))
        {
            throw new ArgumentException("Recovery admission requires an unplayed controller-aware world.", nameof(basis));
        }

        RestorativeUseDefinition[] definitions = [.. recoveryDefinitions];
        CarePermission[] permissions = [.. recoveryPermissions];
        if (definitions.Length == 0 || definitions.Any(value => value is null
                || value.DeliveryMode != RestorativeDeliveryMode.IncapacitatedAssistance)
            || permissions.Any(value => value is null || !value.IsActive
                || definitions.All(definition => definition.DefinitionId != value.UseDefinitionId)))
        {
            throw new ArgumentException("Recovery additions require explicit incapacitated-assistance policies and fresh grants.");
        }

        var care = new CareSnapshot(basis.WorldId, basis.BranchId, 0, basis.Care.Remedies, basis.Care.Agreements, [], [],
            new OrdinaryCareSnapshot(ordinary.Definitions.Concat(definitions), ordinary.Permissions.Concat(permissions), []));
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.recovery-admission.v1",
            basis.ContentHash.ToByteArray(), WorldRecoveryPolicy.RulesPin.ToByteArray(), care.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: basis.Controllers, recovery: WorldRecoveryPolicy.AssistedIncapacity);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        bool hasDefinitions = world.Care?.Ordinary?.SupportsIncapacitatedAssistance == true;
        if ((world.Recovery is not null) != hasDefinitions
            || world.Recovery is not null && (world.Controllers is null || world.EncounterOutcome is null
                || world.Coordination?.Markets is null))
        {
            throw new ArgumentException("Recovery definitions require their explicit complete-world interpretation and vice versa.");
        }
    }

    internal static bool SupportsRecipient(WorldSnapshot world, RestorativeUseDefinition definition, int currentHp) =>
        definition.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance
            ? world.Recovery is not null && currentHp == 0
            : currentHp > 0;
}

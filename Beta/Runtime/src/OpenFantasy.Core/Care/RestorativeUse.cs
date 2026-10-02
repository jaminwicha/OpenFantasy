using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Completed ordinary use with exact material, permission, resource and exploration witnesses.</summary>
public sealed class RestorativeUse
{
    public RestorativeUse(EntityId useId, EntityId commandId, EntityId actorId, EntityId recipientActorId,
        EntityId useDefinitionId, ContentHash useDefinitionVersion, EntityId? permissionId,
        ContentHash? permissionGrantVersion, ulong? permissionRevision, EntityId consumptionId,
        ulong worldSequence, uint commandOrdinal, ulong patientRevisionBefore, int maximumHp,
        int hpBefore, int hpAfter, ulong explorationMilliseconds = RestorativeUseDefinition.ExplorationDurationMilliseconds)
        : this(useId, commandId, actorId, recipientActorId, useDefinitionId, useDefinitionVersion, permissionId,
            permissionGrantVersion, permissionRevision, consumptionId, worldSequence, commandOrdinal,
            patientRevisionBefore, maximumHp, hpBefore, hpAfter, explorationMilliseconds, false)
    {
    }

    /// <summary>Explicit HP-zero assistance witness; the world separately authenticates recovery policy and permission.</summary>
    public static RestorativeUse CreateIncapacitatedAssistance(EntityId useId, EntityId commandId, EntityId actorId,
        EntityId recipientActorId, EntityId useDefinitionId, ContentHash useDefinitionVersion, EntityId? permissionId,
        ContentHash? permissionGrantVersion, ulong? permissionRevision, EntityId consumptionId,
        ulong worldSequence, uint commandOrdinal, ulong patientRevisionBefore, int maximumHp,
        int hpBefore, int hpAfter, ulong explorationMilliseconds = RestorativeUseDefinition.ExplorationDurationMilliseconds) =>
        new(useId, commandId, actorId, recipientActorId, useDefinitionId, useDefinitionVersion, permissionId,
            permissionGrantVersion, permissionRevision, consumptionId, worldSequence, commandOrdinal,
            patientRevisionBefore, maximumHp, hpBefore, hpAfter, explorationMilliseconds, true);

    private RestorativeUse(EntityId useId, EntityId commandId, EntityId actorId, EntityId recipientActorId,
        EntityId useDefinitionId, ContentHash useDefinitionVersion, EntityId? permissionId,
        ContentHash? permissionGrantVersion, ulong? permissionRevision, EntityId consumptionId,
        ulong worldSequence, uint commandOrdinal, ulong patientRevisionBefore, int maximumHp,
        int hpBefore, int hpAfter, ulong explorationMilliseconds, bool incapacitatedAssistance)
    {
        ArgumentNullException.ThrowIfNull(useDefinitionVersion);
        bool self = actorId == recipientActorId;
        if (useId != EntityId.DeriveSpawned(commandId, 1) || consumptionId != EntityId.DeriveSpawned(commandId, 0)
            || worldSequence == 0 || commandOrdinal >= WorldStepBatch.MaximumActorsPerBatch
            || patientRevisionBefore == ulong.MaxValue || maximumHp <= 0
            || (incapacitatedAssistance ? hpBefore != 0 || self : hpBefore <= 0)
            || hpBefore >= maximumHp || hpAfter <= hpBefore || hpAfter > maximumHp
            || explorationMilliseconds != RestorativeUseDefinition.ExplorationDurationMilliseconds
            || self && (permissionId is not null || permissionGrantVersion is not null || permissionRevision is not null)
            || !self && (permissionId is null || permissionGrantVersion is null || permissionRevision != 0))
            throw new ArgumentException(incapacitatedAssistance
                ? "Incapacitated assistance requires zero initial HP, distinct supplier, capped effect, exact permission and exploration position."
                : "Ordinary use requires a living capped effect, exact dose, permission and exploration position.");
        var identities = new HashSet<EntityId> { useId, commandId, actorId, useDefinitionId, consumptionId };
        if (identities.Count != 5 || !self && !identities.Add(recipientActorId)
            || permissionId is { } permission && !identities.Add(permission))
            throw new ArgumentException("An ordinary use cannot alias its participants, definition, permission or generated identities.");
        UseId = useId;
        CommandId = commandId;
        ActorId = actorId;
        RecipientActorId = recipientActorId;
        UseDefinitionId = useDefinitionId;
        UseDefinitionVersion = useDefinitionVersion;
        PermissionId = permissionId;
        PermissionGrantVersion = permissionGrantVersion;
        PermissionRevision = permissionRevision;
        ConsumptionId = consumptionId;
        WorldSequence = worldSequence;
        CommandOrdinal = commandOrdinal;
        PatientRevisionBefore = patientRevisionBefore;
        MaximumHp = maximumHp;
        HpBefore = hpBefore;
        HpAfter = hpAfter;
        ExplorationMilliseconds = explorationMilliseconds;
    }

    public EntityId UseId { get; }
    public EntityId CommandId { get; }
    public EntityId ActorId { get; }
    public EntityId RecipientActorId { get; }
    public EntityId UseDefinitionId { get; }
    public ContentHash UseDefinitionVersion { get; }
    public EntityId? PermissionId { get; }
    public ContentHash? PermissionGrantVersion { get; }
    public ulong? PermissionRevision { get; }
    public EntityId ConsumptionId { get; }
    public ulong WorldSequence { get; }
    public uint CommandOrdinal { get; }
    public ulong PatientRevisionBefore { get; }
    public ulong PatientRevisionAfter => PatientRevisionBefore + 1;
    public int MaximumHp { get; }
    public int HpBefore { get; }
    public int HpAfter { get; }
    public ulong ExplorationMilliseconds { get; }
}

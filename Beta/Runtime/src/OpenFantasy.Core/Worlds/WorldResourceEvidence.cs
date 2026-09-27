using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Named domain evidence for one command's resource revisions; never permission to execute it.</summary>
public abstract class WorldResourceEvidence
{
    private protected WorldResourceEvidence(EntityId commandId) => CommandId = commandId;
    public EntityId CommandId { get; }
}

/// <summary>Retains the selected request and exact resolved attack in its scenario and seeded episode.</summary>
public sealed class WorldEncounterResourceEvidence : WorldResourceEvidence
{
    public WorldEncounterResourceEvidence(EntityId scenarioId, WorldActionCommand request, EncounterActionResolution resolution)
        : base((request ?? throw new ArgumentNullException(nameof(request))).CommandId)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (request.Intent is not QueueEncounterAbilityIntent intent
            || resolution.Status != EncounterActionResolutionStatus.Damaged
            || resolution.DecisionCommandId != request.CommandId || resolution.ActorId != request.ActorId
            || resolution.AbilityDefinitionId != intent.AbilityDefinitionId || resolution.TargetActorId != intent.TargetActorId)
            throw new ArgumentException("Resource evidence requires one exact selected and successfully resolved ability.");
        ScenarioId = scenarioId;
        Request = request;
        Resolution = resolution;
    }

    public EntityId ScenarioId { get; }
    public WorldActionCommand Request { get; }
    public EncounterActionResolution Resolution { get; }
    public EntityId EncounterId => ((QueueEncounterAbilityIntent)Request.Intent).EncounterId;
}

/// <summary>The command resolves to exactly one retained completed quest and its pinned reward.</summary>
public sealed class WorldRewardResourceEvidence(EntityId commandId) : WorldResourceEvidence(commandId);

/// <summary>The treatment resolves permission, recipient, remedy and separately retained dose consumption.</summary>
public sealed class WorldTreatmentResourceEvidence(EntityId commandId, EntityId treatmentId) : WorldResourceEvidence(commandId)
{
    public EntityId TreatmentId { get; } = treatmentId;
}

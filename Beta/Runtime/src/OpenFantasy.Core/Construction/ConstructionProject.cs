using System.Collections.ObjectModel;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Construction;

public sealed record InstalledStructure(EntityId StructureId, EntityId GuardLotId, ulong GuardLotRevision,
    EntityId ProductionBatchId, EntityId CompletionCommandId);

/// <summary>One builder-owned commitment. Pausing and obstruction retain earned work and reserved stock.</summary>
public sealed class ConstructionProject
{
    public const int MaximumAccessRequirements = 32;

    public ConstructionProject(EntityId projectId, ConstructionDefinition definition, EntityId ownerActorId,
        EntityId builderActorId, EntityId questId, EntityId projectCustodianId, EntityId reservationId,
        SubunitPosition siteFeet, SubunitPosition workFeet, IEnumerable<GroundAccessRequirement> requiredAccess,
        ulong revision = 0, ExposureProgressState? progress = null, InstalledStructure? installed = null,
        ConstructionStockLocation stockLocation = ConstructionStockLocation.BuilderCarried)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(requiredAccess);
        if (ownerActorId != builderActorId || !Enum.IsDefined(stockLocation)
            || new[] { projectId, definition.DefinitionId, ownerActorId, questId, projectCustodianId, reservationId }
                .Distinct().Count() != 6 || siteFeet.Z != workFeet.Z
            || siteFeet.Z % SubunitPosition.SubunitsPerCell != 0)
        {
            throw new ArgumentException("The admitted project requires distinct roles, carried owner stock and one ground plane.");
        }
        var restored = progress is null ? ExposureProgress.Begin(projectId, definition.WorkRate)
            : ExposureProgress.Restore(progress, definition.WorkRate);
        if (restored.SubjectId != projectId || restored.CompletedUnits > definition.RequiredWork.Value
            || restored.CompletedUnits != revision || restored.Remainder != 0
            || (installed is not null) != (restored.CompletedUnits == definition.RequiredWork.Value))
        {
            throw new ArgumentException("Project revision and completion must match the exact qualified work count.");
        }
        if (installed is not null && (installed.GuardLotRevision != 0
            || installed.GuardLotId != EntityId.DeriveSpawned(installed.CompletionCommandId,
                checked((ulong)definition.InstalledOutputIndex + 1))))
        {
            throw new ArgumentException("An installed output retains its original created lot identity and revision; relocation is not admitted.");
        }
        List<GroundAccessRequirement> access = [];
        foreach (GroundAccessRequirement requirement in requiredAccess)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            if (access.Count == MaximumAccessRequirements || access.Contains(requirement)
                || requirement.From.Z != siteFeet.Z || requirement.To.Z != siteFeet.Z)
            {
                throw new ArgumentException("Access requirements must be bounded, unique and on the admitted ground plane.");
            }
            access.Add(requirement);
        }
        if (access.Count == 0 || !access.Any(value => value.ActorId == builderActorId && value.From == workFeet)
            || access.Count + access.Select(value => (value.ActorId, value.To)).Distinct().Count() > MaximumAccessRequirements)
        {
            throw new ArgumentException("A project must preserve explicit exits with enough budget for current actor positions.");
        }
        ProjectId = projectId;
        Definition = definition;
        OwnerActorId = ownerActorId;
        BuilderActorId = builderActorId;
        QuestId = questId;
        ProjectCustodianId = projectCustodianId;
        ReservationId = reservationId;
        SiteFeet = siteFeet;
        WorkFeet = workFeet;
        RequiredAccess = Array.AsReadOnly(access.OrderBy(value => value.ActorId)
            .ThenBy(value => value.From.X).ThenBy(value => value.From.Y).ThenBy(value => value.From.Z)
            .ThenBy(value => value.To.X).ThenBy(value => value.To.Y).ThenBy(value => value.To.Z).ToArray());
        Revision = revision;
        Progress = restored.Capture();
        Installed = installed;
        StockLocation = stockLocation;
    }

    public EntityId ProjectId { get; }
    public ConstructionDefinition Definition { get; }
    public EntityId OwnerActorId { get; }
    public EntityId BuilderActorId { get; }
    public EntityId QuestId { get; }
    public EntityId ProjectCustodianId { get; }
    public EntityId ReservationId { get; }
    public SubunitPosition SiteFeet { get; }
    public SubunitPosition WorkFeet { get; }
    public ReadOnlyCollection<GroundAccessRequirement> RequiredAccess { get; }
    public ulong Revision { get; }
    public ExposureProgressState Progress { get; }
    public InstalledStructure? Installed { get; }
    public ConstructionStockLocation StockLocation { get; }
    public bool IsCompleted => Installed is not null;

    internal ConstructionProject WithProgress(ExposureProgress progress, InstalledStructure? installed) =>
        new(ProjectId, Definition, OwnerActorId, BuilderActorId, QuestId, ProjectCustodianId, ReservationId,
            SiteFeet, WorkFeet, RequiredAccess, checked(Revision + 1), progress.Capture(), installed, StockLocation);
}

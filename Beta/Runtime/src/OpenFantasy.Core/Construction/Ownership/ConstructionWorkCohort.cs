using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction.Ownership;

public sealed record ConstructionOwnerReference(ConstructionOwnerKey Key, ContentHash Hash);

/// <summary>
/// Read-only work qualification from selected stored owners under one completely admitted source.
/// Capture retains geometry and hash-bound completeness witnesses, never inventory/production detail.
/// The caller must serialize current-head comparison with these reads; this is not publication.
/// </summary>
public sealed class ConstructionWorkCohort
{
    private readonly ConstructionSiteEvidence _site;
    private readonly WorldActionCommand _command;
    private readonly TerrainScope _scope;
    private readonly ulong _custodyRevision;
    private readonly ulong _constructionRevision;
    private readonly EntityId _definitionId;
    private readonly EntityId _projectId;
    private readonly EntityId _reservationId;
    private readonly EntityId[] _reservedLots;

    private ConstructionWorkCohort(WorldSnapshot world, WorldActionCommand command)
    {
        if (world.ProfileVersion != 5 || command.Intent is not WorkConstructionIntent intent
            || world.Construction?.FindProject(intent.ProjectId) is not { } project)
            throw new ArgumentException("Stored work qualification requires an actual profile-five construction project.");
        var directory = ConstructionOwnerDirectory.Capture(world);
        _site = ConstructionSiteEvidence.Capture(world, command);
        _command = command;
        _scope = world.Space.Scope;
        _custodyRevision = world.Inventory.Custody.Revision;
        _constructionRevision = world.Construction.Revision;
        _definitionId = project.Definition.DefinitionId;
        _projectId = project.ProjectId;
        _reservationId = project.ReservationId;
        var reserved = world.Inventory.Custody.Lots.Where(value => value.ReservedBy == project.ReservationId).ToArray();
        _reservedLots = reserved.Select(value => value.LotId).Order().ToArray();
        var custodianIds = reserved.Select(value => value.CustodianId)
            .Append(project.ProjectCustodianId).Append(world.GetResident(command.ActorId).InventoryCustodianId).Distinct();
        HashSet<ConstructionOwnerKey> required = [new(ConstructionOwnerKind.Actor, command.ActorId),
            new(ConstructionOwnerKind.Control, command.ActorId), new(ConstructionOwnerKind.Project, project.ProjectId),
            new(ConstructionOwnerKind.ConstructionDefinition, _definitionId),
            new(ConstructionOwnerKind.Progression, world.WorldId), new(ConstructionOwnerKind.Quests, world.WorldId),
            new(ConstructionOwnerKind.Dialogues, world.WorldId)];
        foreach (var custodian in custodianIds) required.Add(new(ConstructionOwnerKind.Custodian, custodian));
        var selected = directory.Owners.Where(value => required.Contains(value.Key))
            .Select(value => new ConstructionOwnerReference(value.Key, value.Hash)).ToArray();
        if (selected.Length != required.Count)
            throw new ArgumentException("The admitted source cannot supply every required work owner.");
        RequiredOwners = Array.AsReadOnly(selected);
        DirectoryHash = directory.Hash;
        SourceWorldHash = world.ContentHash;
        SourceRevision = world.Revision;
        CompleteOwnerCount = directory.Owners.Count;
    }

    public ContentHash DirectoryHash { get; }
    public ContentHash SourceWorldHash { get; }
    public ulong SourceRevision { get; }
    public int CompleteOwnerCount { get; }
    public ReadOnlyCollection<ConstructionOwnerReference> RequiredOwners { get; }

    public static ConstructionWorkCohort Capture(WorldSnapshot world, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        return new(world, command);
    }

    /// <summary>No clock, mutation, receipt or material completion; exact current-head freshness is mandatory.</summary>
    public ConstructionWorkAssessment Assess(ContentHash currentDirectory, Func<ContentHash, byte[]> readOwner)
    {
        ArgumentNullException.ThrowIfNull(readOwner);
        if (currentDirectory != DirectoryHash)
            throw new InvalidOperationException("The admitted work directory changed; capture current dependencies again.");
        try
        {
            Dictionary<ConstructionOwnerKey, byte[][]> fields = [];
            long bytes = 0;
            foreach (var reference in RequiredOwners)
            {
                byte[] payload = readOwner(reference.Hash) ?? throw new FormatException("A required work owner is missing.");
                bytes = checked(bytes + payload.Length);
                if (bytes > ConstructionOwnerDirectory.MaximumTotalPayloadByteLength)
                    throw new FormatException("Selected work owners exceed their aggregate byte envelope.");
                fields.Add(reference.Key, StoredConstructionOwners.ReadFields(payload, reference.Key, _scope, reference.Hash));
            }
            var result = Qualify(fields, _command, _constructionRevision, true, false);
            return new(SourceWorldHash, SourceRevision, _command.CommandId, result.Rejection,
                result.Work?.Progress.Capture(), result.Work is { } work
                    && work.Progress.CompletedUnits >= work.Project.Definition.RequiredWork.Value);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Stored work dependencies violate their admitted source.", error);
        }
    }
    internal void ValidateCommand(WorldActionCommand command)
    {
        if (command.ActorId != _command.ActorId || command.Intent is not WorkConstructionIntent intent
            || intent.ProjectId != _projectId)
            throw new InvalidOperationException("Work is outside the captured actor/project interval.");
    }

    internal ConstructionWorkQualification Qualify(IReadOnlyDictionary<ConstructionOwnerKey, byte[][]> fields,
        WorldActionCommand command, ulong constructionRevision, bool advancing, bool held)
    {
        ValidateCommand(command);
        var owners = new StoredConstructionWorkOwners(_scope, _custodyRevision, constructionRevision,
            _projectId, _definitionId, _reservationId, _reservedLots, fields);
        var source = new StoredConstructionDecisionSource(_scope, command, fields);
        _site.ValidateProject(owners.FindProject(_projectId)!);
        return ConstructionWorkQualification.Evaluate(source, owners, _site, command, advancing, held);
    }

}

/// <summary>Proposed qualification only. RequiresCompletion means a larger material cohort is still necessary.</summary>
public sealed record ConstructionWorkAssessment(ContentHash SourceWorldHash, ulong SourceRevision,
    EntityId CommandId, WorldActionRejection Rejection, ExposureProgressState? ProposedProgress, bool RequiresCompletion);

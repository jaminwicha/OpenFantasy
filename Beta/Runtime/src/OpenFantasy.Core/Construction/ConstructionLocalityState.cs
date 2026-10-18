using System.Collections.Immutable;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// Complete nonterrain owners of an admitted profile-five locality during one project's work interval.
/// Selected work can prepare successive candidates without terrain detail. This is not a durable world,
/// a migration of later profiles, or permission to omit final complete-world and storage admission.
/// </summary>
public sealed class ConstructionLocalityState
{
    private readonly ConstructionSiteEvidence _site;
    private readonly ImmutableHashSet<EntityId> _identities;

    private ConstructionLocalityState(WorldSnapshot world, EntityId projectId, int maximumMetadataPages)
    {
        if (world.ProfileVersion != 5 || world.Space.GroundSupport != GroundSupportMode.TerrainOnly
            || world.Space.StructureSupport != StructureSupportMode.TerrainOnly
            || world.Production is null || world.Production.IsContinued || world.Production.MaterialHistory is not null
            || world.Construction?.FindProject(projectId) is not { } project
            || world.Progression is null || world.Quests is null)
        {
            throw new ArgumentException("Construction locality capture requires the complete uncontinued profile-five owners and terrain-only support.", nameof(world));
        }
        SourceWorldHash = world.ContentHash;
        SourceRevision = world.Revision;
        AdmissionVersion = world.AdmissionVersion;
        ProjectId = projectId;
        Inventory = world.Inventory;
        Production = world.Production;
        Construction = world.Construction;
        Progression = world.Progression;
        Quests = world.Quests;
        Gathering = world.Gathering;
        Residents = world.Residents;
        Dialogues = world.Dialogues;
        Calendar = world.Calendar;
        Revision = world.Revision;
        AdvancingSteps = world.AdvancingSteps;
        Actors = world.Space.Actors;
        Terrain = Array.AsReadOnly(world.Space.Regions.Select(TerrainRegionReference.Capture).ToArray());
        Occupancy = SpatialOccupancyDirectory.Create(world.Space.Scope,
            Actors.Select(SpatialOccupancyReference.Capture).Concat(world.Space.Structures.Select(value =>
                SpatialOccupancyReference.Capture(world.Space.Scope, value))),
            LocalSpatialSnapshot.MaximumActors + LocalSpatialSnapshot.MaximumStructures, maximumMetadataPages);
        // The probe identity has no authority; every real command rechecks its own identities.
        _site = ConstructionSiteEvidence.Capture(world.Space, project, project.Definition.DefinitionId);
        var reservations = world.IdentityIndex.CaptureReservations();
        if (reservations.Families.Length != 0)
            throw new ArgumentException("This locality cannot drop encounter identity families.", nameof(world));
        _identities = reservations.Identities.ToImmutableHashSet();
    }

    private ConstructionLocalityState(ConstructionLocalityState source, ConstructionWorkResult work,
        WorldExplorationBoundary? boundary, IEnumerable<WorldCommittedIdentity> identities)
    {
        SourceWorldHash = source.SourceWorldHash;
        SourceRevision = source.SourceRevision;
        AdmissionVersion = source.AdmissionVersion;
        ProjectId = source.ProjectId;
        Inventory = work.CandidateInventory;
        Production = work.CandidateProduction;
        Construction = work.CandidateConstruction;
        Progression = source.Progression;
        Quests = source.Quests;
        Dialogues = source.Dialogues;
        Revision = checked(source.Revision + 1);
        Calendar = boundary?.Calendar ?? source.Calendar;
        AdvancingSteps = boundary?.AdvancingSteps ?? source.AdvancingSteps;
        // Same due/process order as the profile-five world interpreter: work, gathering, residents.
        Gathering = boundary is null ? source.Gathering : source.Gathering?.AdvanceStep(AdvancingSteps);
        Residents = boundary is null ? source.Residents : Array.AsReadOnly(source.Residents.Select(value => value.ExperienceStep()).ToArray());
        Actors = source.Actors;
        Terrain = source.Terrain;
        Occupancy = work.CandidateStructure is null ? source.Occupancy : source.Occupancy.Apply(source.Occupancy.Revision,
            [new(null, SpatialOccupancyReference.Capture(source.Occupancy.Scope, work.CandidateStructure))]);
        _site = source._site;
        _identities = source._identities.Union(identities.Select(value => value.Identity));
    }

    public ContentHash SourceWorldHash { get; }
    public ulong SourceRevision { get; }
    public ContentHash AdmissionVersion { get; }
    public EntityId ProjectId { get; }
    public CountedAuthorityState Inventory { get; }
    public ProductionSnapshot Production { get; }
    public ConstructionSnapshot Construction { get; }
    public ProgressionSnapshot Progression { get; }
    public QuestSnapshot Quests { get; }
    public GatheringSnapshot? Gathering { get; }
    public ReadOnlyCollection<WorldResident> Residents { get; }
    public ReadOnlyCollection<DialogueSession> Dialogues { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }
    public ReadOnlyCollection<TerrainRegionReference> Terrain { get; }
    public SpatialOccupancyDirectory Occupancy { get; }
    public WorldInstant Calendar { get; }
    public ulong Revision { get; }
    public ulong AdvancingSteps { get; }
    public int RetainedIdentityCount => _identities.Count;

    public static ConstructionLocalityState Capture(WorldSnapshot world, EntityId projectId, int maximumMetadataPages)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new(world, projectId, maximumMetadataPages);
    }

    /// <summary>
    /// Interprets one selected work command against current owner-established geometry metadata.
    /// Metadata freshness, history lookup and final publication must serialize at the actual owner.
    /// Unsupported actions require their ordinary execution path and a new admitted capture.
    /// </summary>
    public ConstructionLocalityStep PrepareStep(WorldStepBatch batch, SpatialOccupancyDirectory currentOccupancy,
        IEnumerable<TerrainRegionReference> currentTerrain, Func<EntityId, bool> historicallyOccupied)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(historicallyOccupied);
        if (batch.ProfileVersion != 5 || batch.WorldId != Inventory.WorldId || batch.BranchId != Inventory.BranchId
            || batch.ExpectedRevision != Revision || batch.ExpectedAdvancingSteps != AdvancingSteps
            || batch.Kind is not WorldBatchKind.AdvancingStep and not WorldBatchKind.HeldInteraction
            || (batch.Kind == WorldBatchKind.HeldInteraction) != (Dialogues.Count != 0)
            || batch.Movement.Count != 0 || batch.Actions.Count != 1
            || batch.Actions[0].Intent is not WorkConstructionIntent intent || intent.ProjectId != ProjectId)
        {
            throw new InvalidOperationException("The selected batch is outside this exact construction locality interval.");
        }
        ValidateGeometry(currentOccupancy, currentTerrain);
        var project = Construction.FindProject(ProjectId)!;
        if (!project.IsCompleted)
        {
            _site.ValidateProject(project);
            _site.ValidateCurrent(currentOccupancy, Terrain, GroundSupportMode.TerrainOnly, StructureSupportMode.TerrainOnly);
        }
        ulong sequence = checked(Revision + 1);
        var reservations = new WorldStepIdentityReservations(identity => ContainsIdentity(identity) || historicallyOccupied(identity));
        var command = batch.Actions[0];
        reservations.Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        reservations.Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
        var work = ConstructionWorkKernel.Plan(new ConstructionLocalitySource(this), Inventory, Production,
            Construction, _site, command, batch.Kind == WorldBatchKind.AdvancingStep, Dialogues.Count != 0,
            reservations.Contains, false, null);
        var receipt = WorldActionReceipt.Create(command, sequence, work.Rejection);
        reservations.Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
        foreach (var identity in work.CreatedIds) reservations.Reserve(identity, WorldCommittedIdentityKind.SpawnedEntity);
        WorldExplorationBoundary? boundary = batch.Kind == WorldBatchKind.AdvancingStep
            ? new WorldExplorationBoundary(Calendar, AdvancingSteps, null, null) : null;
        var candidate = new ConstructionLocalityState(this, work, boundary, reservations.Identities);
        return new(this, candidate, receipt, reservations.Identities);
    }

    /// <summary>Loads exact original terrain and performs normal complete world admission; no publication.</summary>
    public WorldSnapshot Rehydrate(IEnumerable<TerrainRegionSnapshot> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var loaded = regions.Take(LocalSpatialSnapshot.MaximumRegions + 1).ToArray();
        ValidateTerrain(loaded.Select(TerrainRegionReference.Capture));
        var space = new LocalSpatialSnapshot(Occupancy.Scope, loaded, Actors, Construction.GetStructures());
        return new(Inventory, space, Residents, AdmissionVersion, Calendar, Revision, AdvancingSteps,
            Dialogues, Gathering, Production, Progression, Quests, Construction);
    }

    internal bool ContainsIdentity(EntityId identity) => _identities.Contains(identity);

    private void ValidateGeometry(SpatialOccupancyDirectory current, IEnumerable<TerrainRegionReference> terrain)
    {
        ArgumentNullException.ThrowIfNull(current);
        ValidateTerrain(terrain);
        var expected = Actors.Select(SpatialOccupancyReference.Capture).Concat(Construction.GetStructures()
            .Select(value => SpatialOccupancyReference.Capture(Occupancy.Scope, value))).ToArray();
        if (current.Scope != Occupancy.Scope || current.EntityCount != expected.Length
            || expected.Any(value => current.Find(value.EntityId) != value))
        {
            throw new InvalidOperationException("Construction locality geometry changed; prepare from the current admitted owners.");
        }
    }

    private void ValidateTerrain(IEnumerable<TerrainRegionReference> terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var current = terrain.Take(LocalSpatialSnapshot.MaximumRegions + 1).ToArray();
        if (current.Length != Terrain.Count || Terrain.Any(value => current.Count(other => other == value) != 1))
            throw new InvalidOperationException("Construction locality requires its exact complete terrain references.");
    }
}

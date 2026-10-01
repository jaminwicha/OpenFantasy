using OpenFantasy.Core.Care;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Complete admitted state of the current exploration profile. Presence, inventory, dialogue and time
/// change together; this bounded locality is not the unimplemented global simulation's complete schema.
/// </summary>
public sealed class WorldSnapshot
{
    private readonly Dictionary<EntityId, WorldResident> _residents;
    private ContentHash? _contentHash;
    private readonly EncounterActionSnapshot? _standaloneEncounterActions;

    public WorldSnapshot(CountedAuthorityState inventory, LocalSpatialSnapshot space,
        IEnumerable<WorldResident> residents, ContentHash admissionVersion, WorldInstant calendar,
        ulong revision, ulong advancingSteps, IEnumerable<DialogueSession>? dialogues = null,
        GatheringSnapshot? gathering = null, ProductionSnapshot? production = null,
        ProgressionSnapshot? progression = null, QuestSnapshot? quests = null, ConstructionSnapshot? construction = null,
        PreparedWatershed? watershed = null, PreparedWatershedNetwork? watershedNetwork = null,
        LiquidReservoirState? reservoirs = null, ReservoirTransportState? transport = null, ExcavationSnapshot? excavation = null,
        ConstructionPhysicalSnapshot? constructionPhysical = null, ConstructionLoadPolicy? structuralPolicy = null,
        StructuralSettlementPolicy? structuralSettlementPolicy = null,
        ConsequenceExecutionPolicy? consequencePolicy = null, PendingExcavation? pendingExcavation = null,
        CoupledSettlementPolicy? coupledSettlementPolicy = null,
        ConstructionDescentMode descentMode = ConstructionDescentMode.CommonTrajectory,
        ReservoirSealPolicy? reservoirSeals = null, LiquidCellState? liquidCells = null, WorldPacingState? pacing = null, EncounterActionSnapshot? encounterActions = null,
        EncounterLifecycleSnapshot? encounterLifecycle = null, CareSnapshot? care = null, WorldResourceHistory? resourceHistory = null,
        WorldCoordinatedContinuation? coordination = null, EncounterRetreatState? retreat = null)
        : this(inventory, space, residents, admissionVersion, calendar, revision, advancingSteps, dialogues,
            gathering, production, progression, quests, construction, watershed, watershedNetwork, reservoirs, transport, excavation, constructionPhysical, structuralPolicy, structuralSettlementPolicy, consequencePolicy, pendingExcavation, coupledSettlementPolicy, descentMode, reservoirSeals, liquidCells, pacing, encounterActions, encounterLifecycle, care, resourceHistory, coordination, retreat, null)
    {
    }

    private WorldSnapshot(CountedAuthorityState inventory, LocalSpatialSnapshot space,
        IEnumerable<WorldResident> residents, ContentHash admissionVersion, WorldInstant calendar,
        ulong revision, ulong advancingSteps, IEnumerable<DialogueSession>? dialogues, GatheringSnapshot? gathering,
        ProductionSnapshot? production, ProgressionSnapshot? progression, QuestSnapshot? quests,
        ConstructionSnapshot? construction, PreparedWatershed? watershed, PreparedWatershedNetwork? watershedNetwork,
        LiquidReservoirState? reservoirs, ReservoirTransportState? transport, ExcavationSnapshot? excavation,
        ConstructionPhysicalSnapshot? constructionPhysical, ConstructionLoadPolicy? structuralPolicy,
        StructuralSettlementPolicy? structuralSettlementPolicy, ConsequenceExecutionPolicy? consequencePolicy,
        PendingExcavation? pendingExcavation, CoupledSettlementPolicy? coupledSettlementPolicy,
        ConstructionDescentMode descentMode, ReservoirSealPolicy? reservoirSeals, LiquidCellState? liquidCells,
        WorldPacingState? pacing, EncounterActionSnapshot? encounterActions, EncounterLifecycleSnapshot? encounterLifecycle, CareSnapshot? care, WorldResourceHistory? resourceHistory, WorldCoordinatedContinuation? coordination, EncounterRetreatState? retreat, WorldIdentityIndex? previous)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(residents);
        ArgumentNullException.ThrowIfNull(admissionVersion);
        if (production?.IsContinued == true && (coordination is null || !production.Continuation!.SupportsCompleteWorld))
        {
            throw new ArgumentException("World admission requires a coordinated material, care and resource continuation profile.", nameof(production));
        }
        if (care?.Continuation is not null && coordination is null)
        {
            throw new ArgumentException("Continued care requires a coordinated material, care and resource world profile.", nameof(care));
        }
        if ((production?.SupportsConsumption == true) != (care is not null) || care is not null && encounterLifecycle is null
            || (care is null) != (resourceHistory is null))
        {
            throw new ArgumentException("Administered material requires a complete care-enabled world profile.", nameof(production));
        }
        if (space.Scope.WorldId != inventory.WorldId || space.Scope.BranchId != inventory.BranchId)
        {
            throw new ArgumentException("Presence and inventory must belong to the same world and branch.");
        }
        _residents = [];
        foreach (WorldResident resident in residents)
        {
            ArgumentNullException.ThrowIfNull(resident);
            if (_residents.Count == WorldStepBatch.MaximumActorsPerBatch || !_residents.TryAdd(resident.ActorId, resident))
            {
                throw new ArgumentException("Residents need bounded unique actor identities.");
            }
            if (!inventory.Policies.Any(policy => policy.CustodianId == resident.InventoryCustodianId
                && policy.OwnerActorId == resident.ActorId))
            {
                throw new ArgumentException("Each resident's primary inventory requires its own admitted custody policy.");
            }
        }
        if (_residents.Count == 0 || !inventory.Actors.SequenceEqual(_residents.Keys.Order())
            || !space.Actors.Select(actor => actor.ActorId).SequenceEqual(_residents.Keys.Order()))
        {
            throw new ArgumentException("The local actor, resident and counted authority sets must be complete and equal.");
        }
        var sessions = new SortedDictionary<EntityId, DialogueSession>();
        HashSet<EntityId> listeners = [];
        foreach (DialogueSession session in dialogues ?? [])
        {
            ArgumentNullException.ThrowIfNull(session);
            if (sessions.Count == 4 || !sessions.TryAdd(session.DialogueId, session)
                || !listeners.Add(session.ListenerActorId) || !_residents.ContainsKey(session.ListenerActorId)
                || !_residents.TryGetValue(session.SpeakerActorId, out WorldResident? speaker)
                || speaker.OpeningUtterance?.DefinitionId != session.UtteranceDefinitionId
                || session.StartedAt > calendar)
            {
                throw new ArgumentException("The active dialogue set violates its addressed utterance or clock binding.");
            }
        }
        Inventory = inventory;
        Space = space;
        AdmissionVersion = admissionVersion;
        Calendar = calendar;
        Revision = revision;
        AdvancingSteps = advancingSteps;
        Residents = Array.AsReadOnly(_residents.Values.OrderBy(resident => resident.ActorId).ToArray());
        Dialogues = Array.AsReadOnly(sessions.Values.ToArray());
        Gathering = gathering;
        Production = production;
        Progression = progression;
        Quests = quests;
        Construction = construction;
        Watershed = watershed;
        Transport = transport;
        if (transport is not null)
        {
            if (reservoirs is not null || transport.Boundary != calendar)
            {
                throw new ArgumentException("Transport exclusively owns the physical reservoirs at the world's exact calendar boundary.");
            }
            reservoirs = transport.Reservoirs;
        }
        Reservoirs = reservoirs;
        if (reservoirs is not null)
        {
            if (watershed is not null || watershedNetwork is not null || construction is null)
            {
                throw new ArgumentException("Reservoir ownership requires construction and exclusively owns its network.");
            }
            WorldReservoirBinding.Validate(reservoirs, inventory, production);
        }
        WatershedNetwork = reservoirs?.Capacity.Domain.Network ?? watershedNetwork;
        if (watershed is not null && watershedNetwork is not null)
        {
            throw new ArgumentException("A world cannot independently own both a single basin and a watershed network.");
        }
        if ((watershed is not null || watershedNetwork is not null) && construction is null)
        {
            throw new ArgumentException("The watershed world profile requires its complete construction basis.");
        }
        if (construction is not null && quests is null || construction is null && space.Structures.Count != 0)
        {
            throw new ArgumentException("Placed structures require their complete construction and quest profile.");
        }
        if ((progression is null) != (quests is null) || quests is not null && production is null)
        {
            throw new ArgumentException("Quest worlds require both funded progression and retained production components.");
        }
        if (production is not null && gathering is null)
        {
            throw new ArgumentException("The production profile requires its retained gathering component.");
        }
        Excavation = excavation;
        ConstructionPhysical = constructionPhysical;
        StructuralPolicy = structuralPolicy;
        StructuralSettlementPolicy = structuralSettlementPolicy;
        ConsequencePolicy = consequencePolicy;
        PendingExcavation = pendingExcavation;
        CoupledSettlementPolicy = coupledSettlementPolicy;
        DescentMode = descentMode;
        ReservoirSeals = reservoirSeals;
        LiquidCells = liquidCells;
        Pacing = pacing;
        if (encounterActions is not null && encounterLifecycle is not null)
        {
            throw new ArgumentException("Lifecycle exclusively owns its current and retained encounter actions.");
        }
        _standaloneEncounterActions = encounterActions;
        EncounterLifecycle = encounterLifecycle;
        Care = care;
        ResourceHistory = resourceHistory;
        Coordination = coordination;
        Retreat = retreat;
        if (liquidCells is not null && reservoirSeals is null)
        {
            throw new ArgumentException("Detailed liquid placement requires the complete contained reservoir owner.");
        }
        bool terrainLiquid = liquidCells?.Topology is not null;
        if (terrainLiquid && (excavation is null || space.GroundSupport != GroundSupportMode.TerrainOnly))
        {
            throw new ArgumentException("Terrain-bound liquid requires explicit excavation and dry terrain-supported actors.");
        }
        if (!terrainLiquid && excavation?.Sites.Any(site => site.ReachMode != ExcavationReachMode.SameLevelSide) == true)
        {
            throw new ArgumentException("Raised-bank extraction requires the terrain-bound liquid world interpretation.");
        }
        if (reservoirSeals is not null && (reservoirs is null || transport is not null || excavation is not null && !terrainLiquid
            || constructionPhysical is not null || structuralPolicy is not null || structuralSettlementPolicy is not null
            || consequencePolicy is not null || pendingExcavation is not null || coupledSettlementPolicy is not null))
        {
            throw new ArgumentException("Owned reservoir seals require fixed reservoir ownership without transport, extraction or physical structural consequences.");
        }
        if (!Enum.IsDefined(descentMode)
            || descentMode == ConstructionDescentMode.EventOrdered && coupledSettlementPolicy is null)
        {
            throw new ArgumentException("Event-ordered descent requires the complete coupled construction profile.", nameof(descentMode));
        }
        if ((coupledSettlementPolicy is not null) != (space.StructureSupport == StructureSupportMode.TerrainAndConstruction)
            || coupledSettlementPolicy is not null && consequencePolicy is null)
        {
            throw new ArgumentException("Coupled construction support requires its complete consequence policy and explicit spatial interpretation.");
        }
        if (consequencePolicy is not null && structuralSettlementPolicy is null
            || pendingExcavation is not null && consequencePolicy is null)
        {
            throw new ArgumentException("Pending consequences require the complete settlement and execution policies.");
        }
        if (structuralSettlementPolicy is not null && structuralPolicy is null)
        {
            throw new ArgumentException("Settlement requires the complete structural policy owner.");
        }
        if (constructionPhysical is not null && (construction is null || excavation is null
            || space.GroundSupport != GroundSupportMode.SteppedTerrainAndConstruction
            || constructionPhysical.ConstructionHash != construction.ContentHash
            || constructionPhysical.Revision > revision))
        {
            throw new ArgumentException("Physical construction requires its exact fabrication in a complete stepped dry world.");
        }
        if (space.GroundSupport != GroundSupportMode.TerrainOnly && excavation is null)
        {
            throw new ArgumentException("Constructed actor support requires the complete dry extraction profile.");
        }
        if (excavation is not null && (construction is null || watershed is not null || watershedNetwork is not null || reservoirs is not null && !terrainLiquid))
        {
            throw new ArgumentException("Local extraction exclusively requires its dry construction profile.");
        }
        if (structuralPolicy is not null && (constructionPhysical is null || structuralPolicy.Scope != space.Scope))
        {
            throw new ArgumentException("Structural policy requires complete physical construction in its exact terrain scope.");
        }
        PendingExcavation?.ValidateForWorld(this);
        WorldPacingAdmission.Validate(this);
        WorldCareAdmission.Validate(this);
        WorldResourceAdmission.Validate(this);
        WorldCoordinatedAdmission.Validate(this);
        WorldEncounterActionAdmission.Validate(this);
        WorldEncounterLifecycleAdmission.Validate(this);
        WorldEncounterRetreatAdmission.Validate(this);
        IdentityIndex = WorldAdmissionValidator.Validate(this, previous);
        if (structuralPolicy is not null)
        {
            WorldStructuralBinding.ValidateStable(this);
        }
        if (reservoirSeals is not null)
        {
            WorldReservoirContainmentBinding.Validate(this);
        }
        if (liquidCells is not null)
        {
            WorldLiquidCellBinding.Validate(this);
        }
    }

    public EntityId WorldId => Inventory.WorldId;
    public EntityId BranchId => Inventory.BranchId;
    public CountedAuthorityState Inventory { get; }
    public LocalSpatialSnapshot Space { get; }
    public ContentHash AdmissionVersion { get; }
    public WorldInstant Calendar { get; }
    public ulong Revision { get; }

    // Domain revisions count actor operations, while a complete world revision counts batches.
    // Legacy interpretations retain their original single-operation admission bound.
    internal UInt128 MaximumActionRevision => (UInt128)Revision
        * (uint)(Coordination is null ? 1 : WorldStepBatch.MaximumActorsPerBatch);
    public ulong AdvancingSteps { get; }
    public ReadOnlyCollection<WorldResident> Residents { get; }
    public ReadOnlyCollection<DialogueSession> Dialogues { get; }
    public GatheringSnapshot? Gathering { get; }
    public ProductionSnapshot? Production { get; }
    public ProgressionSnapshot? Progression { get; }
    public QuestSnapshot? Quests { get; }
    public ConstructionSnapshot? Construction { get; }
    public PreparedWatershed? Watershed { get; }
    public PreparedWatershedNetwork? WatershedNetwork { get; }
    public LiquidReservoirState? Reservoirs { get; }
    public ReservoirTransportState? Transport { get; }
    public ReservoirSealPolicy? ReservoirSeals { get; }
    public LiquidCellState? LiquidCells { get; }
    public ExcavationSnapshot? Excavation { get; }
    public ConstructionPhysicalSnapshot? ConstructionPhysical { get; }
    public ConstructionLoadPolicy? StructuralPolicy { get; }
    public StructuralSettlementPolicy? StructuralSettlementPolicy { get; }
    public ConsequenceExecutionPolicy? ConsequencePolicy { get; }
    public PendingExcavation? PendingExcavation { get; }
    public CoupledSettlementPolicy? CoupledSettlementPolicy { get; }
    public ConstructionDescentMode DescentMode { get; }
    public WorldPacingState? Pacing { get; }
    public CareSnapshot? Care { get; }
    public WorldResourceHistory? ResourceHistory { get; }
    /// <summary>Non-null selects the explicit coordinated material, care and resource interpretation.</summary>
    public WorldCoordinatedContinuation? Coordination { get; }
    public EncounterLifecycleSnapshot? EncounterLifecycle { get; }
    /// <summary>Non-null selects the explicit retreat interpretation (profile 34) over the local-market world.</summary>
    public EncounterRetreatState? Retreat { get; }
    public EncounterActionSnapshot? EncounterActions => EncounterLifecycle is { } lifecycle
        ? lifecycle.HasEncounter ? lifecycle.Actions : null : _standaloneEncounterActions;
    public ulong ProcessIntervals => Pacing?.ProcessIntervals ?? AdvancingSteps;
    public ushort ProfileVersion => Coordination?.Markets is not null
        ? Retreat is not null ? WorldEncounterRetreatAdmission.ProfileVersion : WorldMarketAdmission.ProfileVersion
        : Coordination?.PaidExchange is not null ? WorldPaidExchangeAdmission.ProfileVersion
        : Coordination?.Proposals is not null ? (ushort)30 : Coordination?.ConsentedMaterial is not null ? (ushort)28 : Coordination is not null ? (ushort)27 : Care?.SupportsOrdinary == true ? (ushort)26 : Care is not null ? (ushort)25 : EncounterLifecycle is not null ? (ushort)24 : EncounterActions is not null ? (ushort)23 : Pacing is not null ? (ushort)22 : BaseProfileVersion;
    internal ushort BaseProfileVersion => LiquidCells?.Topology is not null ? (ushort)21 : LiquidCells is not null ? (ushort)20 : ReservoirSeals is not null ? (ushort)19
        : DescentMode == ConstructionDescentMode.EventOrdered ? (ushort)18
        : CoupledSettlementPolicy is not null ? (ushort)17 : ConsequencePolicy is not null ? (ushort)16 : StructuralSettlementPolicy is not null ? (ushort)15 : StructuralPolicy is not null ? (ushort)14
        : ConstructionPhysical is not null ? (ushort)13
        : Space.GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction ? (ushort)12
        : Space.GroundSupport == GroundSupportMode.TerrainAndConstruction ? (ushort)11
        : Excavation is not null ? (ushort)10 : Transport is not null ? (ushort)9 : Reservoirs is not null ? (ushort)8 : WatershedNetwork is not null ? (ushort)7 : Watershed is not null ? (ushort)6
        : Construction is not null ? (ushort)5 : Quests is not null ? (ushort)4
        : Production is not null ? (ushort)3 : Gathering is null ? (ushort)1 : (ushort)2;
    public WorldIdentityIndex IdentityIndex { get; }
    /// <summary>The canonical digest belongs to this immutable root; successors own independent caches.</summary>
    public ContentHash ContentHash
    {
        get
        {
            ContentHash? cached = Volatile.Read(ref _contentHash);
            if (cached is not null) return cached;
            ContentHash computed = ContentHash.Compute(WorldSnapshotCodec.Encode(this));
            // Concurrent first readers may compute independently; publish one complete immutable digest.
            return Interlocked.CompareExchange(ref _contentHash, computed, null) ?? computed;
        }
    }
    public bool HasReadingHold => Dialogues.Count != 0;
    public WorldResident GetResident(EntityId actorId) => _residents.TryGetValue(actorId, out WorldResident? resident)
        ? resident : throw new ArgumentException("The actor is not admitted in this locality.", nameof(actorId));

    internal WorldSnapshot WithState(CountedAuthorityState inventory, LocalSpatialSnapshot space,
        IEnumerable<WorldResident> residents, WorldInstant calendar, ulong revision, ulong advancingSteps,
        IEnumerable<DialogueSession> dialogues, GatheringSnapshot? gathering, ProductionSnapshot? production,
        ProgressionSnapshot? progression, QuestSnapshot? quests, ConstructionSnapshot? construction,
        LiquidReservoirState? reservoirs, ReservoirTransportState? transport, ExcavationSnapshot? excavation,
        ConstructionPhysicalSnapshot? constructionPhysical, LiquidCellState? liquidCells = null, WorldPacingState? pacing = null, EncounterActionSnapshot? encounterActions = null,
        EncounterLifecycleSnapshot? encounterLifecycle = null, CareSnapshot? care = null, WorldResourceHistory? resourceHistory = null,
        WorldCoordinatedContinuation? coordination = null, EncounterRetreatState? retreat = null)
    {
        if (PendingExcavation is not null)
        {
            throw new InvalidOperationException("Resolve or retain the pending interval before changing world state.");
        }
        if (space.GroundSupport != Space.GroundSupport || space.StructureSupport != Space.StructureSupport)
        {
            throw new ArgumentException("A successor must retain its admitted ground support rules.");
        }
        if ((Excavation is null) != (excavation is null))
        {
            throw new ArgumentException("A successor must retain its extraction ownership.");
        }
        if ((ConstructionPhysical is null) != (constructionPhysical is null))
        {
            throw new ArgumentException("A successor must retain physical construction ownership.");
        }
        if ((Reservoirs is null) != (reservoirs is null))
        {
            throw new ArgumentException("A world successor cannot add or remove physical reservoir ownership.");
        }
        if ((Transport is null) != (transport is null) || transport is not null && transport.Reservoirs.ContentHash != reservoirs?.ContentHash)
        {
            throw new ArgumentException("A world successor must retain the sole transport and physical owner together.");
        }
        liquidCells ??= LiquidCells;
        if ((LiquidCells is null) != (liquidCells is null))
        {
            throw new ArgumentException("A world successor must retain its detailed liquid placement owner.");
        }
        pacing ??= Pacing;
        if ((pacing is null) != (Pacing is null))
        {
            throw new ArgumentException("A successor must retain its process clock owner.");
        }
        encounterLifecycle ??= EncounterLifecycle;
        if ((encounterLifecycle is null) != (EncounterLifecycle is null))
        {
            throw new ArgumentException("A successor must retain its permanent encounter lifecycle owner.");
        }
        if (encounterLifecycle is null)
        {
            encounterActions ??= _standaloneEncounterActions;
            if ((encounterActions is null) != (_standaloneEncounterActions is null))
            {
                throw new ArgumentException("A successor must retain its standalone encounter action owner.");
            }
        }
        else if (encounterActions is not null)
        {
            throw new ArgumentException("Publish encounter actions through their lifecycle owner.");
        }
        resourceHistory ??= ResourceHistory;
        if ((resourceHistory is null) != (ResourceHistory is null))
            throw new ArgumentException("A successor must retain its resource history owner.");
        care ??= Care;
        if ((care is null) != (Care is null))
            throw new ArgumentException("A successor must retain its care owner.");
        if (care is not null && !ReferenceEquals(care, Care)) Care!.ValidateSuccessor(care);
        retreat ??= Retreat;
        if ((retreat is null) != (Retreat is null) || retreat is not null && retreat.RulesPin != Retreat!.RulesPin)
            throw new ArgumentException("A successor must retain its exact retreat interpretation.");
        return new(inventory, space, residents, AdmissionVersion, calendar, revision, advancingSteps, dialogues,
            gathering, production, progression, quests, construction, Watershed, reservoirs is null ? WatershedNetwork : null, transport is null ? reservoirs : null, transport, excavation, constructionPhysical, StructuralPolicy, StructuralSettlementPolicy, ConsequencePolicy, null, CoupledSettlementPolicy, DescentMode, ReservoirSeals, liquidCells, pacing, encounterActions, encounterLifecycle, care, resourceHistory, coordination ?? Coordination, retreat, IdentityIndex);
    }
    internal WorldSnapshot WithPending(PendingExcavation? pending, ulong revision) => new(
        Inventory, Space, Residents, AdmissionVersion, Calendar, revision, AdvancingSteps,
        Dialogues, Gathering, Production, Progression, Quests, Construction, Watershed,
        Reservoirs is null ? WatershedNetwork : null, Transport is null ? Reservoirs : null,
        Transport, Excavation, ConstructionPhysical, StructuralPolicy, StructuralSettlementPolicy,
        ConsequencePolicy, pending, CoupledSettlementPolicy, DescentMode, ReservoirSeals, LiquidCells, Pacing, _standaloneEncounterActions, EncounterLifecycle, Care, ResourceHistory, Coordination, Retreat, IdentityIndex);

    /// <summary>
    /// Explicit coordinated closure at the same revision. Only representations change; time, work,
    /// custody, material and resources stay exact. Permanent identities remain the host lookup's duty.
    /// </summary>
    internal WorldSnapshot WithCoordinatedCut(ProductionSnapshot production, LiquidReservoirState reservoirs,
        LiquidCellState liquidCells, CareSnapshot care, WorldResourceHistory resourceHistory,
        WorldCoordinatedContinuation coordination) => new(
        Inventory, Space, Residents, AdmissionVersion, Calendar, Revision, AdvancingSteps,
        Dialogues, Gathering, production, Progression, Quests, Construction, Watershed, null, reservoirs,
        null, Excavation, ConstructionPhysical, StructuralPolicy, StructuralSettlementPolicy,
        ConsequencePolicy, PendingExcavation, CoupledSettlementPolicy, DescentMode, ReservoirSeals, liquidCells,
        Pacing, _standaloneEncounterActions, EncounterLifecycle, care, resourceHistory, coordination, Retreat, null);
}

using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Immutable admission and source-frame observations; no retained world or second spendable inventory.</summary>
internal sealed class ExcavationMaterialSource
{
    private readonly ContentHash _admission;
    private readonly ushort _profile;
    private readonly ContentHash _inventoryHash;
    private readonly ContentHash _productionHash;
    private readonly ContentHash _excavationHash;
    private readonly ContentHash _regionsHash;
    private readonly ContentHash? _questsHash;
    private readonly ContentHash? _progressionHash;
    private readonly (EntityId Actor, EntityId Bag)[] _residents;
    private readonly Dictionary<EntityId, byte[]> _installedLots;
    private readonly HashSet<EntityId> _originalLots;
    private readonly HashSet<EntityId> _admittedMaterialIdentities = [];
    private readonly HashSet<EntityId> _constructionReservations;
    private readonly ContentHash _constructionStockHash;
    internal ExcavationMaterialSource(WorldSnapshot world)
    {
        RequireSupported(world);
        _admission = world.AdmissionVersion;
        _profile = world.ProfileVersion;
        _inventoryHash = world.Inventory.ContentHash;
        _productionHash = world.Production!.ContentHash;
        _excavationHash = world.Excavation!.ContentHash;
        _regionsHash = RegionsHash(world.Space.Regions);
        _questsHash = world.Quests?.ContentHash;
        _progressionHash = world.Progression?.ContentHash;
        _residents = world.Residents.Select(value => (value.ActorId, value.InventoryCustodianId)).OrderBy(value => value.ActorId).ToArray();
        InitialGathering = world.Gathering!;
        Construction = world.Construction!;
        Physical = world.ConstructionPhysical;
        Structures = world.Space.Structures.ToArray();
        InitialIdentities = world.IdentityIndex;
        Scope = world.Space.Scope;
        GroundSupport = world.Space.GroundSupport;
        StructureSupport = world.Space.StructureSupport;
        // Only identity observations are retained; the source inventory and production roots are not.
        bool ObserveMaterialIdentity(EntityId identity)
        {
            _admittedMaterialIdentities.Add(identity);
            return false;
        }
        world.Inventory.ContainsIdentityMatching(ObserveMaterialIdentity);
        world.Production.ContainsIdentityMatching(ObserveMaterialIdentity);
        // V8 retains these already admitted non-material owners explicitly in its material registry.
        foreach (var node in InitialGathering.Nodes)
        {
            _admittedMaterialIdentities.Add(node.NodeId);
            _admittedMaterialIdentities.Add(node.Definition.DefinitionId);
        }
        foreach (var project in Construction.Projects.Where(value => value.Installed is not null))
            _admittedMaterialIdentities.Add(project.Installed!.StructureId);
        _constructionReservations = Construction.Projects.Select(value => value.ReservationId).ToHashSet();
        _constructionStockHash = ConstructionStockHash(world.Inventory, world.Production);
        _originalLots = world.Inventory.Custody.Lots.Select(value => value.LotId).Concat(world.Production.Lots.Select(value => value.LotId)).ToHashSet();
        _installedLots = Construction.Projects.Where(value => value.Installed is not null).ToDictionary(value => value.Installed!.GuardLotId,
            value => ProductionCodecV1.EncodeLot(world.Production.Lots.Single(lot => lot.LotId == value.Installed!.GuardLotId)));
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("excavation.material-source.v1", world.ContentHash.ToByteArray(), _admission.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(_profile), _inventoryHash.ToByteArray(), _productionHash.ToByteArray(),
            _excavationHash.ToByteArray(), _regionsHash.ToByteArray(), InitialGathering.ContentHash.ToByteArray(),
            Construction.ContentHash.ToByteArray(), Physical?.ContentHash.ToByteArray() ?? [],
            _questsHash?.ToByteArray() ?? [], _progressionHash?.ToByteArray() ?? []));
    }

    internal ContentHash ContentHash { get; }
    internal GatheringSnapshot InitialGathering { get; }
    internal ConstructionSnapshot Construction { get; }
    internal ConstructionPhysicalSnapshot? Physical { get; }
    internal SpatialStructure[] Structures { get; }
    internal WorldIdentityIndex InitialIdentities { get; }
    internal TerrainScope Scope { get; }
    internal GroundSupportMode GroundSupport { get; }
    internal StructureSupportMode StructureSupport { get; }

    internal void ValidateFrame(WorldSnapshot world)
    {
        RequireSupported(world);
        if (world.AdmissionVersion != _admission || world.ProfileVersion != _profile || world.Space.Scope != Scope
            || world.Inventory.ContentHash != _inventoryHash || world.Production!.ContentHash != _productionHash
            || world.Excavation!.ContentHash != _excavationHash || RegionsHash(world.Space.Regions) != _regionsHash
            || world.Construction!.ContentHash != Construction.ContentHash || world.ConstructionPhysical?.ContentHash != Physical?.ContentHash
            || world.Quests?.ContentHash != _questsHash || world.Progression?.ContentHash != _progressionHash
            || world.Space.GroundSupport != GroundSupport || world.Space.StructureSupport != StructureSupport
            || !_residents.SequenceEqual(world.Residents.Select(value => (value.ActorId, value.InventoryCustodianId)).OrderBy(value => value.ActorId))
            || world.Gathering!.ContentHash != InitialGathering.AdvanceTo(world.ProcessIntervals).ContentHash)
            throw new ArgumentException("Excavation needs a fresh frame of the exact admitted source; unjoined source work or material changes cannot reset component facts.");
    }

    internal void ValidateStaticMaterial(CountedAuthorityState inventory, ProductionSnapshot production, LocalSpatialSnapshot space)
    {
        if (space.Scope != Scope || space.GroundSupport != GroundSupport || space.StructureSupport != StructureSupport
            || space.Structures.Count != Structures.Length || Structures.Where((value, index) =>
                value.StructureId != space.Structures[index].StructureId || !value.Boxes.SequenceEqual(space.Structures[index].Boxes)
                || !value.BearingSurfaces.SequenceEqual(space.Structures[index].BearingSurfaces)).Any())
            throw new ArgumentException("Excavation retains its exact static construction and support interpretation.");
        Physical?.ValidateStableGeometry(space);
        if (ConstructionStockHash(inventory, production) != _constructionStockHash)
            throw new ArgumentException("Static construction preserves exact original reserved funding and installed backing.");
        if (production.ContainsIdentityMatching(identity => InitialIdentities.Contains(identity)
            && !_admittedMaterialIdentities.Contains(identity)))
            throw new ArgumentException("New material identities, including retired lots and batch producers, cannot alias source-world roles.");
        foreach (var pair in _installedLots)
        {
            var lot = production.Lots.SingleOrDefault(value => value.LotId == pair.Key);
            if (lot is null || !ProductionCodecV1.EncodeLot(lot).AsSpan().SequenceEqual(pair.Value))
                throw new ArgumentException("Excavation cannot mutate independently installed construction backing.");
        }
        if (production.MaterialHistory is { } history && history.Operations.Any(value => InitialIdentities.Contains(value.CommandId)))
            throw new ArgumentException("Material work cannot alias an admitted source-world identity.");
        if (inventory.Custody.Lots.Select(value => value.LotId).Concat(production.Lots.Select(value => value.LotId))
            .Any(value => !_originalLots.Contains(value) && InitialIdentities.Contains(value)))
            throw new ArgumentException("New material lots cannot alias source-world roles.");
    }

    private ContentHash ConstructionStockHash(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        bool Reserved(EntityId? identity) => identity is { } value && _constructionReservations.Contains(value);
        var raw = new CustodySnapshot(inventory.WorldId, inventory.BranchId, 0,
            inventory.Custody.Storages.Select(value => value with { Revision = 0 }),
            inventory.Custody.Lots.Where(value => Reserved(value.ReservedBy)));
        return ContentHash.Compute(CanonicalEncodingV1.Frame("excavation.static-construction-stock.v1",
            CustodyEncodingV1.Encode(raw), CanonicalEncodingV1.Frame("excavation.static-construction-outputs.v1",
                production.Lots.Where(value => Reserved(value.ReservedBy))
                    .Select(value => (ReadOnlyMemory<byte>)ProductionCodecV1.EncodeLot(value)).ToArray())));
    }

    internal static void RequireSupported(WorldSnapshot world)
    {
        if (world.Excavation is null || world.Gathering is null || world.Production is null || world.Construction is null
            || world.Production.IsContinued || world.HasReadingHold || world.PendingExcavation is not null
            || world.StructuralPolicy is not null || world.StructuralSettlementPolicy is not null || world.CoupledSettlementPolicy is not null
            || world.ConsequencePolicy is not null || world.Reservoirs is not null || world.Transport is not null || world.LiquidCells is not null
            || world.Watershed is not null || world.WatershedNetwork is not null || world.Pacing is not null || world.Care is not null
            || world.ResourceHistory is not null || world.EncounterLifecycle is not null || world.EncounterActions is not null)
            throw new ArgumentException("Excavation material continuation requires an unheld, nonpending dry source without unjoined load, settlement, liquid or encounter/resource publication.", nameof(world));
    }

    internal static ContentHash RegionsHash(IEnumerable<TerrainRegionSnapshot> regions) => ContentHash.Compute(
        CanonicalEncodingV1.Frame("excavation.material-regions.v1", regions.Select(value => (ReadOnlyMemory<byte>)value.ComputeContentHash().ToByteArray()).ToArray()));

    internal static ulong SpatialVersion(LocalSpatialSnapshot space) => space.StructureSupport == StructureSupportMode.TerrainAndConstruction ? 4UL
        : space.GroundSupport switch { GroundSupportMode.SteppedTerrainAndConstruction => 3UL, GroundSupportMode.TerrainAndConstruction => 2UL, _ => 1UL };
    internal static byte[] EncodeSpace(LocalSpatialSnapshot space) => WorldSpatialCodecV1.EncodeComponents(space, SpatialVersion(space));
}

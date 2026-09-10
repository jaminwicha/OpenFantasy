using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Composes finite gathering sources and a supplied tool kit into one complete new genesis.</summary>
internal sealed class GatheringFarmBuilder
{
    private readonly WorldSnapshot _basis;
    private readonly GatheringFarmDefinition _definition;
    private readonly GatheringFarmIdentityScope _ids;
    private readonly Dictionary<string, CountedItemDefinition> _items = [];

    internal GatheringFarmBuilder(WorldSnapshot basis, GatheringFarmDefinition definition,
        GatheringFarmSources sources, GatheringFarmInput input)
    {
        _basis = basis;
        _definition = definition;
        _ids = new GatheringFarmIdentityScope(input, basis);
        // This source pin is already validated by both the unchanged base and added source adapter.
        if (sources.Record("openfantasy.aldermere:items/wood").GetProperty("id").GetString() != "wood")
        {
            throw new InvalidDataException("The composed base source must remain counted wood.");
        }
        _items.Add("items/wood", basis.Inventory.Sources.Single().Definition);
        foreach (JsonElement item in definition.Root.GetProperty("items").EnumerateArray())
        {
            string key = item.GetProperty("key").GetString()!;
            _items.Add(key, new CountedItemDefinition(_ids.Definition(key), FarmCanonicalJson.Hash(item)));
        }
    }

    internal WorldSnapshot Build()
    {
        WorldResident player = _basis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);
        List<AdmittedCountedSource> sources = _basis.Inventory.Sources.ToList();
        List<CountedSourceLot> lots = _basis.Inventory.Custody.Lots.ToList();
        List<GatherToolDefinition> tools = CreateToolKit(player, sources, lots);
        List<GatherNodeState> nodes = CreateNodes(player, sources);
        JsonElement kit = _definition.Root.GetProperty("starterKit");
        ulong playerCapacity = kit.GetProperty("playerStorageUnitCapacity").GetUInt64();
        var storages = _basis.Inventory.Custody.Storages.Select(storage => storage.CustodianId == player.InventoryCustodianId
            ? new CustodianStorage(storage.CustodianId, playerCapacity, storage.Revision) : storage);
        var custody = new CustodySnapshot(_basis.WorldId, _basis.BranchId, 0, storages, lots);
        ulong totalCapacity = 0;
        foreach (AdmittedCountedSource source in sources) totalCapacity = checked(totalCapacity + source.UnitCapacity);
        var inventory = new CountedAuthorityState(custody, totalCapacity, _basis.Inventory.Actors,
            _basis.Inventory.Controls, _basis.Inventory.Policies, sources, _basis.Inventory.ReservationIds);
        uint initialEffort = _definition.Root.GetProperty("workProfile").GetProperty("initialEffort").GetUInt32();
        var gathering = new GatheringSnapshot(_basis.WorldId, _basis.BranchId, 0, nodes, tools,
            _basis.Residents.Select(resident => new GatherActorWork(resident.ActorId, initialEffort)));
        return new WorldSnapshot(inventory, _basis.Space, _basis.Residents, _definition.Version,
            _basis.Calendar, 0, 0, gathering: gathering);
    }

    private List<GatherToolDefinition> CreateToolKit(WorldResident player, List<AdmittedCountedSource> sources,
        List<CountedSourceLot> lots)
    {
        List<GatherToolDefinition> tools = [];
        foreach (JsonElement metadata in _definition.Root.GetProperty("starterKit").GetProperty("tools").EnumerateArray())
        {
            string key = metadata.GetProperty("itemKey").GetString()!;
            CountedItemDefinition item = _items[key];
            ulong quantity = metadata.GetProperty("quantity").GetUInt64();
            EntityId sourceId = _ids.Instance("supplied-kit.source." + key);
            sources.Add(new AdmittedCountedSource(sourceId, item, quantity));
            lots.Add(new CountedSourceLot(_ids.Instance("supplied-kit.lot." + key), player.InventoryCustodianId,
                item, new SourceInterval(sourceId, 0, quantity), 0));
            tools.Add(new GatherToolDefinition(item, (GatherToolKind)metadata.GetProperty("kind").GetInt32(),
                metadata.GetProperty("power").GetUInt32()));
        }
        return tools;
    }

    private List<GatherNodeState> CreateNodes(WorldResident player, List<AdmittedCountedSource> sources)
    {
        List<GatherNodeState> nodes = [];
        foreach (JsonElement metadata in _definition.Root.GetProperty("nodes").EnumerateArray())
        {
            string role = metadata.GetProperty("role").GetString()!;
            var definition = new GatherNodeDefinition(_ids.Definition(metadata.GetProperty("definitionKey").GetString()!),
                (GatherNodeKind)metadata.GetProperty("kind").GetInt32(), _items[metadata.GetProperty("yieldItemKey").GetString()!]);
            SubunitPosition feet = FarmAdmissionDefinition.Feet(metadata.GetProperty("tileX").GetInt32(),
                metadata.GetProperty("tileY").GetInt32(), metadata.GetProperty("feetZ").GetInt64());
            if (_basis.Space.QueryOccupancy(player.ActorId, feet) != SpatialOccupancy.Clear
                || definition.MaxSwings != metadata.GetProperty("swingsPerCycle").GetInt32()
                || definition.RegrowthDelaySteps != metadata.GetProperty("regrowthDelaySteps").GetUInt64()
                || definition.StumpSteps != metadata.GetProperty("stumpSteps").GetUInt64())
            {
                throw new InvalidDataException("A gathering node cannot be admitted on unsupported geometry or different work rules.");
            }
            ulong capacity = checked((ulong)definition.MaxSwings * metadata.GetProperty("completeCycles").GetUInt64());
            EntityId sourceId = _ids.Instance("node.source." + role);
            sources.Add(new AdmittedCountedSource(sourceId, definition.YieldDefinition, capacity));
            nodes.Add(new GatherNodeState(_ids.Instance("node.instance." + role), definition, sourceId,
                capacity, feet, 0, 0, definition.MaxSwings));
        }
        return nodes;
    }
}

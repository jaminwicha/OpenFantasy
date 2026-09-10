using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Builds one complete candidate genesis; publication and restoration belong to the world store.</summary>
internal sealed class FarmWorldBuilder
{
    private readonly FarmAdmissionDefinition _definition;
    private readonly FarmSourcePacket _source;
    private readonly FarmIdentityScope _ids;

    internal FarmWorldBuilder(FarmAdmissionDefinition definition, FarmSourcePacket source, byte[] seed)
    {
        _definition = definition;
        _source = source;
        _ids = new FarmIdentityScope(seed);
    }

    internal WorldSnapshot Build(string playerName)
    {
        var scope = new TerrainScope(_ids.Instance("world"), _ids.Instance("branch.main"), _ids.Instance("realm.material"));
        var body = _definition.CreateBody(_ids);
        EntityId place = _definition.PlaceId(_ids);
        TerrainRegionSnapshot terrain = _definition.CreateTerrain(scope, _ids);
        var actors = new FarmActorBuilder(_definition, _ids, scope, body, place);
        AdmittedFarmActor player = actors.Player(playerName);
        AdmittedFarmActor wren = actors.Wren(_source);
        var space = new LocalSpatialSnapshot(scope, [terrain], [player.Presence, wren.Presence]);
        CountedAuthorityState inventory = CreateInventory(scope, player, wren);
        return new WorldSnapshot(inventory, space, [player.Resident, wren.Resident], _definition.Version,
            _definition.InitialCalendar, _definition.Root.GetProperty("initialRevision").GetUInt64(),
            _definition.Root.GetProperty("initialAdvancingSteps").GetUInt64());
    }

    private CountedAuthorityState CreateInventory(TerrainScope scope, AdmittedFarmActor player, AdmittedFarmActor wren)
    {
        if (_definition.Wren.GetProperty("initialWood").GetUInt64() != 0)
        {
            throw new InvalidDataException("This farm genesis admits no initial wood for Wren.");
        }
        ulong quantity = _definition.Player.GetProperty("initialWood").GetUInt64();
        CountedItemDefinition wood = _definition.WoodDefinition(_ids);
        EntityId source = _ids.Instance("genesis.wood-source");
        var lot = new CountedSourceLot(_ids.Instance("genesis.player-wood-lot"), player.Storage.CustodianId,
            wood, new SourceInterval(source, 0, quantity), 0);
        var custody = new CustodySnapshot(scope.WorldId, scope.BranchId, 0,
            [player.Storage, wren.Storage], [lot]);
        return new CountedAuthorityState(custody, quantity, [player.Presence.ActorId, wren.Presence.ActorId],
            [player.Control, wren.Control], [player.Policy, wren.Policy], [new AdmittedCountedSource(source, wood, quantity)]);
    }
}

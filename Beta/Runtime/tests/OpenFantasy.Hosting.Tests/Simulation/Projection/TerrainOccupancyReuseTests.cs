using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Client.Cargo;

namespace OpenFantasy.Hosting.Tests.Simulation.Projection;

public sealed class TerrainOccupancyReuseTests
{
    private static TerrainRegionSnapshot Region => CargoTestWorld.Genesis.Space.Regions[0];

    [Fact]
    public void Worksite_and_liquid_views_share_exact_immutable_occupancy_and_match_independent_projection()
    {
        TerrainRegionSnapshot region = Region;
        var worksite = new LocalWorksiteTerrainView(region);
        var liquid = new LocalLiquidTerrainView(region);
        var expected = Array.AsReadOnly(region.Cells.Select(cell => cell.IsSolid).ToArray());
        Assert.Same(worksite.Occupancy, liquid.Occupancy);
        Assert.Same(worksite.Occupancy, new LocalWorksiteTerrainView(region).Occupancy);
        Assert.Equal(expected, worksite.Occupancy);
        Assert.Equal(JsonSerializer.Serialize(new LocalWorksiteTerrainView(region.Bounds, region.Revision, expected)),
            JsonSerializer.Serialize(worksite));
        Assert.Equal(JsonSerializer.Serialize(new LocalLiquidTerrainView(region.Bounds, region.Revision, expected)),
            JsonSerializer.Serialize(liquid));
        Assert.Throws<NotSupportedException>(() => ((IList<bool>)worksite.Occupancy)[0] = false);
    }

    [Fact]
    public void Equal_bounds_and_revisions_do_not_alias_different_excavations_or_decoded_region_instances()
    {
        TerrainRegionSnapshot original = Region;
        CellCoordinate[] solids = Enumerable.Range(0, original.Cells.Count).Where(index => original.Cells[index].IsSolid)
            .Take(2).Select(original.Bounds.CoordinateAt).ToArray();
        TerrainRegionSnapshot first = original.Excavate(original.Revision, [solids[0]]);
        TerrainRegionSnapshot second = original.Excavate(original.Revision, [solids[1]]);
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(first.Bounds, second.Bounds);
        var before = new LocalWorksiteTerrainView(original);
        var afterFirst = new LocalWorksiteTerrainView(first);
        var afterSecond = new LocalWorksiteTerrainView(second);
        Assert.NotSame(before.Occupancy, afterFirst.Occupancy);
        Assert.NotSame(afterFirst.Occupancy, afterSecond.Occupancy);
        Assert.True(before.IsSolid(solids[0]) && before.IsSolid(solids[1]));
        Assert.False(afterFirst.IsSolid(solids[0]));
        Assert.True(afterFirst.IsSolid(solids[1]));
        Assert.True(afterSecond.IsSolid(solids[0]));
        Assert.False(afterSecond.IsSolid(solids[1]));
        var decoded = new LocalWorksiteTerrainView(TerrainRegionCodecV1.Decode(TerrainRegionCodecV1.Encode(first)));
        Assert.NotSame(afterFirst.Occupancy, decoded.Occupancy);
        Assert.Equal(afterFirst.Occupancy, decoded.Occupancy);
    }

    [Fact]
    public void Rehydration_defensively_copies_input_even_when_bounds_and_revision_match_a_cached_region()
    {
        TerrainRegionSnapshot region = Region;
        var cached = new LocalWorksiteTerrainView(region);
        bool[] cells = cached.Occupancy.ToArray();
        var sharedInput = Array.AsReadOnly(cells);
        var worksite = new LocalWorksiteTerrainView(region.Bounds, region.Revision, sharedInput);
        var liquid = new LocalLiquidTerrainView(region.Bounds, region.Revision, sharedInput);
        cells[0] = !cells[0];
        Assert.NotSame(cached.Occupancy, worksite.Occupancy);
        Assert.NotSame(worksite.Occupancy, liquid.Occupancy);
        Assert.Equal(cached.Occupancy, worksite.Occupancy);
        Assert.Equal(cached.Occupancy, liquid.Occupancy);
    }

    [Fact]
    public async Task Concurrent_observers_receive_one_owned_projection()
    {
        TerrainRegionSnapshot region = TerrainRegionCodecV1.Decode(TerrainRegionCodecV1.Encode(Region));
        var projections = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            Task.Run(() => new LocalWorksiteTerrainView(region), TestContext.Current.CancellationToken)));
        Assert.All(projections, view => Assert.Same(projections[0].Occupancy, view.Occupancy));
        Assert.Equal(region.Cells.Select(cell => cell.IsSolid), projections[0].Occupancy);
    }

    [Fact]
    public void Retained_public_occupancy_does_not_keep_its_source_region_alive()
    {
        (WeakReference source, LocalWorksiteTerrainView view) = CreateDetachedView();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(source.IsAlive);
        Assert.NotEmpty(view.Occupancy);
        GC.KeepAlive(view);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Source, LocalWorksiteTerrainView View) CreateDetachedView()
    {
        TerrainRegionSnapshot region = TerrainRegionCodecV1.Decode(TerrainRegionCodecV1.Encode(Region));
        return (new WeakReference(region), new LocalWorksiteTerrainView(region));
    }

    [Fact]
    public void Warm_occupancy_lookup_allocates_no_cell_arrays()
    {
        TerrainRegionSnapshot region = Region;
        LocalTerrainOccupancy expected = LocalTerrainOccupancy.Project(region);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++) GC.KeepAlive(LocalTerrainOccupancy.Project(region));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Same(expected, LocalTerrainOccupancy.Project(region));
    }

    [Fact]
    public void Four_player_views_share_terrain_while_preserving_actor_inventory_and_wire_scope()
    {
        WorldSnapshot world = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText,
            new[] { "Amber", "Birch", "Cedar", "Dawn" });
        EntityId[] players = world.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).Order().ToArray();
        LocalWorldView[] views = players.Select(actor => new LocalWorldView(world, actor)).ToArray();
        Assert.Equal(4, views.Select(view => view.PlayerInventory.CustodianId).Distinct().Count());
        for (int index = 0; index < views.Length; index++)
        {
            LocalWorldView view = views[index];
            Assert.Equal(world.GetResident(players[index]).InventoryCustodianId, view.PlayerInventory.CustodianId);
            Assert.All(view.PlayerInventory.Lots, lot => Assert.Contains(world.Inventory.Custody.Lots,
                actual => actual.LotId == lot.LotId && actual.CustodianId == view.PlayerInventory.CustodianId));
            for (int region = 0; region < world.Space.Regions.Count; region++)
            {
                Assert.Same(views[0].Worksite!.Terrain[region].Occupancy, view.Worksite!.Terrain[region].Occupancy);
                Assert.Same(view.Worksite.Terrain[region].Occupancy, view.Liquids!.Terrain[region].Occupancy);
            }
            byte[] bytes = PlayerViewCodec.Encode(view);
            LocalWorldView replica = PlayerViewCodec.Decode(bytes);
            Assert.Equal(players[index], replica.PlayerActorId);
            Assert.Equal(bytes, PlayerViewCodec.Encode(replica));
        }
    }
}

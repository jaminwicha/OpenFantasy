using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalBreachViewTests
{
    [Fact]
    public void Owned_bank_target_is_public_without_fabricated_physical_members_or_private_water_and_substrate_claims()
    {
        WorldSnapshot state = BreachFarmAdmission.CreateEmbedded("breach-public-excavation", "Traveler");
        EntityId player = state.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        byte[] before = WorldSnapshotCodec.Encode(state);
        var view = new LocalWorldView(state, player);
        LocalWorksiteView worksite = Assert.IsType<LocalWorksiteView>(view.Worksite);
        Assert.NotNull(view.Liquids);
        Assert.False(worksite.HasPhysicalConstruction);
        Assert.False(worksite.SupportsAssistedTraversal);
        Assert.False(worksite.IsResolvingConsequences);
        Assert.Empty(worksite.Members);
        LocalExcavationView target = Assert.Single(worksite.ExcavationTargets);
        ExcavationSite site = Assert.Single(state.Excavation!.Sites);
        Assert.Equal(site.SiteId, target.SiteId);
        Assert.Equal(site.Cell, target.Cell);
        Assert.Equal(site.Stance, target.Stance);
        Assert.Equal(ExcavationReachMode.RaisedBankTop, target.ReachMode);
        Assert.Equal(site.Revision, target.SiteRevision);
        Assert.Equal(site.RequiredWork, target.RequiredWork);
        Assert.Equal(site.CompletedWork, target.CompletedWork);
        Assert.Equal(view.PlayerInventory.Revision, target.DestinationRevision);
        Assert.Equal(state.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision, target.TerrainRevision);
        Assert.True(worksite.Terrain.Single(region => region.Bounds.Contains(site.Cell)).IsSolid(site.Cell));
        foreach (var region in state.Space.Regions)
        {
            var projected = Assert.Single(worksite.Terrain, value => value.Bounds == region.Bounds);
            Assert.Equal(region.Revision, projected.Revision);
            Assert.Equal(region.Cells.Select(cell => cell.IsSolid), projected.Occupancy);
        }

        LocalExcavationToolView tool = Assert.Single(worksite.Tools);
        CountedSourceLot retained = state.Inventory.Custody.GetLot(tool.LotId);
        Assert.Equal(site.RequiredTool, retained.Definition);
        Assert.Equal(retained.Definition.DefinitionId, tool.DefinitionId);
        Assert.Equal(retained.Revision, tool.Revision);
        Assert.Null(retained.ReservedBy);
        Assert.False(retained.Protection.HasFlag(LotProtection.UserReserved));
        Assert.Contains(state.Inventory.Policies, policy => policy.CustodianId == retained.CustodianId && policy.OwnerActorId == player);

        EntityId other = state.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).ActorId;
        var foreignView = new LocalWorldView(state, other);
        Assert.Empty(foreignView.Worksite!.ExcavationTargets);
        Assert.Empty(foreignView.Worksite.Tools);
        string json = JsonSerializer.Serialize(worksite);
        foreach (string forbidden in new[] { "Source", "Claim", "Custodian", "Inventory", "Reservoir", "Origin", "ContentHash", "InputPin", "Quantity" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
        }
        IEnumerable<EntityId> hidden = state.Inventory.Sources.Select(source => source.SourceId)
            .Concat(state.Excavation.Sites.Select(site => site.TerrainSourceId))
            .Concat(state.Inventory.Custody.Lots.Where(lot => lot.LotId != tool.LotId).Select(lot => lot.LotId))
            .Concat(state.Reservoirs!.Geometries.SelectMany(geometry => new[] { geometry.ReservoirId, geometry.CustodianId }));
        foreach (EntityId identity in hidden)
        {
            Assert.DoesNotContain(JsonSerializer.Serialize(identity), json, StringComparison.Ordinal);
        }
        Assert.Throws<NotSupportedException>(() => ((IList<LocalExcavationView>)worksite.ExcavationTargets).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LocalExcavationToolView>)worksite.Tools).Clear());
        Assert.Equal(before, WorldSnapshotCodec.Encode(state));
    }
}

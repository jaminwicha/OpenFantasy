using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Spatial;

namespace OpenFantasy.Core.Tests.Structures;

/// <summary>Supplied completed-domain geometry only; earned construction and journal history belong to Hosting tests.</summary>
public sealed class StructuralSettlementBoundaryTests
{
    private static readonly ContentHash Pin = ContentHash.Compute("settlement-boundary-test"u8);
    private static readonly CellCoordinate Removed = new(0, 0, 0);
    private static readonly StructuralSettlementPolicy Policy = new(256, 256, 256, 9800);

    [Fact]
    public void A_rigid_slab_and_its_complete_rider_descend_together_without_replacing_material()
    {
        ConstructionPhysicalSnapshot physical = Physical(Project(1, [new(0, 0, 0, 1000, 1000, 125)]));
        TerrainRegionSnapshot terrain = Region();
        ActorPresence rider = SpatialFixture.Actor(1, new(128, 128, 288), SpatialFixture.Body(64, 64, 128));
        LocalSpatialSnapshot original = Scene(terrain, physical, rider);
        ConstructionMemberState member = Assert.Single(physical.Members);
        StructuralMemberReference backing = physical.GetMemberReference(member.StructureId, member.MemberIndex);

        ConstructionSettlementResult result = new ConstructionSettlementResolver().Resolve(original,
            [Remove(terrain)], physical, Policy);

        ConstructionMemberState settled = Assert.Single(result.Physical.Members);
        Assert.Equal(new SubunitPosition(0, 0, -256), settled.Displacement);
        Assert.Equal(ConstructionMemberDisposition.Resting, settled.Disposition);
        Assert.Equal(member.ConditionParts, settled.ConditionParts);
        Assert.Equal(1UL, settled.Revision);
        Assert.Equal(1UL, result.Physical.Revision);
        Assert.Equal(backing, result.Physical.GetMemberReference(settled.StructureId, settled.MemberIndex));
        Assert.Same(physical.Construction, result.Physical.Construction);
        Assert.Equal(new SubunitPosition(128, 128, 32), result.Space.GetActor(rider.ActorId).Feet);
        Assert.Equal(1UL, result.Space.GetActor(rider.ActorId).Revision);
        Assert.Equal(new SubunitPosition(128, 128, 288), rider.Feet);
        Assert.Equal(default, member.Displacement);
        Assert.False(terrain.GetCell(Removed).IsExcavated);
    }

    [Fact]
    public void An_unsupported_actor_descends_to_known_terrain_without_moving_an_unaffected_assembly()
    {
        ConstructionPhysicalSnapshot physical = Physical(Project(1, [new(2000, 0, 0, 3000, 1000, 125)]));
        TerrainRegionSnapshot terrain = Region();
        ActorPresence actor = SpatialFixture.Actor(1, new(128, 128, 256), SpatialFixture.Body(64, 64, 128));
        LocalSpatialSnapshot original = Scene(terrain, physical, actor);

        ConstructionSettlementResult result = new ConstructionSettlementResolver().Resolve(original,
            [Remove(terrain)], physical, Policy);

        Assert.Equal(new SubunitPosition(128, 128, 0), result.Space.GetActor(actor.ActorId).Feet);
        Assert.Equal(1UL, result.Space.GetActor(actor.ActorId).Revision);
        Assert.Same(actor.Condition, result.Space.GetActor(actor.ActorId).Condition);
        Assert.Same(physical, result.Physical);
        Assert.Equal(new SubunitPosition(128, 128, 256), actor.Feet);
        Assert.False(terrain.GetCell(Removed).IsExcavated);
    }

    [Fact]
    public void An_upper_member_cannot_cross_a_foreign_overhang_even_when_both_endpoint_scenes_are_clear()
    {
        ConstructionProject moving = Project(1,
            [new(0, 0, 0, 250, 1000, 1000), new(0, 0, 1000, 2000, 1000, 1125)]);
        ConstructionProject obstacle = Project(2,
            [new(2000, 0, 0, 2250, 1000, 375), new(1250, 0, 375, 2250, 1000, 500)]);
        ConstructionPhysicalSnapshot physical = Physical(moving, obstacle);
        TerrainRegionSnapshot terrain = Region();
        LocalSpatialSnapshot original = Scene(terrain, physical);
        TerrainRegionSnapshot excavated = Remove(terrain);
        ContentHash before = physical.ContentHash;

        // A destination-only check would accept this geometry: the upper member ends below the
        // foreign overhang, and its base never intersects that obstacle's footprint.
        ConstructionPhysicalSnapshot teleportedEndpoint = physical.WithMembers(0, physical.Members
            .Where(value => value.StructureId == moving.Installed!.StructureId)
            .Select(value => value.WithState(value.Revision, value.ConditionParts,
                new(0, 0, -256), ConstructionMemberDisposition.Resting)));
        LocalSpatialSnapshot clearEndpoint = Scene(excavated, teleportedEndpoint);
        teleportedEndpoint.ValidateStableGeometry(clearEndpoint);

        Assert.Throws<ArgumentException>(() => new ConstructionSettlementResolver().Resolve(original,
            [excavated], physical, Policy));
        Assert.Equal(before, physical.ContentHash);
        Assert.All(physical.Members, value => Assert.Equal(default, value.Displacement));
    }

    [Fact]
    public void Missing_lower_terrain_cannot_become_a_floor_at_the_drop_limit()
    {
        ConstructionPhysicalSnapshot physical = Physical(Project(1, [new(0, 0, 0, 1000, 1000, 125)]));
        TerrainRegionSnapshot terrain = Region(minimumZ: 0);
        LocalSpatialSnapshot original = Scene(terrain, physical);
        ContentHash before = physical.ContentHash;

        Assert.Throws<ArgumentException>(() => new ConstructionSettlementResolver().Resolve(original,
            [Remove(terrain)], physical, Policy));

        Assert.Equal(before, physical.ContentHash);
        Assert.Equal(0UL, physical.Revision);
    }

    [Fact]
    public void A_partial_surviving_ledge_blocks_rigid_descent_to_the_complete_lower_floor()
    {
        ConstructionPhysicalSnapshot physical = Physical(Project(1, [new(0, 0, 0, 2000, 1000, 125)]));
        TerrainRegionSnapshot terrain = Region();
        LocalSpatialSnapshot original = Scene(terrain, physical);
        TerrainRegionSnapshot excavated = Remove(terrain);
        Assert.True(excavated.GetCell(new(1, 0, 0)).IsSolid);
        Assert.True(excavated.GetCell(new(0, 0, -1)).IsSolid);
        Assert.True(excavated.GetCell(new(1, 0, -1)).IsSolid);

        Assert.Throws<ArgumentException>(() => new ConstructionSettlementResolver().Resolve(original,
            [excavated], physical, Policy));

        Assert.All(physical.Members, value => Assert.Equal(ConstructionMemberDisposition.Standing, value.Disposition));
    }

    [Fact]
    public void A_stationary_actor_below_the_base_is_neither_ignored_nor_used_as_a_landing_floor()
    {
        ConstructionPhysicalSnapshot physical = Physical(Project(1, [new(0, 0, 0, 1000, 1000, 125)]));
        TerrainRegionSnapshot terrain = Region(minimumZ: -2);
        terrain = terrain.Excavate(terrain.Revision, [new(0, 0, -1)]);
        ActorPresence below = SpatialFixture.Actor(2, new(128, 128, -256), SpatialFixture.Body(64, 64, 256));
        LocalSpatialSnapshot original = Scene(terrain, physical, below);

        Assert.Throws<ArgumentException>(() => new ConstructionSettlementResolver().Resolve(original,
            [Remove(terrain)], physical, Policy));

        Assert.Equal(new SubunitPosition(128, 128, -256), below.Feet);
        Assert.Equal(0UL, below.Revision);
        Assert.Equal(0UL, physical.Revision);
    }

    private static LocalSpatialSnapshot Scene(TerrainRegionSnapshot terrain, ConstructionPhysicalSnapshot physical,
        params ActorPresence[] actors) => new(SpatialFixture.Scope, [terrain], actors, physical.GetStructures(),
            GroundSupportMode.SteppedTerrainAndConstruction);

    private static TerrainRegionSnapshot Region(long minimumZ = -1)
    {
        var material = new CountedItemDefinition(Id(800), Pin);
        var rules = new StratifiedTerrainRules(0, 0, 1, new(material, material, material));
        return new StratifiedTerrainGenerator().Generate(SpatialFixture.Scope, Id(801), rules,
            new(new(-1, -1, minimumZ), 5, 3, 8));
    }

    private static TerrainRegionSnapshot Remove(TerrainRegionSnapshot terrain) =>
        terrain.Excavate(terrain.Revision, [Removed]);

    private static ConstructionPhysicalSnapshot Physical(params ConstructionProject[] projects) =>
        ConstructionPhysicalSnapshot.Create(new(SpatialFixture.Scope.WorldId, SpatialFixture.Scope.BranchId,
            checked((ulong)projects.Length), projects.Select(value => value.Definition), projects));

    private static ConstructionProject Project(ulong ordinal, MillimetreBox[] members)
    {
        EntityId Named(ulong suffix) => Id(ordinal * 100 + suffix);
        ulong volume = members.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres));
        const ulong mass = 1_000_000;
        var package = new PackageId("settlement-boundary-test");
        var input = new MaterialDefinition(package, Named(1), "input", new(mass), new(volume), new(1));
        var output = new MaterialDefinition(package, Named(2), "output", new(mass), new(volume), new(1));
        var inputItem = new CountedItemDefinition(Named(3), Pin);
        var outputItem = new CountedItemDefinition(Named(4), Pin);
        var materialRecipe = new MaterialRecipeDefinition(package, Named(5), "assemble", new(1), null,
            new MaterialCatalog([input, output]),
            [new(RecipeMaterialRole.Input, input.Pin(), 1), new(RecipeMaterialRole.Output, output.Pin(), 1)]);
        var recipe = new CraftRecipeDefinition(Named(6), materialRecipe, [new(inputItem, input), new(outputItem, output)],
            [new(inputItem, 0, new(mass))]);
        var tool = new ConstructionToolDefinition(Named(7), new(Named(8), Pin), ConstructionToolOperation.CutCrossSlots);
        var definition = new ConstructionDefinition(Named(9), $"test/settlement-{ordinal}", "Settlement specimen",
            recipe, 0, tool, members, new(1), new ExposureRate(Named(10), Pin, 1, ExposurePacingV1.ExplorationMillisecondsPerStep));
        EntityId projectId = Named(11);
        EntityId builder = Named(12);
        var progress = ExposureProgress.Begin(projectId, definition.WorkRate)
            .Advance(LocalExposureDuration.FromMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep));
        EntityId cause = Named(16);
        var installed = new InstalledStructure(EntityId.DeriveSpawned(cause, 2), EntityId.DeriveSpawned(cause, 1),
            0, EntityId.DeriveSpawned(cause, 0), cause);
        var site = new SubunitPosition(0, 0, 256);
        SubunitPosition work = site.Offset(0, -256, 0);
        return new(projectId, definition, builder, builder, Named(13), Named(14), Named(15), site, work,
            [new(builder, work, work.Offset(256, 0, 0))], 1, progress.Capture(), installed);
    }

    private static EntityId Id(ulong value) => new(0x736574746c6564, value);
}

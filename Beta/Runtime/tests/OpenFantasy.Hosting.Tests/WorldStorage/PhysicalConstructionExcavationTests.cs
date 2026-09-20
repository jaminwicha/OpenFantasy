using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class PhysicalConstructionExcavationTests
{
    private static readonly CellCoordinate Pedestal = new(102, 324, 0);
    private static readonly CellCoordinate StanceCell = new(102, 325, 0);
    private static readonly CellCoordinate LowerLegCell = new(106, 327, 0);
    private static readonly SubunitPosition Stance = new(102 * 256 + 128, 325 * 256 + 128, 0);

    [Theory]
    [InlineData(1UL)]
    [InlineData(3UL)]
    public void Higher_component_support_cannot_be_excavated_or_charge_partial_work(ulong requiredWork)
    {
        var fixture = new StairNavigationWorldFixture();
        WorldSnapshot genesis = ExtractionGenesis(fixture.Genesis, requiredWork);
        using var files = new StorageFiles();
        WorldSnapshot built = FundAndBuild(fixture, files.Path, genesis);
        Assert.Single(built.Production!.Batches);
        Assert.NotNull(Assert.Single(built.Construction!.Projects).Installed);
        Assert.Equal(12, built.ConstructionPhysical!.Members.Count);

        // Explicit supplied scene: one leg has been detached and placed on a lower authored floor.
        // Funding and construction above were real actions; this does not claim an executed fall,
        // impact, terrain-carving action or character traversal into the supplied stance.
        WorldSnapshot before = SupplyStableScene(built, fixture.Player);
        byte[] beforeBytes = WorldSnapshotCodec.Encode(before);
        Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(beforeBytes)));
        ExcavationSite site = Assert.Single(before.Excavation!.Sites);
        TerrainRegionSnapshot region = before.Space.Regions.Single(value => value.Bounds.Contains(Pedestal));
        Assert.True(region.GetCell(Pedestal).IsSolid);
        Assert.Equal(0L, Assert.Single(before.Space.Structures).GroundPlaneZ);
        Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(before.Space, fixture.Player, site.Stance));
        Assert.True(before.Gathering!.GetActorWork(fixture.Player).Effort >= GatherRulesV1.EffortPerSwing);

        // Negative control: the old aggregate-minimum geometry check accepts this exact final hole.
        // Only the physical owner's per-component grounding discovers the higher unsupported base.
        TerrainRegionSnapshot hole = region.Excavate(region.Revision, [Pedestal]);
        var legacyAccepted = new LocalSpatialSnapshot(before.Space.Scope,
            before.Space.Regions.Select(value => ReferenceEquals(value, region) ? hole : value),
            before.Space.Actors, before.Space.Structures, before.Space.GroundSupport);
        Assert.All(before.Gathering.Nodes, node =>
            Assert.Equal(SpatialOccupancy.Clear, ResourceInteractionQueries.QueryAnchor(legacyAccepted, node.Feet)));
        Assert.Throws<ArgumentException>(() => before.ConstructionPhysical!.ValidateStableGeometry(legacyAccepted));

        var tool = before.Inventory.Custody.Lots.Single(value => value.Definition == site.RequiredTool
            && value.CustodianId == before.GetResident(fixture.Player).InventoryCustodianId);
        var delivery = ConstructionUpgradeFixture.Delivery(before, fixture.Player);
        var command = new WorldActionCommand(fixture.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            fixture.Player, before.WorldId, before.BranchId, before.Space.GetActor(fixture.Player).Revision,
            new ExcavateIntent(site.SiteId, site.Revision, region.Revision, tool.LotId, tool.Revision,
                before.Inventory.Custody.GetStorage(before.GetResident(fixture.Player).InventoryCustodianId).Revision));
        WorldStepBatch batch = fixture.Actions.Batch(before, [command], []);
        WorldStepDecision refused = new WorldStepProcessor().Decide(before,
            WorldStepCodec.Decode(WorldStepCodec.Encode(batch)));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        WorldSnapshot after = refused.Candidate;
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.Excavation.ContentHash, after.Excavation!.ContentHash);
        Assert.Equal(before.Space.Regions.Select(value => value.ComputeContentHash()),
            after.Space.Regions.Select(value => value.ComputeContentHash()));
        Assert.Equal(before.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            after.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(0UL, Assert.Single(after.Excavation.Sites).CompletedWork);
        Assert.DoesNotContain(after.Inventory.Custody.Lots, value => value.Source.SourceId == site.CountedSourceId);
        Assert.DoesNotContain(refused.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.False(after.IdentityIndex.Contains(EntityId.DeriveSpawned(command.CommandId, 0)));

        // Refusal preserves work and stock while the normally selected advancing tick still happens.
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
        Assert.Equal(before.Gathering.AdvanceStep(after.AdvancingSteps).ContentHash, after.Gathering!.ContentHash);
        Assert.All(before.Residents, resident => Assert.Equal(
            resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)),
            after.GetResident(resident.ActorId).ExperiencedTime));
        Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(before));
        byte[] afterBytes = WorldSnapshotCodec.Encode(after);
        Assert.Equal(afterBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(afterBytes)));
        using SqliteWorldAuthority untouched = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(built.ContentHash, untouched.InspectForHost().State.ContentHash);
    }

    private static WorldSnapshot ExtractionGenesis(WorldSnapshot original, ulong requiredWork)
    {
        ExcavationSite prior = Assert.Single(original.Excavation!.Sites);
        TerrainRegionSnapshot surface = original.Space.Regions.Single(value => value.Bounds.Contains(Pedestal));
        TerrainCell cell = surface.GetCell(Pedestal);
        Assert.Equal(prior.Material.Item, cell.Material);
        var site = new ExcavationSite(prior.SiteId, Pedestal, cell.SourceId!.Value, prior.CountedSourceId,
            prior.OwnerActorId, prior.Material, prior.RequiredTool, requiredWork, 0, Stance);
        // This complete lower dependency is authored at fresh admission, below the original surface region.
        var lower = new StratifiedTerrainGenerator().Generate(original.Space.Scope, surface.Seed, surface.Rules,
            new(new(102, 324, -2), 8, 4, 2));
        var space = new LocalSpatialSnapshot(original.Space.Scope, original.Space.Regions.Append(lower), original.Space.Actors);
        var basis = new WorldSnapshot(original.Inventory, space, original.Residents, original.AdmissionVersion,
            original.Calendar, 0, 0, gathering: original.Gathering, production: original.Production,
            progression: original.Progression, quests: original.Quests, construction: original.Construction);
        WorldSnapshot extraction = WorldExcavationAdmission.CreateGenesis(basis, new([site]));
        return WorldConstructionPhysicalAdmission.CreateGenesis(
            WorldSteppedSupportAdmission.CreateGenesis(WorldConstructedSupportAdmission.CreateGenesis(extraction)));
    }

    private static WorldSnapshot FundAndBuild(StairNavigationWorldFixture fixture, string path, WorldSnapshot genesis)
    {
        WorldSnapshot world = fixture.Actions.CreateSource(path, completeQuest: true, genesis: genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = fixture.Actions.Act(authority, world, fixture.Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        var controller = new LocalNpcController();
        for (int step = 0; world.Construction!.Projects.Single().Installed is null; step++)
        {
            Assert.True(step < 128, "The supplied geometry must originate in actual funded NPC construction.");
            WorldStepDecision decision = authority.Commit(fixture.Actions.Batch(world,
                controller.SelectActions(world, fixture.Actions.NextIdentity), controller.SelectMovement(world)));
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = decision.Candidate;
        }
        for (int step = 0; world.Gathering!.GetActorWork(fixture.Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40, "Ordinary advancing steps must restore enough effort within the fixture bound.");
            world = authority.Commit(fixture.Actions.Batch(world, [], [])).Candidate;
        }
        return world;
    }

    private static WorldSnapshot SupplyStableScene(WorldSnapshot built, EntityId player)
    {
        ConstructionProject project = Assert.Single(built.Construction!.Projects);
        uint leg = (uint)project.Definition.Members.Select((value, index) => (value, index))
            .Single(value => value.value.MinX == 2000 && value.value.MinY == 3750 && value.value.MaxZ == 2000).index;
        ConstructionPhysicalSnapshot physical = built.ConstructionPhysical!;
        ConstructionMemberState member = physical.GetMember(project.Installed!.StructureId, leg);
        // Original leg: X108..108.25, Y327.75..328, Z1..3 metres.
        // Resting leg: X106.25..106.5, Y327.25..327.5, Z0..2 metres; the rest stays at Z1.
        physical = physical.WithMember(physical.Revision,
            member.WithState(member.Revision, member.ConditionParts, new(-448, -128, -256), ConstructionMemberDisposition.Resting));
        TerrainRegionSnapshot surface = built.Space.Regions.Single(value => value.Bounds.Contains(Pedestal));
        TerrainRegionSnapshot carved = surface.Excavate(surface.Revision, [StanceCell, LowerLegCell]);
        ActorPresence original = built.Space.GetActor(player);
        var actor = new ActorPresence(original.Binding, original.Condition, original.Scope, Stance,
            checked(original.Revision + 1), original.HomePlaceId);
        var space = new LocalSpatialSnapshot(built.Space.Scope,
            built.Space.Regions.Select(value => ReferenceEquals(value, surface) ? carved : value),
            built.Space.Actors.Select(value => value.ActorId == player ? actor : value), physical.GetStructures(), built.Space.GroundSupport);
        physical.ValidateStableGeometry(space);
        return new(built.Inventory, space, built.Residents, built.AdmissionVersion, built.Calendar,
            built.Revision, built.AdvancingSteps, built.Dialogues, built.Gathering, built.Production, built.Progression,
            built.Quests, built.Construction, excavation: built.Excavation, constructionPhysical: physical);
    }
}

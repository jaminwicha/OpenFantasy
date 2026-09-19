using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ExcavationContractTests
{
    [Theory]
    [InlineData("site")]
    [InlineData("terrain")]
    [InlineData("actor")]
    [InlineData("destination")]
    [InlineData("tool")]
    public void Exact_revision_pins_cannot_be_replaced_by_current_state(string field)
    {
        var f = new ExcavationWorldFixture();
        var world = f.Earn(f.Genesis);
        var command = f.Command(world);
        var i = (ExcavateIntent)command.Intent;
        var altered = i with
        {
            ExpectedSiteRevision = field == "site" ? 100UL : i.ExpectedSiteRevision,
            ExpectedTerrainRevision = field == "terrain" ? 100UL : i.ExpectedTerrainRevision,
            ExpectedDestinationRevision = field == "destination" ? 100UL : i.ExpectedDestinationRevision,
            ExpectedToolLotRevision = field == "tool" ? 100UL : i.ExpectedToolLotRevision
        };
        command = new(command.CommandId, command.PrincipalId, command.OriginSessionId, command.ActorId,
            command.WorldId, command.BranchId, field == "actor" ? 100UL : command.ExpectedActorRevision, altered);
        var result = f.Step(world, command);
        Assert.Equal(field == "tool" ? WorldActionRejection.Unavailable : WorldActionRejection.RevisionConflict, result.Receipts.Single().Rejection);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(world.Excavation!.ContentHash, result.Candidate.Excavation!.ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_effort_or_unowned_tool_cannot_work(bool otherTool)
    {
        var f = new ExcavationWorldFixture();
        var world = otherTool ? f.Earn(f.Genesis) : f.Genesis;
        var command = f.Command(world);
        if (otherTool)
        {
            var lot = world.Inventory.Custody.Lots.First(l => l.CustodianId != world.GetResident(f.Player).InventoryCustodianId);
            command = Replace(command, ((ExcavateIntent)command.Intent) with { ToolLotId = lot.LotId, ExpectedToolLotRevision = lot.Revision });
        }
        Assert.Equal(WorldActionRejection.Unavailable, f.Step(world, command).Receipts.Single().Rejection);
    }

    [Fact]
    public void Direct_face_reach_cannot_borrow_the_stance_as_an_extension()
    {
        var f = new ExcavationWorldFixture();
        var world = f.Earn(f.Genesis);
        var actors = world.Space.Actors.Select(a => a.ActorId == f.Player
            ? new ActorPresence(a.Binding, a.Condition, a.Scope, new(-304, 128, 0), a.Revision, a.HomePlaceId) : a);
        world = Copy(world, space: world.Space.WithActors(actors));
        // Stance is within 448 subunits, but the actual face is farther than the tool reach.
        Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(world.Space, f.Player, f.Site.Stance));
        Assert.Equal(WorldActionRejection.OutOfReach, f.Step(world, f.Command(world)).Receipts.Single().Rejection);
    }

    [Fact]
    public void Work_suppresses_movement_and_never_issues_partial_material()
    {
        var f = new ExcavationWorldFixture();
        var world = f.Earn(f.Genesis);
        var movement = new WorldMovementInput(ConstructionUpgradeFixture.Delivery(world, f.Player), f.Player, -32, 0);
        var result = new WorldStepProcessor().Decide(world, f.Actions.Batch(world, [f.Command(world)], [movement]));
        Assert.Equal(WorldActionRejection.None, result.Receipts.Single().Rejection);
        Assert.Equal(world.Space.GetActor(f.Player).Feet, result.Candidate.Space.GetActor(f.Player).Feet);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(1UL, result.Candidate.Excavation!.Sites.Single().CompletedWork);
    }

    [Fact]
    public void Reading_hold_refuses_work_and_advances_neither_effort_nor_calendar()
    {
        var f = new ExcavationWorldFixture();
        var world = f.Earn(f.Genesis);
        var other = world.Residents.Single(r => r.ActorId != f.Player).ActorId;
        var c = Replace(f.Command(world), new SpeakToActorIntent(other, world.Space.GetActor(other).Revision));
        world = f.Step(world, c).Candidate;
        Assert.True(world.HasReadingHold);
        var result = f.Step(world, f.Command(world));
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single().Rejection);
        Assert.Equal(world.Calendar, result.Candidate.Calendar);
        Assert.Equal(world.Gathering!.ContentHash, result.Candidate.Gathering!.ContentHash);
    }

    [Fact]
    public void Unissued_source_cannot_be_smuggled_into_a_solid_cell_world()
    {
        var f = new ExcavationWorldFixture();
        var w = f.Genesis;
        var lot = new CountedSourceLot(ExcavationWorldFixture.Id(90), w.GetResident(f.Player).InventoryCustodianId,
            f.Site.Material.Item, new(f.Site.CountedSourceId, 0, 1), 0);
        var inventory = Inventory(w, w.Inventory.Custody.Lots.Append(lot));
        Assert.Throws<ArgumentException>(() => Copy(w, inventory: inventory));
    }

    [Fact]
    public void Calibration_is_shared_across_sites_even_without_production_recipes()
    {
        var f = new ExcavationWorldFixture();
        var s = f.Site;
        var physical = s.Material.Physical;
        var alternate = new ItemPhysicalBinding(s.Material.Item, new(physical.Package, ExcavationWorldFixture.Id(93), physical.Key,
            physical.MassPerUnit, new(500_000_000), physical.Hardness));
        var second = new ExcavationSite(ExcavationWorldFixture.Id(94), new(1, 1, 0), ExcavationWorldFixture.Id(95),
            ExcavationWorldFixture.Id(96), f.Player, alternate, s.RequiredTool, 3, 0, new(384, 128, 0));
        Assert.Throws<ArgumentException>(() => new ExcavationSnapshot([s, second]));
    }

    [Fact]
    public void Completed_material_can_be_crafted_and_recrafted_without_restoring_the_cell()
    {
        var f = new ExcavationWorldFixture();
        var m = f.Site.Material;
        var physical = new MaterialRecipeDefinition(m.Physical.Package, ExcavationWorldFixture.Id(100), "reshape", new(1), null,
            new([m.Physical]), [new(RecipeMaterialRole.Input, m.Physical.Pin(), 1), new(RecipeMaterialRole.Output, m.Physical.Pin(), 1)]);
        var recipe = new CraftRecipeDefinition(ExcavationWorldFixture.Id(101), physical, [m], [new(m.Item, 0, m.Physical.MassPerUnit)]);
        var w = f.Genesis;
        w = Copy(w, production: new(w.WorldId, w.BranchId, 0, w.Production!.Recipes.Append(recipe), [], []));
        for (int i = 0; i < 3; i++) { w = f.Earn(w); w = f.Step(w, f.Command(w)).Candidate; }
        var raw = w.Inventory.Custody.Lots.Single(l => l.Source.SourceId == f.Site.CountedSourceId);
        var bag = raw.CustodianId;
        var craft = new CraftIntent(new(recipe.DefinitionId, recipe.Version, w.Production!.Revision, bag,
            w.Inventory.Custody.GetStorage(bag).Revision, [new(IngredientLotKind.Source, raw.LotId, raw.Revision, 1)]));
        var decision = f.Step(w, Replace(f.Command(w), craft));
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single().Rejection);
        w = decision.Candidate;
        Assert.DoesNotContain(w.Inventory.Custody.Lots, l => l.Source.SourceId == f.Site.CountedSourceId);
        Assert.Single(w.Production!.Batches.SelectMany(b => b.Ingredients), i => i.Source?.SourceId == f.Site.CountedSourceId);
        var derived = w.Production.Lots.Single(l => l.Definition == m.Item);
        craft = new(new(recipe.DefinitionId, recipe.Version, w.Production.Revision, bag,
            w.Inventory.Custody.GetStorage(bag).Revision, [new(IngredientLotKind.Transformed, derived.LotId, derived.Revision, 1)]));
        decision = f.Step(w, Replace(f.Command(w), craft));
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single().Rejection);
        Assert.Equal(decision.Candidate.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(decision.Candidate)).ContentHash);
        Assert.Throws<ArgumentException>(() => Copy(f.Earn(f.Genesis), production: decision.Candidate.Production));
    }

    [Fact]
    public void Action_and_catalog_bytes_are_canonical_and_old_profiles_cannot_execute_excavation()
    {
        var f = new ExcavationWorldFixture();
        var command = f.Command(f.Genesis);
        var bytes = WorldActionCodec.Encode(command);
        Assert.Equal(bytes, WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldActionCodec.Decode([..bytes, 0]));
        var catalog = ExcavationCodecV1.Encode(f.Genesis.Excavation!);
        Assert.Equal(catalog, ExcavationCodecV1.Encode(ExcavationCodecV1.Decode(catalog)));
        Assert.Throws<FormatException>(() => ExcavationCodecV1.Decode([..catalog, 0]));
        for (ushort profile = 1; profile < 10; profile++)
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(f.Actions.NextIdentity(), f.Genesis.WorldId, f.Genesis.BranchId,
                0, 0, WorldBatchKind.AdvancingStep, [], [command], profile));
        Assert.Throws<ArgumentException>(() => WorldExcavationAdmission.CreateGenesis(f.Earn(f.Basis), f.Genesis.Excavation!));
    }

    [Fact]
    public void Final_capacity_and_historical_identity_failures_preserve_earned_work_and_material()
    {
        var f = new ExcavationWorldFixture();
        var w = f.Genesis;
        for (int i = 0; i < 2; i++) { w = f.Earn(w); w = f.Step(w, f.Command(w)).Candidate; }
        w = f.Earn(w);
        var command = f.Command(w);
        var result = new WorldStepProcessor().Decide(w, f.Actions.Batch(w, [command], []),
            id => id == EntityId.DeriveSpawned(command.CommandId, 0));
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single().Rejection);
        Assert.Equal(w.Excavation!.ContentHash, result.Candidate.Excavation!.ContentHash);
        Assert.Equal(w.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        var bag = w.GetResident(f.Player).InventoryCustodianId;
        var prior = w.Inventory;
        var custody = new CustodySnapshot(w.WorldId, w.BranchId, prior.Custody.Revision,
            prior.Custody.Storages.Select(s => s.CustodianId == bag ? s with { UnitCapacity = prior.Custody.GetStoredQuantity(bag) } : s),
            prior.Custody.Lots);
        w = Copy(w, inventory: new(custody, prior.AdmittedUnitCapacity, prior.Actors, prior.Controls, prior.Policies, prior.Sources, prior.ReservationIds));
        result = f.Step(w, f.Command(w));
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single().Rejection);
        Assert.Equal(2UL, result.Candidate.Excavation!.Sites.Single().CompletedWork);
        Assert.Equal(w.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(Math.Min(1000U, w.Gathering!.GetActorWork(f.Player).Effort + 10U), result.Candidate.Gathering!.GetActorWork(f.Player).Effort);
    }

    [Fact]
    public void A_second_actor_does_not_expand_the_owners_labor_budget()
    {
        var f = new ExcavationWorldFixture();
        var w = f.Step(f.Genesis).Candidate;
        var s = f.Site;
        var impossible = new ExcavationSite(s.SiteId, s.Cell, s.TerrainSourceId, s.CountedSourceId, s.OwnerActorId,
            s.Material, s.RequiredTool, s.RequiredWork, 2, s.Stance);
        Assert.Throws<ArgumentException>(() => Copy(w, excavation: new([impossible])));
    }

    [Fact]
    public void Gathering_anchor_support_is_checked_before_spending_work()
    {
        var f = new ExcavationWorldFixture();
        var w = f.Earn(f.Genesis);
        var g = w.Gathering!;
        var first = g.Nodes[0];
        var nodes = g.Nodes.Select(n => n.NodeId == first.NodeId ? new OpenFantasy.Core.Gathering.GatherNodeState(n.NodeId,
            n.Definition, n.SourceId, n.UnitCapacity, new(128, 384, 256), n.Revision, n.NextUnissuedUnit, n.RemainingSwings, n.DepletedAtStep) : n);
        var gathering = new OpenFantasy.Core.Gathering.GatheringSnapshot(w.WorldId, w.BranchId, w.AdvancingSteps, nodes, g.Tools, g.Actors);
        w = new(w.Inventory, w.Space, w.Residents, w.AdmissionVersion, w.Calendar, w.Revision, w.AdvancingSteps,
            w.Dialogues, gathering, w.Production, w.Progression, w.Quests, w.Construction, excavation: w.Excavation);
        var result = f.Step(w, f.Command(w));
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single().Rejection);
        Assert.Equal(w.Excavation!.ContentHash, result.Candidate.Excavation!.ContentHash);
    }

    [Fact]
    public void Completed_source_cannot_be_lost_and_geometry_cannot_be_excavated_early()
    {
        var f = new ExcavationWorldFixture();
        var w = f.Genesis;
        var region = w.Space.Regions.Single(r => r.Bounds.Contains(f.Site.Cell));
        var earlySpace = new LocalSpatialSnapshot(w.Space.Scope, w.Space.Regions.Select(r => ReferenceEquals(r, region)
            ? r.Excavate(r.Revision, [f.Site.Cell]) : r), w.Space.Actors, w.Space.Structures);
        Assert.Throws<ArgumentException>(() => Copy(w, space: earlySpace));
        for (int i = 0; i < 3; i++) { w = f.Earn(w); w = f.Step(w, f.Command(w)).Candidate; }
        var depleted = Inventory(w, w.Inventory.Custody.Lots.Where(l => l.Source.SourceId != f.Site.CountedSourceId));
        Assert.Throws<ArgumentException>(() => Copy(w, inventory: depleted));
    }

    [Fact]
    public void Sequential_workers_cannot_both_use_a_stale_shared_region_revision()
    {
        var f = new ExcavationWorldFixture();
        var w = f.Basis;
        var first = f.Site;
        var other = w.Residents.Single(r => r.ActorId != f.Player);
        var otherTool = w.Inventory.Custody.Lots.First(l => l.CustodianId == other.InventoryCustodianId
            && w.Construction!.Definitions.Any(d => d.RequiredTool.Item == l.Definition));
        var cell = new CellCoordinate(1, 1, 0);
        var region = w.Space.Regions.Single(r => r.Bounds.Contains(cell));
        var second = new ExcavationSite(ExcavationWorldFixture.Id(200), cell, region.GetCell(cell).SourceId!.Value,
            ExcavationWorldFixture.Id(201), other.ActorId, first.Material, otherTool.Definition, 1, 0, new(384, 128, 0));
        var shortFirst = new ExcavationSite(first.SiteId, first.Cell, first.TerrainSourceId, first.CountedSourceId,
            first.OwnerActorId, first.Material, first.RequiredTool, 1, 0, first.Stance);
        var inventory = new CountedAuthorityState(w.Inventory.Custody, w.Inventory.AdmittedUnitCapacity + 1,
            w.Inventory.Actors, w.Inventory.Controls, w.Inventory.Policies,
            w.Inventory.Sources.Append(new(second.CountedSourceId, second.Material.Item, 1)), w.Inventory.ReservationIds);
        var basis = new WorldSnapshot(inventory, w.Space, w.Residents, w.AdmissionVersion, w.Calendar, 0, 0,
            gathering: w.Gathering, production: w.Production, progression: w.Progression, quests: w.Quests, construction: w.Construction);
        w = f.Earn(WorldExcavationAdmission.CreateGenesis(basis, new([shortFirst, second])));
        WorldActionCommand Command(ExcavationSite site, EntityId toolId)
        {
            var delivery = ConstructionUpgradeFixture.Delivery(w, site.OwnerActorId);
            var tool = w.Inventory.Custody.GetLot(toolId);
            return new(f.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, site.OwnerActorId, w.WorldId, w.BranchId,
                w.Space.GetActor(site.OwnerActorId).Revision, new ExcavateIntent(site.SiteId, 0, region.Revision, toolId, tool.Revision,
                    w.Inventory.Custody.GetStorage(w.GetResident(site.OwnerActorId).InventoryCustodianId).Revision));
        }
        var batch = f.Actions.Batch(w, [Command(shortFirst, f.Tool), Command(second, otherTool.LotId)], []);
        var result = new WorldStepProcessor().Decide(w, batch);
        Assert.Equal(WorldActionRejection.None, result.Receipts.Single(r => r.CommandId == batch.Actions[0].CommandId).Rejection);
        Assert.Equal(WorldActionRejection.RevisionConflict, result.Receipts.Single(r => r.CommandId == batch.Actions[1].CommandId).Rejection);
        Assert.Single(result.Candidate.Excavation!.Sites, site => site.IsComplete);
        Assert.Equal(1UL, result.Candidate.Inventory.Custody.Lots.Where(l => l.Source.SourceId == first.CountedSourceId
            || l.Source.SourceId == second.CountedSourceId).Aggregate(0UL, (n, l) => n + l.Source.Quantity));
    }

    private static WorldActionCommand Replace(WorldActionCommand c, WorldActionIntent intent) => new(c.CommandId,
        c.PrincipalId, c.OriginSessionId, c.ActorId, c.WorldId, c.BranchId, c.ExpectedActorRevision, intent);
    internal static WorldSnapshot Copy(WorldSnapshot w, CountedAuthorityState? inventory = null, LocalSpatialSnapshot? space = null,
        ProductionSnapshot? production = null, ExcavationSnapshot? excavation = null) => new(inventory ?? w.Inventory, space ?? w.Space,
        w.Residents, w.AdmissionVersion, w.Calendar, w.Revision, w.AdvancingSteps, w.Dialogues, w.Gathering, production ?? w.Production,
        w.Progression, w.Quests, w.Construction, excavation: excavation ?? w.Excavation);
    internal static CountedAuthorityState Inventory(WorldSnapshot w, IEnumerable<CountedSourceLot> lots) => new(
        new(w.WorldId, w.BranchId, w.Inventory.Custody.Revision, w.Inventory.Custody.Storages, lots), w.Inventory.AdmittedUnitCapacity,
        w.Inventory.Actors, w.Inventory.Controls, w.Inventory.Policies, w.Inventory.Sources, w.Inventory.ReservationIds);
}

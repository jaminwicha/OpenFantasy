using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

/// <summary>Fresh unworked catalogs for admission tests; candidate holes below are diagnostic inputs, not played excavation.</summary>
internal static class EventSettlementFixture
{
    internal static readonly EntityId Player = AdmissionFixture.Id(10);
    internal static readonly CellCoordinate Removed = new(0, 0, 0);
    internal static EntityId Id(ulong value) => new(0x6576656e74776f72, value);

    internal static WorldSnapshot Create17()
    {
        WorldSnapshot catalogs = ConstructionWorldFixture.Basis(completed: false);
        WorldConstructionAdmission admission = ConstructionWorldFixture.Admission(catalogs);
        CountedAuthorityState inventory = admission.CreateGenesisInventory(catalogs.Inventory);
        var definition = Assert.Single(admission.Construction.Definitions);
        var production = new ProductionSnapshot(catalogs.WorldId, catalogs.BranchId, 0,
            [definition.MaterialTransform], [], []);
        ActorPresence[] actors = catalogs.Space.Actors.Select(actor => actor.ActorId == Player
            ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, new(128, 128, 256), 0, actor.HomePlaceId)
            : actor).ToArray();
        var space = new LocalSpatialSnapshot(catalogs.Space.Scope, catalogs.Space.Regions, actors);
        WorldSnapshot construction = new(inventory, space, catalogs.Residents,
            ContentHash.Compute("fresh-event-settlement-test"u8), catalogs.Calendar, 0, 0, [], catalogs.Gathering,
            production, catalogs.Progression, catalogs.Quests, admission.Construction);

        TerrainCell cell = Assert.Single(space.Regions).GetCell(Removed);
        var site = new ExcavationSite(Id(1), Removed, cell.SourceId!.Value, Id(2), Player,
            definition.MaterialTransform.BindingFor(cell.Material!), GatheringFixture.Axe, 1, 0, new(-128, 128, 0));
        inventory = new(inventory.Custody, checked(inventory.AdmittedUnitCapacity + site.Quantity), inventory.Actors,
            inventory.Controls, inventory.Policies, inventory.Sources.Append(new(site.CountedSourceId, site.Material.Item, site.Quantity)),
            inventory.ReservationIds);
        construction = new(inventory, space, construction.Residents, construction.AdmissionVersion, construction.Calendar,
            0, 0, [], construction.Gathering, production, construction.Progression, construction.Quests, admission.Construction);
        WorldSnapshot physical = WorldConstructionPhysicalAdmission.CreateGenesis(WorldSteppedSupportAdmission.CreateGenesis(
            WorldConstructedSupportAdmission.CreateGenesis(WorldExcavationAdmission.CreateGenesis(construction, new([site])))));

        // The definition is unbuilt. Full ratings are retained; no successful construction or joint load is claimed.
        var template = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version,
            definition.Members.Select((_, index) => new StructuralMemberRating((uint)index, new(1_000_000_000), 1000)), []);
        StableTerrainBearing[] foundations = space.Regions.SelectMany(region => region.Cells).Where(value => value.IsSolid)
            .Select((value, index) => new StableTerrainBearing(Id(checked(1000UL + (ulong)index)), value.Coordinate,
                value.SourceId!.Value, value.Material!, new(1_000_000_000))).ToArray();
        var bodies = actors.Select(actor => new StructuralBodyMass(actor.Binding.DefinitionId,
            actor.Binding.DefinitionVersion, new(1000))).DistinctBy(value => value.DefinitionId);
        var masses = new StructuralMassCatalog(bodies,
            [new(GatheringFixture.Axe, new(7)), new(definition.RequiredTool.Item, new(11))]);
        WorldSnapshot structural = WorldStructuralPolicyAdmission.CreateGenesis(physical,
            new(space.Scope, [template], foundations, masses));
        return WorldCoupledSettlementAdmission.CreateGenesis(WorldConsequenceAdmission.CreateGenesis(
            WorldStructuralSettlementAdmission.CreateGenesis(structural, new(256, 256, 256, 9800)), new(1)), new(2));
    }

    internal static TerrainRegionSnapshot[] CandidateRegions(WorldSnapshot world) => world.Space.Regions
        .Select(region => region.Bounds.Contains(Removed) ? region.Excavate(region.Revision, [Removed]) : region).ToArray();

    internal static WorldSnapshot Copy(WorldSnapshot value, IEnumerable<DialogueSession>? dialogues = null,
        ConstructionDescentMode? mode = null, bool coupled = true) => new(value.Inventory,
        coupled ? value.Space : new LocalSpatialSnapshot(value.Space.Scope, value.Space.Regions, value.Space.Actors,
            value.Space.Structures, value.Space.GroundSupport, StructureSupportMode.TerrainOnly),
        value.Residents, value.AdmissionVersion, value.Calendar, value.Revision, value.AdvancingSteps,
        dialogues ?? value.Dialogues, value.Gathering, value.Production, value.Progression, value.Quests,
        value.Construction, excavation: value.Excavation, constructionPhysical: value.ConstructionPhysical,
        structuralPolicy: value.StructuralPolicy, structuralSettlementPolicy: value.StructuralSettlementPolicy,
        consequencePolicy: value.ConsequencePolicy, coupledSettlementPolicy: coupled ? value.CoupledSettlementPolicy : null,
        descentMode: mode ?? value.DescentMode);

    internal static WorldStepBatch Step(WorldSnapshot world, ulong ordinal, bool move = false) =>
        new(Id(10_000 + ordinal), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
            WorldBatchKind.AdvancingStep, move ? [StepFixture.Move(world, 10)] : [], [], world.ProfileVersion);
}

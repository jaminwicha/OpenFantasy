using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class ExcavationWorldFixture
{
    internal static EntityId Id(ulong n) => new(0x6578636176617465, n);
    internal ConstructionUpgradeFixture Actions { get; } = new();
    internal WorldSnapshot Basis { get; }
    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Tool { get; }
    internal ExcavationSite Site { get; }
    internal static readonly CellCoordinate Target = new(0, 1, 0);

    internal ExcavationWorldFixture(bool npc = false, bool occupiedAbove = false)
    {
        var farm = ConstructionFarmAdmission.CreateGenesis(Actions.QuestGenesis());
        Player = farm.Residents.Single(r => r.ControllerKind == (npc ? ResidentControllerKind.Host : ResidentControllerKind.Player)).ActorId;
        var original = farm.Space.Regions[0];
        var region = new StratifiedTerrainGenerator().Generate(farm.Space.Scope, original.Seed, original.Rules,
            new(new(-3, -3, -2), 9, 7, 7));
        region = region.Excavate(0, Enumerable.Range(-2, 6).Select(x => new CellCoordinate(x, 0, 0)));
        var actors = farm.Space.Actors.Select(a => new ActorPresence(a.Binding, a.Condition, a.Scope, a.ActorId == Player ? new(128, 128, 0)
            : occupiedAbove ? new(128, 384, 256) : new(640, 128, 0), 0, a.HomePlaceId));
        var space = new LocalSpatialSnapshot(farm.Space.Scope, farm.Space.Regions.Append(region), actors);
        var bag = farm.GetResident(Player).InventoryCustodianId;
        var tool = farm.Inventory.Custody.Lots.First(l => l.CustodianId == bag && l.ReservedBy is null
            && (farm.Gathering!.Tools.Any(t => t.Definition == l.Definition) || farm.Construction!.Definitions.Any(d => d.RequiredTool.Item == l.Definition)));
        Tool = tool.LotId;
        var cell = region.GetCell(Target);
        var material = new ItemPhysicalBinding(cell.Material!, new(new PackageId("openfantasy.excavation"), Id(2),
            "substrate", new(2_500_000_000), new(1_000_000_000), new(4)));
        Site = new(Id(1), Target, cell.SourceId!.Value, Id(3), Player, material, tool.Definition, 3, 0, new(128, 128, 0));
        var inventory = new CountedAuthorityState(farm.Inventory.Custody, farm.Inventory.AdmittedUnitCapacity + 1,
            farm.Inventory.Actors, farm.Inventory.Controls, farm.Inventory.Policies,
            farm.Inventory.Sources.Append(new(Id(3), cell.Material!, 1)), farm.Inventory.ReservationIds);
        Basis = new(inventory, space, farm.Residents, farm.AdmissionVersion, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: farm.Production, progression: farm.Progression,
            quests: farm.Quests, construction: farm.Construction);
        Genesis = WorldExcavationAdmission.CreateGenesis(Basis, new([Site]));
    }

    internal WorldActionCommand Command(WorldSnapshot world, ulong? expectedSite = null)
    {
        var site = world.Excavation!.Sites.Single();
        var delivery = ConstructionUpgradeFixture.Delivery(world, Player);
        var tool = world.Inventory.Custody.Lots.Single(l => l.LotId == Tool);
        return new(Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, Player, world.WorldId, world.BranchId,
            world.Space.GetActor(Player).Revision, new ExcavateIntent(site.SiteId, expectedSite ?? site.CompletedWork,
                world.Space.Regions.Single(r => r.Bounds.Contains(Target)).Revision, Tool, tool.Revision,
                world.Inventory.Custody.GetStorage(world.GetResident(Player).InventoryCustodianId).Revision));
    }

    internal WorldStepDecision Step(WorldSnapshot world, WorldActionCommand? command = null) => new WorldStepProcessor().Decide(world,
        Actions.Batch(world, command is null ? [] : [command], []));
    internal WorldSnapshot Earn(WorldSnapshot world)
    {
        for (int i = 0; i < 40; i++) world = Step(world).Candidate;
        return world;
    }
}

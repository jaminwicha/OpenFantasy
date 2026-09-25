using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Terrain;

/// <summary>
/// Authored geometry for the public planner's domain contract. Empty committed steps earn labor;
/// the separately supplied extraction catalog is not a claim of admission into a live world profile.
/// </summary>
internal sealed class ExcavationRaisedBankFixture
{
    internal static EntityId Id(ulong value) => new(0x7261697365646261, value);
    internal static readonly EntityId Player = AdmissionFixture.Id(10);
    internal static readonly CellCoordinate Target = new(0, 0, 0);
    internal WorldSnapshot World { get; }
    internal ExcavationSite Site { get; }
    internal ExcavationSnapshot Excavation { get; }

    internal ExcavationRaisedBankFixture(SubunitPosition? actorFeet = null, bool coveredTarget = false,
        IEnumerable<CellCoordinate>? holes = null, SubunitPosition? stance = null,
        ExcavationReachMode mode = ExcavationReachMode.RaisedBankTop, ulong requiredWork = 2, int earnedSteps = 40)
    {
        WorldSnapshot basis = GatheringFixture.Create(effort: 0);
        var material = AdmissionFixture.Material;
        var region = new StratifiedTerrainGenerator().Generate(basis.Space.Scope, Id(1),
            new StratifiedTerrainRules(1, 0, 1, new(material, material, material)),
            new(new(-3, -3, -1), 9, 7, 5));
        CellCoordinate[] cleared = region.Cells.Where(cell => cell.IsSolid && cell.Coordinate.Z > 0
            && !(coveredTarget && cell.Coordinate == new CellCoordinate(0, 0, 1)))
            .Select(cell => cell.Coordinate).Concat(holes ?? []).ToArray();
        foreach (CellCoordinate[] batch in cleared.Chunk(TerrainRegionSnapshot.MaximumExcavationBatch))
        {
            region = region.Excavate(region.Revision, batch);
        }
        var actors = basis.Space.Actors.Select(actor => new ActorPresence(actor.Binding, actor.Condition,
            actor.Scope, actor.ActorId == Player ? actorFeet ?? new(-128, 128, 256) : new(1024, 512, 256),
            actor.Revision, actor.HomePlaceId));
        var space = new LocalSpatialSnapshot(basis.Space.Scope, [region], actors);
        TerrainCell cell = region.GetCell(Target);
        Site = new(Id(2), Target, cell.SourceId!.Value, Id(3), Player,
            new(material, new(new PackageId("raised-bank-test"), Id(4), "substrate",
                new(2_500_000_000), new(ExcavationSite.CellVolumeMicrolitres), new(4))),
            GatheringFixture.Axe, requiredWork, 0, stance ?? new(-128, 128, 256), mode);
        Excavation = new([Site]);
        var inventory = new CountedAuthorityState(basis.Inventory.Custody,
            checked(basis.Inventory.AdmittedUnitCapacity + Site.Quantity), basis.Inventory.Actors,
            basis.Inventory.Controls, basis.Inventory.Policies,
            basis.Inventory.Sources.Append(new(Site.CountedSourceId, material, Site.Quantity)));
        var gathering = new GatheringSnapshot(basis.WorldId, basis.BranchId, 0, [], basis.Gathering!.Tools,
            basis.Gathering.Actors);
        WorldSnapshot current = new(inventory, space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
            0, 0, gathering: gathering, production: new(basis.WorldId, basis.BranchId, 0, [], [], []));
        for (int step = 0; step < earnedSteps; step++)
        {
            current = new WorldStepProcessor().Decide(current, new(Id(checked(100UL + (ulong)step)),
                current.WorldId, current.BranchId, current.Revision, current.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [], current.ProfileVersion)).Candidate;
        }
        World = current;
    }

    internal WorldActionCommand Command(ulong? actorRevision = null, ulong? toolRevision = null)
    {
        ActorControlBinding control = World.Inventory.Controls.Single(value => value.ActorId == Player);
        var bag = World.Inventory.Custody.GetStorage(World.GetResident(Player).InventoryCustodianId);
        return new(Id(1000), control.PrincipalId, control.SessionId, Player, World.WorldId, World.BranchId,
            actorRevision ?? World.Space.GetActor(Player).Revision,
            new ExcavateIntent(Site.SiteId, Site.Revision, World.Space.Regions.Single().Revision,
                GatheringFixture.ToolLot, toolRevision ?? 0, bag.Revision));
    }

    internal ExcavationPlan Plan(WorldActionCommand? command = null, bool advancing = true, bool held = false) =>
        new ExcavationPlanner().Plan(World, World.Inventory, World.Space, World.Gathering!, World.Production!,
            Excavation, command ?? Command(), advancing, held, World.IdentityIndex.Contains);

    internal static ExcavationSite Copy(ExcavationSite site, SubunitPosition stance,
        ExcavationReachMode mode, EntityId? id = null, CellCoordinate? cell = null) =>
        new(id ?? site.SiteId, cell ?? site.Cell, site.TerrainSourceId, site.CountedSourceId,
            site.OwnerActorId, site.Material, site.RequiredTool, site.RequiredWork, site.CompletedWork, stance, mode);
}

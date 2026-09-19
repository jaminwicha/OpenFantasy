using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class ReservoirBoundaryFixture
{
    internal ReservoirWorldFixture Materials { get; } = new();
    internal WorldSnapshot World { get; }
    internal ReservoirSealPolicy Policy { get; }
    internal static EntityId SourceReservoir => ReservoirWorldFixture.Id(30);

    internal ReservoirBoundaryFixture(bool splitRegions = false, IEnumerable<CellCoordinate>? extraHoles = null,
        bool stepped = false, bool leaveSubstrate = false, bool missingWest = false)
    {
        var basis = Materials.Basis;
        var source = new ReservoirGeometry(basis.Space.Scope, SourceReservoir, ReservoirWorldFixture.SourceStore, 0,
            stepped ? [new(0, 0, -2), new(1, 0, -1)] : [new(0, 0, -2)]);
        var receiver = new ReservoirGeometry(basis.Space.Scope, ReservoirWorldFixture.Id(31), ReservoirWorldFixture.Receiver,
            0, [new(3, 0, -2)]);
        var terrainBasis = basis.Space.Regions[0];
        List<TerrainRegionBounds> bounds = splitRegions
            ? [new(new(-2, -2, -4), 3, 5, 8), new(new(1, -2, -4), 5, 5, 8)]
            : [new(new(missingWest ? 0 : -2, -2, -4), missingWest ? 6 : 8, 5, 8)];
        var holes = leaveSubstrate ? [] : source.Columns.Concat(receiver.Columns)
            .SelectMany(c => Enumerable.Range(0, checked((int)-c.BottomZ)).Select(z => new CellCoordinate(c.X, c.Y, c.BottomZ + z)))
            .Concat(extraHoles ?? []).ToArray();
        var regions = bounds.Select(b =>
        {
            var generated = new StratifiedTerrainGenerator().Generate(basis.Space.Scope, terrainBasis.Seed, terrainBasis.Rules, b);
            var localHoles = holes.Where(b.Contains).ToArray();
            return localHoles.Length == 0 ? generated : generated.Excavate(0, localHoles);
        });
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions.Concat(regions), basis.Space.Actors);
        var physical = LiquidReservoirState.Create(Materials.Reservoirs.Capacity, [source, receiver]);
        World = WorldReservoirAdmission.CreateGenesis(Copy(basis, space), physical);
        Policy = new(ReservoirWorldFixture.Water, new[] { terrainBasis.Rules.Materials.Surface, terrainBasis.Rules.Materials.Soil, terrainBasis.Rules.Materials.Stone }.Distinct().Select(m => new ReservoirSealRule(m, 1000)));
    }

    internal static WorldSnapshot Copy(WorldSnapshot basis, LocalSpatialSnapshot space, LiquidReservoirState? reservoirs = null) =>
        new(basis.Inventory, space, basis.Residents, basis.AdmissionVersion, basis.Calendar, basis.Revision,
            basis.AdvancingSteps, basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, reservoirs: reservoirs);

    internal ReservoirBoundaryAssessment Assess() => new ReservoirBoundaryEvaluator().Assess(World, Policy, TestContext.Current.CancellationToken);
}

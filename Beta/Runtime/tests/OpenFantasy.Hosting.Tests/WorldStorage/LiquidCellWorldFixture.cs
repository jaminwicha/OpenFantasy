using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class LiquidCellWorldFixture
{
    internal ReservoirWorldFixture Materials { get; }
    internal WorldSnapshot ContainedBasis { get; }
    internal WorldSnapshot Genesis { get; }
    internal ReservoirSealPolicy Policy { get; }
    internal static EntityId SourceReservoir => ReservoirWorldFixture.Id(30);
    internal static EntityId ReceiverReservoir => ReservoirWorldFixture.Id(31);
    internal static LiquidCellKey SourceCell(long x, long z) => new(SourceReservoir, new(x, 0, z));

    internal LiquidCellWorldFixture(bool primaryReceiver = false)
    {
        Materials = new(primaryReceiver: primaryReceiver, canceled: true);
        var basis = Materials.Basis;
        var source = new ReservoirGeometry(basis.Space.Scope, SourceReservoir,
            ReservoirWorldFixture.SourceStore, 0,
            [new(0, 0, -1), new(1, 0, -1), new(2, 0, -2), new(3, 0, -2)]);
        var receiver = new ReservoirGeometry(basis.Space.Scope, ReceiverReservoir,
            ReservoirWorldFixture.Receiver, 0, [new(6, 0, -2)]);
        var terrainBasis = basis.Space.Regions[0];
        var bounds = new TerrainRegionBounds(new(-2, -2, -4), 11, 5, 8);
        var holes = source.Columns.Concat(receiver.Columns).SelectMany(column =>
            Enumerable.Range(0, checked((int)-column.BottomZ))
                .Select(z => new CellCoordinate(column.X, column.Y, column.BottomZ + z))).ToArray();
        var terrain = new StratifiedTerrainGenerator().Generate(basis.Space.Scope,
            terrainBasis.Seed, terrainBasis.Rules, bounds).Excavate(0, holes);
        var space = new LocalSpatialSnapshot(basis.Space.Scope,
            basis.Space.Regions.Append(terrain), basis.Space.Actors);
        var reservoirs = LiquidReservoirState.Create(Materials.Reservoirs.Capacity, [source, receiver]);
        var reservoirWorld = WorldReservoirAdmission.CreateGenesis(
            ReservoirBoundaryFixture.Copy(basis, space), reservoirs);
        Policy = new(ReservoirWorldFixture.Water,
            new[] { terrainBasis.Rules.Materials.Surface, terrainBasis.Rules.Materials.Soil,
                terrainBasis.Rules.Materials.Stone }.Distinct().Select(material => new ReservoirSealRule(material, 1000)));
        ContainedBasis = WorldReservoirContainmentAdmission.CreateGenesis(reservoirWorld, Policy);
        var cells = LiquidCellState.Create(reservoirs, new(200, 200), ContainedBasis.Calendar,
            [new(SourceCell(0, -1), LiquidStockOrigin.RawSource, ReservoirWorldFixture.Source, 0, 650),
                new(SourceCell(1, -1), LiquidStockOrigin.RawSource, ReservoirWorldFixture.Source, 650, 1300),
                new(new(ReceiverReservoir, new(6, 0, -2)), LiquidStockOrigin.RawSource,
                    ReservoirWorldFixture.Source, 2000, 2050)]);
        Genesis = WorldLiquidCellAdmission.CreateGenesis(ContainedBasis, cells);
    }
}

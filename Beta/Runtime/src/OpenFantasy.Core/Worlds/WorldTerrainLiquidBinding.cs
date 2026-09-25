using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins excavated voids, conserved liquid placement and dry occupants in a finite vented envelope.</summary>
internal static class WorldTerrainLiquidBinding
{
    internal static void Validate(WorldSnapshot world)
    {
        if (world.LiquidCells?.Topology is not { } retained || world.Reservoirs is null
            || world.ReservoirSeals is null || world.Excavation is null || world.Transport is not null
            || world.ConstructionPhysical is not null || world.StructuralPolicy is not null)
        {
            throw new ArgumentException("Terrain-bound liquid requires its complete dry excavation and fixed material owners.");
        }
        var actual = TerrainLiquidTopology.Create(world.Reservoirs, world.Space.Regions, world.ReservoirSeals);
        if (actual.ContentHash != retained.ContentHash)
        {
            throw new ArgumentException("Liquid topology must describe the world's exact retained terrain.");
        }
        ValidateDryEnvelope(world, world.Space, actual);
        // Exact coverage is already enforced by LiquidCellState. Every normalized portion contains
        // at least one indivisible unit, so this bounds every possible later fragmentation pattern.
        UInt128 units = world.LiquidCells.Portions.Aggregate((UInt128)0, (sum, portion) => sum + portion.Quantity);
        if (units > LiquidCellState.MaximumPortions)
        {
            throw new ArgumentException("Terrain-bound liquid exceeds its finite provenance certificate.");
        }
    }

    internal static void ValidateOpening(WorldSnapshot world, LocalSpatialSnapshot proposedSpace)
    {
        var reservoirs = world.Reservoirs ?? throw new ArgumentException("An opening needs the retained material owner.");
        var seals = world.ReservoirSeals ?? throw new ArgumentException("An opening needs retained sealing rules.");
        var topology = TerrainLiquidTopology.Create(reservoirs, proposedSpace.Regions, seals);
        _ = world.LiquidCells!.RebindTerrain(reservoirs, topology);
        ValidateDryEnvelope(world, proposedSpace, topology);
    }

    private static void ValidateDryEnvelope(WorldSnapshot world, LocalSpatialSnapshot space, TerrainLiquidTopology topology)
    {
        var bodies = space.Actors.Select(ReservoirBoundaryBox.FromBody).ToArray();
        var members = world.Construction!.Projects.Where(project => project.Installed is not null)
            .SelectMany(project => project.Definition.Members.Select(member =>
                ReservoirBoundaryBox.FromMember(project.SiteFeet, member))).ToArray();
        // The declared ambient cell also remains clear: a roof at the spill plane cannot become
        // an implicitly vented chamber, and a dry-capacity proof cannot authorize occupied water.
        IEnumerable<CellCoordinate> cells = topology.NominalCells.Select(cell => cell.Coordinate)
            .Concat(world.Reservoirs!.Geometries.SelectMany(geometry => geometry.Columns
                .Select(column => new CellCoordinate(column.X, column.Y, geometry.SpillZ))));
        var envelope = cells.ToArray();
        if ((long)envelope.Length * (bodies.Length + members.Length + 1) > ReservoirBoundaryEvaluator.MaximumDependencyChecks)
        {
            throw new ArgumentException("Terrain liquid occupancy exceeds the finite containment work bound.");
        }
        foreach (CellCoordinate cell in envelope)
        {
            var box = ReservoirBoundaryBox.FromCell(cell);
            if (bodies.Any(box.Overlaps) || members.Any(box.Overlaps))
            {
                throw new ArgumentException("The potential wet envelope and its vents must remain clear of bodies and installed members.");
            }
        }
    }
}

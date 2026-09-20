using OpenFantasy.Core;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Finite fresh-seed selection under one retained generator, with an accessible natural contour.</summary>
internal static class WorksiteFarmTerrain
{
    private const int MaximumSeedCandidates = 16_384;

    internal static IReadOnlyList<TerrainRegionSnapshot> Create(WorldSnapshot farm)
    {
        TerrainRegionSnapshot retained = farm.Space.Regions.Single();
        var generator = new StratifiedTerrainGenerator();
        var rules = new StratifiedTerrainRules(0, 1, retained.Rules.SoilDepth, retained.Rules.Materials);
        CellCoordinate[] levelCells = LevelCorridorCells().ToArray();
        for (ulong ordinal = 0; ordinal < MaximumSeedCandidates; ordinal++)
        {
            EntityId seed = EntityId.DeriveGenerated(retained.Seed.ToByteArray(),
                "worksite-farm.terrain-search.v1", "contour", "candidate", ordinal);
            // Most seeds are discarded using only four columns. The larger corridor and complete
            // farm are generated only after the existing rule yields this exact accessible face.
            TerrainRegionSnapshot face = generator.Generate(farm.Space.Scope, seed, rules,
                new TerrainRegionBounds(new CellCoordinate(115, 324, -1), 2, 2, 3));
            if (!HasSurface(face, WorksiteFarmLayout.SupportCell)
                || !HasSurface(face, WorksiteFarmLayout.PitApproach.Cell.Offset(0, 0, -1))
                || !HasSurface(face, WorksiteFarmLayout.PitCell.Offset(0, 0, -1)))
            {
                continue;
            }
            TerrainRegionSnapshot corridor = generator.Generate(farm.Space.Scope, seed, rules,
                new TerrainRegionBounds(new CellCoordinate(100, 323, 0), 17, 10, 1));
            if (!levelCells.All(cell => HasSurface(corridor, cell)))
            {
                continue;
            }
            // Selection occurs only while authoring a fresh root. Nothing rerolls or edits a lived
            // region, issues its substrate, or supplies an excavated endpoint.
            return
            [
                generator.Generate(farm.Space.Scope, seed, rules, retained.Bounds),
                generator.Generate(farm.Space.Scope, seed, rules,
                    new TerrainRegionBounds(new CellCoordinate(96, 320, -1), 32, 32, 1))
            ];
        }
        throw new InvalidDataException("The worksite's finite fresh-terrain search found no admissible contour and corridors.");
    }

    private static bool HasSurface(TerrainRegionSnapshot region, CellCoordinate cell) =>
        region.GetCell(cell).Stratum == TerrainStratum.Surface;

    private static IEnumerable<CellCoordinate> LevelCorridorCells()
    {
        HashSet<CellCoordinate> cells = [];
        for (long x = 100; x <= 110; x++)
        {
            for (long y = 323; y <= 327; y++) { cells.Add(new(x, y, 0)); }
        }
        for (long x = 106; x <= 116; x++) { cells.Add(new(x, 323, 0)); }
        for (long x = 106; x <= 113; x++) { cells.Add(new(x, 324, 0)); }
        for (long x = 106; x <= 115; x++) { cells.Add(new(x, 325, 0)); }
        for (long y = 328; y <= 332; y++) { cells.Add(new(105, y, 0)); }
        cells.Add(new(112, 327, 0));
        cells.Add(new(113, 327, 0));
        cells.Add(new(116, 324, 0));
        return cells.OrderBy(cell => cell.X).ThenBy(cell => cell.Y);
    }
}

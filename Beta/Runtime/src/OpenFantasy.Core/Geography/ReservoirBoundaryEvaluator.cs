using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Reads the same retained terrain and installed material as the world; never invents wall stock or an implicit gate seal.</summary>
public sealed class ReservoirBoundaryEvaluator
{
    public const long MaximumInteriorCells = 65_536;
    public const long MaximumBoundaryFaces = 65_536;
    public const long MaximumDependencyChecks = 4_194_304;
    private static readonly (long X, long Y, ReservoirBoundarySurface Surface)[] Sides =
    [(-1, 0, ReservoirBoundarySurface.West), (1, 0, ReservoirBoundarySurface.East),
     (0, -1, ReservoirBoundarySurface.North), (0, 1, ReservoirBoundarySurface.South)];

    public ReservoirBoundaryAssessment Assess(WorldSnapshot world, ReservoirSealPolicy policy,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ReservoirBoundaryResult> results = AssessBoundaries(world, policy, cancellationToken);
        return new(world.ContentHash, world.Reservoirs!.ContentHash, policy.Version, results);
    }

    // Complete root admission consumes the same geometry without requesting a hash of the root
    // being admitted. Public diagnostic reports add their dependency pins only after evaluation.
    internal IReadOnlyList<ReservoirBoundaryResult> AssessBoundaries(WorldSnapshot world, ReservoirSealPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();
        var reservoirs = world.Reservoirs ?? throw new ArgumentException("Boundary assessment requires the complete physical reservoir world.");
        if (policy.Liquid != reservoirs.Capacity.Domain.Material.Item)
        {
            throw new ArgumentException("The sealing policy must name the world's exact stored liquid content pin.");
        }
        var members = ReadMembers(world);
        var bodies = world.Space.Actors.Select(ReservoirBoundaryBox.FromBody).ToArray();
        var terrain = new TerrainVolumeQueries(world.Space.Regions);
        var budgets = reservoirs.Geometries.Select(geometry => Count(geometry)).ToArray();
        long interiors = budgets.Sum(c => c.Interior), faces = budgets.Sum(c => c.Faces);
        long dependencyChecks = checked((interiors + faces) * (world.Space.Regions.Count + members.Length + bodies.Length + 1));
        bool exceedsBudget = interiors > MaximumInteriorCells || faces > MaximumBoundaryFaces
            || dependencyChecks > MaximumDependencyChecks;
        List<ReservoirBoundaryResult> results = [];
        for (int index = 0; index < reservoirs.Geometries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var geometry = reservoirs.Geometries[index];
            var result = new AssessmentBuilder(geometry.ReservoirId, budgets[index].Interior, budgets[index].Faces);
            if (exceedsBudget)
            {
                result.Fail(ReservoirBoundaryFailure.WorkLimit, new(geometry.Columns[0].X, geometry.Columns[0].Y, geometry.Columns[0].BottomZ), ReservoirBoundarySurface.Interior);
            }
            else
            {
                AssessGeometry(geometry, terrain, members, bodies, policy, result, cancellationToken);
            }
            if (world.Transport is { } transport)
            {
                foreach (var gate in transport.Connections.Where(c => c.SourceReservoirId == geometry.ReservoirId || c.ReceiverReservoirId == geometry.ReservoirId))
                {
                    bool source = gate.SourceReservoirId == geometry.ReservoirId;
                    result.Fail(ReservoirBoundaryFailure.UnprovedGate,
                        new(source ? gate.SourceX : gate.ReceiverX, source ? gate.SourceY : gate.ReceiverY, gate.SillZ), ReservoirBoundarySurface.Gate);
                }
            }
            results.Add(result.Build());
        }
        cancellationToken.ThrowIfCancellationRequested();
        return results.AsReadOnly();
    }

    private static ReservoirBoundaryMember[] ReadMembers(WorldSnapshot world) => world.Construction!.Projects
        .Where(p => p.Installed is not null).SelectMany(project =>
        {
            var definition = project.Definition;
            var output = definition.MaterialTransform.ItemFor(definition.MaterialTransform.Outputs[(int)definition.InstalledOutputIndex]);
            return definition.Members.Select(member => new ReservoirBoundaryMember(output,
                ReservoirBoundaryBox.FromMember(project.SiteFeet, member)));
        }).ToArray();

    private static (long Interior, long Faces) Count(ReservoirGeometry geometry)
    {
        var columns = geometry.Columns.ToDictionary(c => (c.X, c.Y));
        long interior = 0, faces = geometry.Columns.Count;
        foreach (var column in geometry.Columns)
        {
            interior += (long)((Int128)geometry.SpillZ - column.BottomZ);
            foreach (var side in Sides)
            {
                faces += ExposedHeight(geometry, column, columns, side.X, side.Y);
            }
        }
        return (interior, faces);
    }

    private static void AssessGeometry(ReservoirGeometry geometry, TerrainVolumeQueries terrain,
        ReservoirBoundaryMember[] members, ReservoirBoundaryBox[] bodies, ReservoirSealPolicy policy,
        AssessmentBuilder result, CancellationToken cancellationToken)
    {
        var columns = geometry.Columns.ToDictionary(c => (c.X, c.Y));
        foreach (var column in geometry.Columns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int depth = checked((int)((Int128)geometry.SpillZ - column.BottomZ));
            for (int layer = 0; layer < depth; layer++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cell = new CellCoordinate(column.X, column.Y, checked(column.BottomZ + layer));
                var retained = terrain.FindCell(cell);
                if (retained is null)
                {
                    result.Fail(ReservoirBoundaryFailure.MissingTerrain, cell, ReservoirBoundarySurface.Interior);
                }
                else if (retained.IsSolid)
                {
                    result.Fail(ReservoirBoundaryFailure.InteriorTerrain, cell, ReservoirBoundarySurface.Interior);
                }
                var volume = ReservoirBoundaryBox.FromCell(cell);
                if (members.Any(m => m.Box.Overlaps(volume)))
                {
                    result.Fail(ReservoirBoundaryFailure.InteriorConstruction, cell, ReservoirBoundarySurface.Interior);
                }
                if (bodies.Any(b => b.Overlaps(volume)))
                {
                    result.Fail(ReservoirBoundaryFailure.InteriorActor, cell, ReservoirBoundarySurface.Interior);
                }
            }
            AssessFace(new(column.X, column.Y, column.BottomZ), ReservoirBoundarySurface.Floor,
                0, 0, -1, terrain, members, policy, result, cancellationToken);
            foreach (var side in Sides)
            {
                int height = ExposedHeight(geometry, column, columns, side.X, side.Y);
                for (int layer = 0; layer < height; layer++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AssessFace(new(column.X, column.Y, checked(column.BottomZ + layer)), side.Surface,
                        side.X, side.Y, 0, terrain, members, policy, result, cancellationToken);
                }
            }
        }
    }

    private static int ExposedHeight(ReservoirGeometry geometry, ReservoirColumn column,
        Dictionary<(long, long), ReservoirColumn> columns, long dx, long dy)
    {
        var origin = new CellCoordinate(column.X, column.Y, column.BottomZ);
        long top = TryOffset(origin, dx, dy, 0, out var neighbor) && columns.TryGetValue((neighbor.X, neighbor.Y), out var other)
            ? Math.Min(geometry.SpillZ, other.BottomZ) : geometry.SpillZ;
        return checked((int)Int128.Max(0, (Int128)top - column.BottomZ));
    }

    private static void AssessFace(CellCoordinate cell, ReservoirBoundarySurface surface, long dx, long dy, long dz,
        TerrainVolumeQueries terrain, ReservoirBoundaryMember[] members, ReservoirSealPolicy policy,
        AssessmentBuilder result, CancellationToken cancellationToken)
    {
        if (!TryOffset(cell, dx, dy, dz, out var outside))
        {
            result.Fail(ReservoirBoundaryFailure.CoordinateLimit, cell, surface);
            return;
        }
        var neighbor = terrain.FindCell(outside);
        if (neighbor is null)
        {
            result.Fail(ReservoirBoundaryFailure.MissingTerrain, cell, surface);
            return;
        }
        if (neighbor.IsSolid && neighbor.Material is { } material && policy.MinimumThicknessFor(material) is not null)
        {
            result.TerrainFaces++;
            return;
        }
        var interior = ReservoirBoundaryBox.FromCell(cell);
        BoundaryRectangle target = interior.Projection(surface);
        List<BoundaryRectangle> coverings = [];
        foreach (var member in members)
        {
            if (policy.MinimumThicknessFor(member.Material) is not { } thickness || !member.Box.Supports(interior, surface, thickness))
            {
                continue;
            }
            var rectangle = member.Box.Projection(surface);
            if (rectangle.MinU >= target.MaxU || rectangle.MaxU <= target.MinU || rectangle.MinV >= target.MaxV || rectangle.MaxV <= target.MinV)
            {
                continue;
            }
            if (coverings.Count == BoundarySurfaceCoverage.MaximumRectangles)
            {
                result.Fail(ReservoirBoundaryFailure.WorkLimit, cell, surface);
                return;
            }
            coverings.Add(rectangle);
        }
        Int128 area = BoundarySurfaceCoverage.CoveredArea(target, coverings, cancellationToken);
        if (area == (Int128)ReservoirBoundaryBox.TicksPerCell * ReservoirBoundaryBox.TicksPerCell)
        {
            result.ConstructedFaces++;
        }
        else
        {
            result.Fail(surface == ReservoirBoundarySurface.Floor ? ReservoirBoundaryFailure.UnsealedFloor : ReservoirBoundaryFailure.UnsealedWall,
                cell, surface);
        }
    }

    private static bool TryOffset(CellCoordinate cell, long dx, long dy, long dz, out CellCoordinate candidate)
    {
        try
        {
            candidate = cell.Offset(dx, dy, dz);
            return true;
        }
        catch (OverflowException)
        {
            candidate = default;
            return false;
        }
    }

    private sealed class AssessmentBuilder(EntityId reservoirId, long interiorCells, long boundaryFaces)
    {
        private readonly Dictionary<ReservoirBoundaryFailure, long> _failures = [];
        private readonly List<ReservoirBoundarySample> _samples = [];
        internal long TerrainFaces { get; set; }
        internal long ConstructedFaces { get; set; }
        internal void Fail(ReservoirBoundaryFailure reason, CellCoordinate cell, ReservoirBoundarySurface surface)
        {
            _failures[reason] = _failures.GetValueOrDefault(reason) + 1;
            if (_samples.Count < ReservoirBoundaryResult.MaximumFailureSamples)
            {
                _samples.Add(new(reason, cell, surface));
            }
        }
        internal ReservoirBoundaryResult Build() => new(reservoirId,
            !_failures.ContainsKey(ReservoirBoundaryFailure.WorkLimit) && !_failures.ContainsKey(ReservoirBoundaryFailure.CoordinateLimit),
            interiorCells, boundaryFaces, TerrainFaces, ConstructedFaces, _failures, _samples);
    }
}

namespace OpenFantasy.Core.Geography;

/// <summary>Exact clipped rectangle-union area, measured in squared 1/256 millimetre ticks.</summary>
public static class BoundarySurfaceCoverage
{
    public const int MaximumRectangles = 256;

    public static Int128 CoveredArea(BoundaryRectangle target, IEnumerable<BoundaryRectangle> coverings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(coverings);
        cancellationToken.ThrowIfCancellationRequested();
        List<BoundaryRectangle> clipped = ClipCoverings(target, coverings, cancellationToken);
        Int128[] edges = clipped.SelectMany(rectangle => new[] { rectangle.MinU, rectangle.MaxU })
            .Distinct().Order().ToArray();
        Int128 area = 0;
        for (int index = 1; index < edges.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Int128 startU = edges[index - 1];
            Int128 endU = edges[index];
            Int128 coveredV = CoveredLength(clipped, startU, endU, cancellationToken);
            if (coveredV == 0)
            {
                continue;
            }
            Int128 width = checked(endU - startU);
            area = checked(area + checked(width * coveredV));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return area;
    }

    private static List<BoundaryRectangle> ClipCoverings(BoundaryRectangle target,
        IEnumerable<BoundaryRectangle> coverings, CancellationToken cancellationToken)
    {
        var clipped = new List<BoundaryRectangle>(MaximumRectangles);
        int count = 0;
        foreach (BoundaryRectangle rectangle in coverings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (count == MaximumRectangles)
            {
                throw new ArgumentException("Surface coverage exceeds the finite rectangle bound.", nameof(coverings));
            }
            count++;
            if (rectangle is null)
            {
                throw new ArgumentException("Surface coverings cannot contain a null rectangle.", nameof(coverings));
            }
            Int128 minU = Int128.Max(target.MinU, rectangle.MinU);
            Int128 minV = Int128.Max(target.MinV, rectangle.MinV);
            Int128 maxU = Int128.Min(target.MaxU, rectangle.MaxU);
            Int128 maxV = Int128.Min(target.MaxV, rectangle.MaxV);
            if (minU < maxU && minV < maxV)
            {
                clipped.Add(new(minU, minV, maxU, maxV));
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return clipped;
    }

    private static Int128 CoveredLength(IReadOnlyList<BoundaryRectangle> rectangles, Int128 startU,
        Int128 endU, CancellationToken cancellationToken)
    {
        BoundaryRectangle[] intervals = rectangles.Where(rectangle => rectangle.MinU <= startU && rectangle.MaxU >= endU)
            .OrderBy(rectangle => rectangle.MinV).ThenBy(rectangle => rectangle.MaxV).ToArray();
        if (intervals.Length == 0)
        {
            return 0;
        }
        Int128 startV = intervals[0].MinV;
        Int128 endV = intervals[0].MaxV;
        Int128 length = 0;
        for (int index = 1; index < intervals.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BoundaryRectangle interval = intervals[index];
            if (interval.MinV > endV)
            {
                length = checked(length + checked(endV - startV));
                startV = interval.MinV;
            }
            endV = Int128.Max(endV, interval.MaxV);
        }
        return checked(length + checked(endV - startV));
    }
}

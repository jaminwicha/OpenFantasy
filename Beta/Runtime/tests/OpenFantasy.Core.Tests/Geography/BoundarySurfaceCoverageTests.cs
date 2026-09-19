using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class BoundarySurfaceCoverageTests
{
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 1, 1, 0)]
    public void RectanglesRequireStrictPositiveExtents(long minU, long minV, long maxU, long maxV)
    {
        Assert.Throws<ArgumentException>(() => new BoundaryRectangle(minU, minV, maxU, maxV));
    }

    [Fact]
    public void OneTickGapAndSingleTickPatchKeepTheirExactArea()
    {
        var target = new BoundaryRectangle(0, 0, 256, 256);
        BoundaryRectangle[] coverings = [new(0, 0, 127, 256), new(128, 0, 256, 256)];
        Assert.Equal((Int128)65_280, Cover(target, coverings));
        Assert.Equal((Int128)65_281, Cover(target, coverings.Append(new(127, 255, 128, 256))));
        Assert.Equal((Int128)65_536, Cover(target, coverings.Append(new(127, 0, 128, 256))));
    }

    [Fact]
    public void OverlapsAndDuplicatesAreCountedOnce()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        BoundaryRectangle[] coverings = [new(0, 0, 8, 6), new(3, 3, 10, 10), new(0, 0, 8, 6), new(2, 2, 3, 3)];
        Assert.Equal((Int128)82, Cover(target, coverings));
        Assert.Equal((Int128)82, Cover(target, coverings.Reverse()));
    }

    [Fact]
    public void OnlyIntersectionWithTargetContributes()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        Assert.Equal((Int128)32, Cover(target, [new(-10, -10, 4, 4), new(6, 6, 30, 30), new(20, 20, 30, 30)]));
        Assert.Equal((Int128)100, Cover(target, [new(-10, -10, 20, 20)]));
        Assert.Equal((Int128)0, Cover(target, []));
    }

    [Fact]
    public void EdgeAndCornerContactsHaveNoArea()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        Assert.Equal((Int128)0, Cover(target,
            [new(10, 10, 12, 12), new(10, 0, 12, 10), new(-2, 3, 0, 6), new(3, -2, 7, 0), new(3, 10, 7, 12)]));
    }

    [Fact]
    public void DisjointIntervalsWithinOneSweepStripLeaveGaps()
    {
        var target = new BoundaryRectangle(-5, -5, 5, 5);
        Assert.Equal((Int128)60, Cover(target, [new(-5, -5, 5, -2), new(-5, 2, 5, 5)]));
    }

    [Fact]
    public void ExtremeCoordinatesPreserveSmallExactIntersections()
    {
        Int128 minU = Int128.MinValue + 5;
        Int128 minV = Int128.MaxValue - 40;
        var target = new BoundaryRectangle(minU, minV, minU + 20, minV + 30);
        Assert.Equal((Int128)300, Cover(target,
            [new(minU - 5, minV - 5, minU + 10, minV + 10), new(minU + 10, minV + 10, minU + 30, Int128.MaxValue)]));
    }

    [Fact]
    public void ClippingAvoidsMeasuringIrrelevantUnrepresentableExtents()
    {
        var entirePlane = new BoundaryRectangle(Int128.MinValue, Int128.MinValue, Int128.MaxValue, Int128.MaxValue);
        var target = new BoundaryRectangle(-3, -2, 3, 2);
        Assert.Equal((Int128)24, Cover(target, [entirePlane]));
        Assert.Equal((Int128)24, Cover(entirePlane, [target]));
        Assert.Equal((Int128)0, Cover(entirePlane, []));
    }

    [Fact]
    public void UncoveredWideSweepGapsDoNotOverflow()
    {
        var target = new BoundaryRectangle(Int128.MinValue, 0, Int128.MaxValue, 1);
        Assert.Equal((Int128)2, Cover(target,
            [new(Int128.MinValue, 0, Int128.MinValue + 1, 1), new(Int128.MaxValue - 1, 0, Int128.MaxValue, 1)]));
    }

    [Fact]
    public void InputOrdersMatchAnIndependentIntegerCellOracle()
    {
        var target = new BoundaryRectangle(-4, -3, 6, 5);
        BoundaryRectangle[] coverings = [new(-6, -5, 0, 2), new(-2, -1, 4, 4), new(1, -4, 7, 0), new(3, 2, 8, 6)];
        int expected = 0;
        for (int u = -4; u < 6; u++)
        {
            for (int v = -3; v < 5; v++)
            {
                if (coverings.Any(rectangle => rectangle.MinU <= u && rectangle.MaxU > u
                    && rectangle.MinV <= v && rectangle.MaxV > v))
                {
                    expected++;
                }
            }
        }
        for (int offset = 0; offset < coverings.Length; offset++)
        {
            var reordered = coverings.Skip(offset).Concat(coverings.Take(offset));
            Assert.Equal((Int128)expected, Cover(target, reordered));
            Assert.Equal((Int128)expected, Cover(target, reordered.Reverse()));
        }
    }

    [Fact]
    public void MaximumCoveringCountIsAcceptedIncludingDuplicates()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        Assert.Equal((Int128)100, Cover(target, Enumerable.Repeat(target, BoundarySurfaceCoverage.MaximumRectangles)));
    }

    [Fact]
    public void ExcessiveOffTargetEnumerationStopsAtTheFirstExcessAndIsDisposed()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        int yielded = 0;
        bool disposed = false;
        Assert.Throws<ArgumentException>(() => Cover(target, EndlessCoverings()));
        Assert.Equal(BoundarySurfaceCoverage.MaximumRectangles + 1, yielded);
        Assert.True(disposed);

        IEnumerable<BoundaryRectangle> EndlessCoverings()
        {
            try
            {
                while (true)
                {
                    yielded++;
                    yield return new(20, 20, 30, 30);
                }
            }
            finally
            {
                disposed = true;
            }
        }
    }

    [Fact]
    public void PreCanceledRequestDoesNotEnumerateCoverings()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        bool enumerated = false;
        Assert.Throws<OperationCanceledException>(() => BoundarySurfaceCoverage.CoveredArea(target, Coverings(), new(true)));
        Assert.False(enumerated);

        IEnumerable<BoundaryRectangle> Coverings()
        {
            enumerated = true;
            yield return target;
        }
    }

    [Fact]
    public void CancellationDuringEnumerationRejectsPartialCoverage()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => BoundarySurfaceCoverage.CoveredArea(target, Coverings(), cancellation.Token));

        IEnumerable<BoundaryRectangle> Coverings()
        {
            yield return target;
            cancellation.Cancel();
            yield return target;
        }
    }

    [Fact]
    public void CancellationAtEnumerationCompletionIsStillObserved()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => BoundarySurfaceCoverage.CoveredArea(target, Coverings(), cancellation.Token));

        IEnumerable<BoundaryRectangle> Coverings()
        {
            yield return target;
            cancellation.Cancel();
        }
    }

    [Fact]
    public void NullInputsReject()
    {
        var target = new BoundaryRectangle(0, 0, 10, 10);
        Assert.Throws<ArgumentNullException>(() => Cover(null!, []));
        Assert.Throws<ArgumentNullException>(() => Cover(target, null!));
        Assert.Throws<ArgumentException>(() => Cover(target, [null!]));
    }

    [Fact]
    public void UnrepresentableCoveredWidthsAndHeightsReject()
    {
        var tooWide = new BoundaryRectangle(Int128.MinValue, 0, Int128.MaxValue, 1);
        var tooTall = new BoundaryRectangle(0, Int128.MinValue, 1, Int128.MaxValue);
        Assert.Throws<OverflowException>(() => Cover(tooWide, [tooWide]));
        Assert.Throws<OverflowException>(() => Cover(tooTall, [tooTall]));
    }

    [Fact]
    public void AreaMultiplicationAndAccumulationNeverWrap()
    {
        var tooLarge = new BoundaryRectangle(0, 0, Int128.MaxValue, 2);
        Assert.Throws<OverflowException>(() => Cover(tooLarge, [tooLarge]));
        var twoStrips = new BoundaryRectangle(0, 0, 2, Int128.MaxValue);
        Assert.Throws<OverflowException>(() => Cover(twoStrips,
            [new(0, 0, 1, Int128.MaxValue), new(1, 0, 2, Int128.MaxValue)]));
    }

    private static Int128 Cover(BoundaryRectangle target, IEnumerable<BoundaryRectangle> coverings) =>
        BoundarySurfaceCoverage.CoveredArea(target, coverings, TestContext.Current.CancellationToken);
}

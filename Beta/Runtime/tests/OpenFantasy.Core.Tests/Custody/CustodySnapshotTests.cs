using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using static OpenFantasy.Core.Tests.Custody.CustodyFixture;

namespace OpenFantasy.Core.Tests.Custody;

public sealed class CustodySnapshotTests
{
    [Fact]
    public void SourceIntervalsArePositiveHalfOpenAndSupportUnsignedBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceInterval(Id(1), 2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceInterval(Id(1), 3, 2));
        Assert.Equal(ulong.MaxValue, new SourceInterval(Id(1), 0, ulong.MaxValue).Quantity);
    }

    [Fact]
    public void SnapshotRejectsDuplicateIdsUnknownCustodiansAndCapacityOverflow()
    {
        CustodySnapshot original = Create();
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            original.Storages.Concat(original.Storages), original.Lots));
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            original.Storages, original.Lots.Concat(original.Lots)));
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            [], original.Lots));
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            [new CustodianStorage(Id(3), 7, 0)], original.Lots));
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            original.Storages.Concat([new CustodianStorage(Id(5), 8, 0)]), original.Lots));
    }

    [Fact]
    public void OverlappingSourceClaimsAndInconsistentUnitsRejectWhileAdjacentIntervalsRemainValid()
    {
        CustodySnapshot original = Create();
        CountedSourceLot Neighbor(ulong start, CountedItemDefinition definition) => new(Id(7), Id(4), definition,
            new SourceInterval(Id(6), start, 20), 0);
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            original.Storages, original.Lots.Concat([Neighbor(17, Wood)])));
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            original.Storages, original.Lots.Concat([Neighbor(18, new CountedItemDefinition(Id(10), ContentHash.Compute("changed"u8)))])));
        var adjacent = new CustodySnapshot(Id(1), Id(2), 0, original.Storages,
            original.Lots.Concat([Neighbor(18, Wood)]));
        Assert.Equal(2, adjacent.Lots.Count);
    }

    [Fact]
    public void ImmutableCopiesAndEnumerationOrderPreserveCanonicalIdentity()
    {
        CustodySnapshot original = Create();
        var storages = original.Storages.Reverse().ToList();
        var lots = original.Lots.ToList();
        var snapshot = new CustodySnapshot(Id(1), Id(2), 7, storages, lots);
        storages.Clear();
        lots.Clear();
        Assert.Equal(original.ContentHash, snapshot.ContentHash);
        Assert.Throws<NotSupportedException>(() => ((IList<CountedSourceLot>)snapshot.Lots).Clear());
        byte[] bytes = CustodyEncodingV1.Encode(snapshot);
        bytes[0] = 0;
        Assert.NotEqual(bytes, CustodyEncodingV1.Encode(snapshot));
    }

    [Fact]
    public void MaximumCapacityDoesNotOverflowWhenSummingInputs()
    {
        CustodianStorage storage = new(Id(3), ulong.MaxValue, 0);
        CountedSourceLot large = new(Id(5), Id(3), Wood, new SourceInterval(Id(6), 0, ulong.MaxValue), 0);
        var snapshot = new CustodySnapshot(Id(1), Id(2), 0, [storage], [large]);
        Assert.Equal(ulong.MaxValue, snapshot.GetStoredQuantity(Id(3)));
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0, [storage],
            [large, new CountedSourceLot(Id(7), Id(3), Wood, new SourceInterval(Id(8), 0, 1), 0)]));
    }

    [Fact]
    public void BoundedSnapshotsRejectExcessiveEnumerations()
    {
        var storages = Enumerable.Range(0, CustodySnapshot.MaximumCustodians)
            .Select(index => new CustodianStorage(Id((ulong)index + 100), 0, 0)).ToArray();
        Assert.Equal(256, new CustodySnapshot(Id(1), Id(2), 0, storages, []).Storages.Count);
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0,
            storages.Concat([new CustodianStorage(Id(900), 0, 0)]), []));
        var lots = Enumerable.Range(0, CustodySnapshot.MaximumLots)
            .Select(index => new CountedSourceLot(Id((ulong)index + 100), Id(3), Wood,
                new SourceInterval(Id(6), (ulong)index, (ulong)index + 1), 0)).ToArray();
        CustodianStorage storage = new(Id(3), 10_000, 0);
        Assert.Equal(4096, new CustodySnapshot(Id(1), Id(2), 0, [storage], lots).Lots.Count);
        Assert.Throws<ArgumentException>(() => new CustodySnapshot(Id(1), Id(2), 0, [storage],
            lots.Concat([new CountedSourceLot(Id(9999), Id(3), Wood, new SourceInterval(Id(6), 4096, 4097), 0)])));
    }

    [Fact]
    public void UnknownProtectionsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(protection: (LotProtection)16));
    }
}

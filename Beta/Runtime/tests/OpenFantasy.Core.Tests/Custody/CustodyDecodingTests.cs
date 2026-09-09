using System.Text.Json;
using OpenFantasy.Core.Custody;
using static OpenFantasy.Core.Tests.Custody.CustodyFixture;

namespace OpenFantasy.Core.Tests.Custody;

public sealed class CustodyDecodingTests
{
    [Fact]
    public void FrozenSnapshotsDecodeWithoutChangingAnyCanonicalBytes()
    {
        using JsonDocument document = Fixture("custody-v1.json");
        foreach (string field in new[] { "originalHex", "candidateHex" })
        {
            byte[] bytes = Convert.FromHexString(document.RootElement.GetProperty(field).GetString()!);
            CustodySnapshot decoded = CustodyEncodingV1.Decode(bytes);
            Assert.Equal(bytes, CustodyEncodingV1.Encode(decoded));
        }
    }

    [Fact]
    public void RestoredTransferContinuesWithSameSourceClaimsAndNextIdentity()
    {
        CustodySnapshot before = Create();
        var planner = new CustodyTransferPlanner();
        CustodySnapshot first = planner.Plan(before, Command(before)).Candidate;
        CustodySnapshot restored = CustodyEncodingV1.Decode(CustodyEncodingV1.Encode(first));
        CustodyTransferPlan expected = planner.Plan(first, Command(first, quantity: 1, commandId: 30));
        CustodyTransferPlan actual = planner.Plan(restored, Command(restored, quantity: 1, commandId: 30));
        Assert.Equal(expected.MovedLotId, actual.MovedLotId);
        Assert.Equal(expected.Candidate.ContentHash, actual.Candidate.ContentHash);
    }

    [Fact]
    public void IndependentMalformedFramesRejectWithTypedDiagnostics()
    {
        using JsonDocument document = Fixture("custody-invalid-v1.json");
        foreach (JsonElement item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(item.GetProperty("hex").GetString()!);
            CustodyFormatException error = Assert.Throws<CustodyFormatException>(() => CustodyEncodingV1.Decode(bytes));
            Assert.True(error.Failure.ToString() == item.GetProperty("failure").GetString(),
                item.GetProperty("name").GetString() + ": " + error.Failure);
        }
    }

    [Fact]
    public void EveryIncompletePrefixAndOverLimitInputRejects()
    {
        byte[] complete = CustodyEncodingV1.Encode(Create());
        for (int length = 0; length < complete.Length; length++)
        {
            byte[] incomplete = complete[..length];
            Assert.Throws<CustodyFormatException>(() => CustodyEncodingV1.Decode(incomplete));
        }
        Assert.Equal(CustodyFormatFailure.LimitExceeded,
            Assert.Throws<CustodyFormatException>(() => CustodyEncodingV1.Decode(
                new byte[CustodyEncodingV1.MaximumSnapshotByteLength + 1])).Failure);
    }

    [Fact]
    public void ExactMaximumSnapshotIncludingZeroIdentityReservationsRoundTrips()
    {
        CustodianStorage[] storages = Enumerable.Range(0, 256)
            .Select(index => new CustodianStorage(Id((ulong)index + 100), 4096, ulong.MaxValue)).ToArray();
        CountedSourceLot[] lots = Enumerable.Range(0, 4096).Select(index =>
            new CountedSourceLot(Id((ulong)index + 1000), storages[index % 256].CustodianId, Wood,
                new SourceInterval(Id(6), (ulong)index, (ulong)index + 1), ulong.MaxValue,
                LotProtection.Equipped | LotProtection.Favorite | LotProtection.Heirloom | LotProtection.UserReserved,
                default(EntityId))).ToArray();
        var snapshot = new CustodySnapshot(Id(1), Id(2), ulong.MaxValue, storages, lots);
        byte[] bytes = CustodyEncodingV1.Encode(snapshot);
        Assert.Equal(CustodyEncodingV1.MaximumSnapshotByteLength, bytes.Length);
        CustodySnapshot restored = CustodyEncodingV1.Decode(bytes);
        Assert.Equal(snapshot.ContentHash, restored.ContentHash);
        Assert.All(restored.Lots, lot => Assert.True(lot.ReservedBy.HasValue));
        Assert.All(restored.Lots, lot => Assert.Equal(default(EntityId), lot.ReservedBy));
        Assert.Null(CustodyEncodingV1.Decode(CustodyEncodingV1.Encode(Create())).Lots[0].ReservedBy);
    }

    [Fact]
    public void MutatedAndRandomFramesEitherRejectOrReencodeExactly()
    {
        byte[] golden = CustodyEncodingV1.Encode(Create());
        for (int bit = 0; bit < golden.Length * 8; bit++)
        {
            byte[] bytes = (byte[])golden.Clone();
            bytes[bit / 8] ^= (byte)(1 << (bit % 8));
            RequireCanonicalOrTypedFailure(bytes);
        }
        var random = new Random(2909);
        for (int count = 0; count < 1000; count++)
        {
            byte[] bytes = new byte[random.Next(1024)];
            random.NextBytes(bytes);
            RequireCanonicalOrTypedFailure(bytes);
        }
    }

    private static void RequireCanonicalOrTypedFailure(byte[] bytes)
    {
        try
        {
            Assert.Equal(bytes, CustodyEncodingV1.Encode(CustodyEncodingV1.Decode(bytes)));
        }
        catch (CustodyFormatException)
        {
            // The only accepted failure family; unexpected runtime exceptions fail this probe.
        }
    }

    private static JsonDocument Fixture(string name) => JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
}

using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Same_earned_episode_compares_publication_and_replay_without_changing_outcomes(bool separatedFirst)
    {
        using var firstFiles = new StorageFiles();
        using var secondFiles = new StorageFiles();
        var first = CompareEpisode(firstFiles.Path, separatedFirst);
        var second = CompareEpisode(secondFiles.Path, !separatedFirst);
        Assert.Equal(first.StartImage, second.StartImage);
        Assert.Equal(first.FinalWorld, second.FinalWorld);
        Assert.Equal(first.Receipts.Count, second.Receipts.Count);
        for (int i = 0; i < first.Receipts.Count; i++) Assert.Equal(first.Receipts[i], second.Receipts[i]);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            SeparatedFirst = separatedFirst,
            First = first.Measurement,
            Second = second.Measurement,
            Scope = "Same selected 200 work commits with an ordinary intervening move after40; excludes setup, selection, assertions, intervention and final encoding from publication timing. Reopen uses full original history. Two execution orders are diagnostics, not statistical or cold-cache qualification."
        }));
    }

    private static ComparedEpisode CompareEpisode(string path, bool separated)
    {
        var ready = Start(path, separated);
        var locality = ready.Locality;
        var startImage = ContentHash.Compute(ConstructionLocalityEncoding.Encode(locality));
        List<byte[]> receipts = [];
        byte[] finalWorld;
        long publicationBytes = 0, publicationTicks = 0;
        using (var authority = ready.Authority)
        {
            WorldSnapshot? world = separated ? null : authority.ReadStateForHost();
            for (int i = 0; i < 200; i++)
            {
                var batch = world is null ? Batch(locality, ready.Fixture)
                    : Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision,
                        world.AdvancingSteps, ready.Fixture);
                WorldActionReceipt receipt;
                long bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
                if (separated)
                {
                    var next = authority.CommitWithoutTerrain(batch);
                    locality = next.Candidate;
                    receipt = next.Receipt;
                }
                else
                {
                    var next = authority.CommitConstructionLocality(batch,
                        ready.Locality.ProjectId, 512);
                    world = next.Candidate;
                    receipt = next.Receipts[0];
                }
                publicationTicks += Stopwatch.GetTimestamp() - ticks;
                publicationBytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                receipts.Add(WorldReceiptCodecV1.Encode(receipt));
                if (i == 39)
                {
                    var returned = authority.ReadStateForHost();
                    Assert.Equal(40UL, returned.Construction!.Projects.Single().Progress.CompletedUnits);
                    var player = returned.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
                    var intervened = authority.Commit(ready.Fixture.Batch(returned, [],
                        [new(ConstructionUpgradeFixture.Delivery(returned, player), player, -32, 0)])).Candidate;
                    if (separated)
                        locality = authority.EvictConstructionTerrain(intervened.Construction!.Projects.Single().ProjectId, 512);
                    else world = intervened;
                }
            }
            var final = authority.ReadStateForHost();
            AssertCompleted(final);
            finalWorld = WorldSnapshotCodec.Encode(final);
        }
        long replayBytes = GC.GetAllocatedBytesForCurrentThread(), replayTicks = Stopwatch.GetTimestamp();
        using var reopened = SqliteWorldAuthority.OpenExisting(path);
        replayTicks = Stopwatch.GetTimestamp() - replayTicks;
        replayBytes = GC.GetAllocatedBytesForCurrentThread() - replayBytes;
        var restored = reopened.ReadStateForHost();
        Assert.Equal(finalWorld, WorldSnapshotCodec.Encode(restored));
        long sqliteBytes = new[] { path, path + "-wal", path + "-shm" }
            .Where(File.Exists).Sum(file => new FileInfo(file).Length);
        return new(startImage, finalWorld, receipts, new(separated, 200, publicationBytes,
            publicationTicks * 1000.0 / Stopwatch.Frequency, replayBytes,
            replayTicks * 1000.0 / Stopwatch.Frequency, finalWorld.Length,
            ConstructionLocalityEncoding.Encode(restored).Length, sqliteBytes));
    }

    private sealed record ComparedEpisode(ContentHash StartImage, byte[] FinalWorld,
        IReadOnlyList<byte[]> Receipts, EpisodeMeasurement Measurement);

    private sealed record EpisodeMeasurement(bool SeparatedTerrain, int SelectedWorkCommits,
        long PublicationAllocatedBytes, double PublicationMilliseconds, long ReplayAllocatedBytes,
        double ReplayMilliseconds, int FullWorldBytes, int LocalityImageBytes, long SqliteFileBytesIncludingWal);
}

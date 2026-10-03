using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Tests.Networking.ObservationViews;

public sealed class ObservationCodecCostTests(ITestOutputHelper output)
{
    private const int PlayerCount = 4;
    private const int WarmupRounds = 2;
    private const int MeasuredRounds = 8;

    [Fact]
    public void Four_player_production_codec_preserves_scope_and_bytes_and_reports_warmed_component_cost()
    {
        output.WriteLine($"observation:runtime; version:{Environment.Version}; framework:{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        WorldSnapshot world = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText,
            new[] { "Amber", "Birch", "Cedar", "Dawn" });
        EntityId[] players = world.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).Order().ToArray();
        Assert.Equal(PlayerCount, players.Length);
        LocalWorldView[] views = players.Select(actor =>
            new LocalWorldView(world, actor, regions: new LocalRegionObserver(actor))).ToArray();
        byte[][] encoded = views.Select(PlayerViewCodec.Encode).ToArray();
        byte[][] expanded = encoded.Select(Expand).ToArray();
        string[] diagnostics = views.Select(view => JsonSerializer.Serialize(view)).ToArray();

        for (int index = 0; index < PlayerCount; index++)
        {
            Assert.True(views[index].IsRegional);
            Assert.NotNull(views[index].Regional);
            AssertOwnScope(world, players[index], views[index]);
            AssertRoundtrip(index);
            PlayerViewCodec.Preflight(expanded[index]);
            output.WriteLine($"observation:workload; player:{index}; wire-bytes:{encoded[index].Length}; "
                + $"expanded-bytes:{expanded[index].Length}; compressed:{ObservationCompression.IsCompressed(encoded[index])}");
        }

        // Projection, decompression for the isolated preflight workload, assertions and output are outside
        // the measured loops. Encode and Decode deliberately retain all production validation they perform.
        Measure("encode", index => GC.KeepAlive(PlayerViewCodec.Encode(views[index])));
        Measure("decode", index => GC.KeepAlive(PlayerViewCodec.Decode(encoded[index])));
        Measure("preflight-expanded-json", index => PlayerViewCodec.Preflight(expanded[index]));

        for (int index = 0; index < PlayerCount; index++)
        {
            Assert.Equal(encoded[index], PlayerViewCodec.Encode(views[index]));
            Assert.Equal(diagnostics[index], JsonSerializer.Serialize(views[index]));
            AssertRoundtrip(index);
        }

        void AssertRoundtrip(int index)
        {
            LocalWorldView decoded = PlayerViewCodec.Decode(encoded[index]);
            AssertOwnScope(world, players[index], decoded);
            Assert.Equal(diagnostics[index], JsonSerializer.Serialize(decoded));
            byte[] reencoded = PlayerViewCodec.Encode(decoded);
            Assert.Equal(encoded[index], reencoded);
            Assert.Equal(expanded[index], Expand(reencoded));
        }
    }

    [Theory]
    [InlineData("{\"Value\":1,\"Value\":2}")]
    [InlineData("{\"Value\":1,\"\\u0056alue\":2}")]
    [InlineData("{\"Items\":[{\"Value\":1,\"\\u0056alue\":2}]}")]
    [InlineData("{\"a/b\":1,\"a\\/b\":2}")]
    public void Preflight_refuses_duplicate_decoded_property_names_within_the_same_object(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Assert.Throws<FormatException>(() => PlayerViewCodec.Preflight(bytes));
    }

    [Fact]
    public void Preflight_keeps_sibling_object_names_independent_and_accepts_unique_escaped_names()
    {
        byte[] bytes = "{\"Items\":[{\"Value\":1},{\"\\u0056alue\":2}],\"Other\":{\"Value\":3,\"\\u004eame\":4}}"u8.ToArray();
        PlayerViewCodec.Preflight(bytes);
    }

    private void Measure(string component, Action<int> operation)
    {
        for (int round = 0; round < WarmupRounds; round++)
            for (int player = 0; player < PlayerCount; player++) operation(player);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int round = 0; round < MeasuredRounds; round++)
            for (int player = 0; player < PlayerCount; player++) operation(player);
        long finished = Stopwatch.GetTimestamp();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        output.WriteLine($"observation:{component}; warmup-calls:{WarmupRounds * PlayerCount}; "
            + $"calls:{MeasuredRounds * PlayerCount}; milliseconds:{Stopwatch.GetElapsedTime(started, finished).TotalMilliseconds:F3}; "
            + $"allocated-bytes:{allocatedBytes}");
    }

    private static byte[] Expand(byte[] encoded) => ObservationCompression.IsCompressed(encoded)
        ? ObservationCompression.Decode(encoded) : encoded;

    private static void AssertOwnScope(WorldSnapshot world, EntityId player, LocalWorldView view)
    {
        EntityId storage = world.GetResident(player).InventoryCustodianId;
        EntityId[] expectedLots = world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == storage)
            .Select(lot => lot.LotId)
            .Concat(world.Production!.Lots.Where(lot => lot.CustodianId == storage).Select(lot => lot.LotId))
            .Order().ToArray();
        Assert.NotEmpty(expectedLots);
        Assert.Equal(world.WorldId, view.WorldId);
        Assert.Equal(world.BranchId, view.BranchId);
        Assert.Equal(player, view.PlayerActorId);
        Assert.NotNull(view.Regional);
        Assert.Equal(player, view.Regional.PlayerActorId);
        Assert.Equal(storage, view.PlayerInventory.CustodianId);
        Assert.Equal(expectedLots, view.PlayerInventory.Lots.Select(lot => lot.LotId).ToArray());
        foreach (WorldResident other in world.Residents.Where(actor =>
            actor.ControllerKind == ResidentControllerKind.Player && actor.ActorId != player))
        {
            Assert.DoesNotContain(view.PlayerInventory.Lots, lot => world.Inventory.Custody.Lots.Any(source =>
                source.CustodianId == other.InventoryCustodianId && source.LotId == lot.LotId));
            Assert.Contains(view.Actors, actor => actor.ActorId == other.ActorId);
        }
        Assert.NotNull(view.Progression);
        Assert.NotNull(world.Progression);
        Assert.Equal(world.Progression.Currency.GetActorAccount(player).Balance, view.Progression.CurrencyBalance);
        Assert.Equal(world.Progression.GetActor(player).CurrentHp, view.Progression.Hp);
        Assert.Equal(world.Progression.GetActor(player).CurrentMp, view.Progression.Mp);
    }
}

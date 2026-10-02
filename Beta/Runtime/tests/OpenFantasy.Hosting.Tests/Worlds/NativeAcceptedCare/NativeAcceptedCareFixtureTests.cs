using System.Text.Json;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.NativeAcceptedCare;

/// <summary>Exports an unplayed production composition. Preparation commits no movement, combat, care or damage.</summary>
public sealed class NativeAcceptedCareFixtureTests
{
    internal const string Seed = "native-accepted-care";
    internal const string PlayerName = "Traveler";

    [Fact]
    public void Export_unplayed_full_health_world_with_original_helper_stock()
    {
        string directory = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ACCEPTED_CARE_FIXTURES")
            ?? throw new InvalidOperationException("An explicit fresh fixture destination is required.");
        Assert.False(Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any());
        Directory.CreateDirectory(directory);
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(Seed, PlayerName);
        var player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var wolf = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
        var helper = genesis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        Assert.Equal((EncounterLifecyclePhase.Exploration, 0UL), (genesis.EncounterLifecycle.Phase, genesis.EncounterLifecycle.EncounterCount));
        Assert.Equal(genesis.Progression!.GetActor(player).Stats.MaxHp, genesis.Progression.GetActor(player).CurrentHp);
        string save = Path.Combine(directory, "save.sqlite");
        using (var authority = SqliteWorldAuthority.CreateNew(save, genesis))
            Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), bytes);
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), bytes);
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-encounter-outcome-fixture-v1", Route = "care-accepted", SeedText = Seed, PlayerName,
            PlayerActorId = player.ToString(), OpponentActorId = wolf.ToString(), WrenActorId = helper.ToString(),
            GenesisHash = genesis.ContentHash.Value, BeforeHash = genesis.ContentHash.Value,
            Boundary = "Unplayed uncalibrated production genesis; all approach, entry and combat belong to native input."
        }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-accepted-care-fixtures-v1", GenesisHash = genesis.ContentHash.Value
        }));
    }
}

using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Small fresh roots exercise save selection; the retained long ledger belongs to the fixture tests.</summary>
public sealed class CoordinatedNativeSelectionTests
{
    private const string Seed = "coordinated-native-selection";
    private const string PlayerName = "Traveler";

    [Fact]
    public async Task The_expected_genesis_opens_only_the_existing_save_it_began()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        byte[] expected = WorldSnapshotCodec.Encode(genesis);
        WorldSnapshot lived = CreateLived(files.Path, genesis);
        byte[] closed = File.ReadAllBytes(files.Path);

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path, expected))
        {
            Assert.Equal(lived.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(expected, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(0).State));
        }
        string genesisPath = WriteGenesis(files, expected);
        await using (FarmClientSession client = await FarmClientSession.OpenCoordinatedFixtureAsync(files.Path, genesisPath))
        {
            Assert.Equal(lived.WorldId, client.World.ReadView().WorldId);
            Assert.Equal(lived.Calendar, client.World.ReadView().Calendar);
            Assert.NotNull(client.World.ReadView().Restorative);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public async Task A_missing_save_is_refused_without_creating_one()
    {
        using var files = new StorageFiles();
        byte[] expected = WorldSnapshotCodec.Encode(Coordinated(Seed));
        string genesisPath = WriteGenesis(files, expected);

        Assert.Throws<FileNotFoundException>(() => SqliteWorldAuthority.OpenExisting(files.Path, expected));
        await Assert.ThrowsAsync<FileNotFoundException>(() => FarmClientSession.OpenCoordinatedFixtureAsync(files.Path, genesisPath));
        Assert.False(File.Exists(files.Path));
    }

    [Theory]
    [InlineData("another-coordinated-seed", PlayerName)]
    [InlineData(Seed, "Another traveler")]
    public async Task Another_exact_genesis_is_refused_and_releases_the_save(string seed, string playerName)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        WorldSnapshot lived = CreateLived(files.Path, genesis);
        byte[] closed = File.ReadAllBytes(files.Path);
        byte[] other = WorldSnapshotCodec.Encode(Coordinated(seed, playerName));
        Assert.NotEqual(WorldSnapshotCodec.Encode(genesis), other);

        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path, other));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            FarmClientSession.OpenCoordinatedFixtureAsync(files.Path, WriteGenesis(files, other)));

        // A refused selection holds no lease: the exact save still opens and nothing was written.
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(lived.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public async Task Matching_scope_and_admission_do_not_replace_exact_original_genesis()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        WorldSnapshot lived = CreateLived(files.Path, genesis);
        WorldResident player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        var renamed = new WorldResident(player.ActorId, player.InventoryCustodianId, "Another original name",
            player.ControllerKind, player.HomeFeet, player.WanderRadiusCells, player.Comprehension,
            player.ExperiencedTime, player.OpeningUtterance);
        var alternative = new WorldSnapshot(genesis.Inventory, genesis.Space,
            genesis.Residents.Select(value => value.ActorId == player.ActorId ? renamed : value),
            genesis.AdmissionVersion, genesis.Calendar, 0, 0, genesis.Dialogues, genesis.Gathering, genesis.Production,
            genesis.Progression, genesis.Quests, genesis.Construction, reservoirs: genesis.Reservoirs,
            reservoirSeals: genesis.ReservoirSeals, liquidCells: genesis.LiquidCells, excavation: genesis.Excavation,
            pacing: genesis.Pacing, encounterLifecycle: genesis.EncounterLifecycle, care: genesis.Care,
            resourceHistory: genesis.ResourceHistory, coordination: genesis.Coordination);
        WorldGenesisAdmission.Validate(alternative);
        FarmSaveOpener.ValidateSelection(lived, alternative);
        Assert.Equal(genesis.WorldId, alternative.WorldId);
        Assert.Equal(genesis.BranchId, alternative.BranchId);
        Assert.Equal(genesis.AdmissionVersion, alternative.AdmissionVersion);
        Assert.NotEqual(genesis.ContentHash, alternative.ContentHash);
        byte[] expected = WorldSnapshotCodec.Encode(alternative);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path, expected));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            FarmClientSession.OpenCoordinatedFixtureAsync(files.Path, WriteGenesis(files, expected)));
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(lived.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task A_lived_expected_root_is_refused()
    {
        using var files = new StorageFiles();
        WorldSnapshot lived = CreateLived(files.Path, Coordinated(Seed));
        byte[] current = WorldSnapshotCodec.Encode(lived);
        Assert.NotEqual(0UL, lived.Revision);

        // The current root is a valid complete world but never an admissible original genesis.
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.OpenExisting(files.Path, current));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            FarmClientSession.OpenCoordinatedFixtureAsync(files.Path, WriteGenesis(files, current)));
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(lived.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task The_client_fixture_route_requires_the_coordinated_profile()
    {
        using var ordinary = new StorageFiles();
        using var coordinated = new StorageFiles();
        WorldSnapshot ordinaryGenesis = RestorativeJourneyAdmission.CreateEmbedded(Seed, PlayerName);
        byte[] ordinaryBytes = WorldSnapshotCodec.Encode(ordinaryGenesis);
        WorldSnapshot ordinaryLived = CreateLived(ordinary.Path, ordinaryGenesis);
        CreateLived(coordinated.Path, Coordinated(Seed));
        Assert.Equal((ushort)26, ordinaryLived.ProfileVersion);

        // Hosting selection is profile-neutral; the explicit client fixture route is not.
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(ordinary.Path, ordinaryBytes))
        {
            Assert.Equal(ordinaryLived.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            FarmClientSession.OpenCoordinatedFixtureAsync(ordinary.Path, WriteGenesis(ordinary, ordinaryBytes)));
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(coordinated.Path, ordinaryBytes));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            FarmClientSession.OpenCoordinatedFixtureAsync(coordinated.Path, WriteGenesis(coordinated, ordinaryBytes)));
    }

    [Fact]
    public async Task Normal_ordinary_care_opening_is_unchanged_and_never_selects_a_coordinated_save()
    {
        using var fresh = new StorageFiles();
        using var coordinated = new StorageFiles();
        WorldSnapshot expected = RestorativeJourneyAdmission.CreateEmbedded(Seed, PlayerName);
        await using (FarmClientSession created = await FarmClientSession.OpenRestorativeAsync(fresh.Path, Seed, PlayerName))
        {
            Assert.Equal(expected.Calendar, created.World.ReadView().Calendar);
        }
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(fresh.Path))
        {
            WorldSnapshot state = authority.ReadStateForHost();
            Assert.Equal((ushort)26, state.ProfileVersion);
            Assert.Equal(expected.ContentHash, state.ContentHash);
        }
        await using (FarmClientSession reopened = await FarmClientSession.OpenRestorativeAsync(fresh.Path, Seed, PlayerName))
        {
            Assert.Equal(expected.Calendar, reopened.World.ReadView().Calendar);
        }

        CreateLived(coordinated.Path, Coordinated(Seed));
        byte[] closed = File.ReadAllBytes(coordinated.Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRestorativeAsync(coordinated.Path, Seed, PlayerName));
        Assert.Equal(closed, File.ReadAllBytes(coordinated.Path));
    }

    private static WorldSnapshot Coordinated(string seed, string playerName = PlayerName) =>
        WorldCoordinatedAdmission.CreateFresh(RestorativeJourneyAdmission.CreateEmbedded(seed, playerName));

    /// <summary>Two real empty advancing steps make the save a lived world without any command.</summary>
    private static WorldSnapshot CreateLived(string path, WorldSnapshot genesis)
    {
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(path, genesis);
        WorldSnapshot state = authority.ReadStateForHost();
        for (ulong step = 1; step <= 2; step++)
        {
            state = authority.Commit(new WorldStepBatch(new EntityId(0xc027_5e1e_0000_0000, step), state.WorldId, state.BranchId,
                state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion)).Candidate;
        }
        return state;
    }

    private static string WriteGenesis(StorageFiles files, byte[] genesis)
    {
        string path = Path.Combine(Path.GetDirectoryName(files.Path)!, "genesis-" + Guid.NewGuid().ToString("N") + ".ofc");
        File.WriteAllBytes(path, genesis);
        return path;
    }
}

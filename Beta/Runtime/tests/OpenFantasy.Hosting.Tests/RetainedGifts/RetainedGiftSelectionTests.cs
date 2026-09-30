using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.ProposalMigration;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.RetainedGifts;

/// <summary>
/// Small real worlds exercise the explicit retained-gift selection contract: an original coordinated
/// genesis plus an authenticated transitioned head. The long original ledger belongs to the fixture tests.
/// </summary>
public sealed class RetainedGiftSelectionTests
{
    private const string Seed = "retained-gift-selection";
    private const string PlayerName = "Traveler";

    [Fact]
    public async Task The_original_genesis_opens_only_its_explicitly_transitioned_save_paused_with_the_gift_policy()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        WorldSnapshot transitioned = Transition(source.Path, destination.Path, genesis);
        byte[] sourceBytes = File.ReadAllBytes(source.Path);
        byte[] closed = File.ReadAllBytes(destination.Path);

        await using (FarmClientSession client = await FarmClientSession.OpenRetainedGiftsAsync(destination.Path,
            WriteGenesis(destination, WorldSnapshotCodec.Encode(genesis)), FarmClientSession.RetainedGiftTargetProfileVersion))
        {
            LocalWorldView view = client.World.ReadView();
            Assert.Equal(LocalWorldRunState.Paused, client.World.ReadRunState());
            Assert.Equal(transitioned.WorldId, view.WorldId);
            Assert.Equal(transitioned.Calendar, view.Calendar);
            Assert.NotNull(view.MaterialProposals);
            Assert.NotNull(view.Restorative);
            EntityId player = transitioned.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            Assert.Equal(transitioned.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == player).HelperActorId,
                client.WrenActorId);
            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
            Assert.Equal(transitioned.Calendar, client.World.ReadView().Calendar);
            Assert.Equal(LocalWorldRunState.Paused, client.World.ReadRunState());
        }
        Assert.Equal(closed, File.ReadAllBytes(destination.Path));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source.Path));
    }

    [Fact]
    public async Task A_missing_save_or_another_target_is_refused_without_creating_anything()
    {
        using var files = new StorageFiles();
        string genesisPath = WriteGenesis(files, WorldSnapshotCodec.Encode(Coordinated(Seed)));
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            FarmClientSession.OpenRetainedGiftsAsync(files.Path, genesisPath, FarmClientSession.RetainedGiftTargetProfileVersion));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FarmClientSession.OpenRetainedGiftsAsync(files.Path, genesisPath, 28));
        Assert.False(File.Exists(files.Path));
    }

    [Theory]
    [InlineData("another-retained-seed", PlayerName)]
    [InlineData(Seed, "Another traveler")]
    public async Task A_foreign_original_genesis_is_refused_and_releases_the_save(string seed, string playerName)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot transitioned = Transition(source.Path, destination.Path, Coordinated(Seed));
        byte[] closed = File.ReadAllBytes(destination.Path);
        string foreign = WriteGenesis(destination, WorldSnapshotCodec.Encode(Coordinated(seed, playerName)));

        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRetainedGiftsAsync(destination.Path, foreign,
            FarmClientSession.RetainedGiftTargetProfileVersion));
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(destination.Path))
        {
            Assert.Equal(transitioned.ContentHash, reopened.ReadStateForHost().ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(destination.Path));
    }

    [Fact]
    public async Task Malformed_or_lived_expected_genesis_bytes_are_refused_before_selection()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        WorldSnapshot transitioned = Transition(source.Path, destination.Path, genesis);
        byte[] closed = File.ReadAllBytes(destination.Path);
        byte[] exact = WorldSnapshotCodec.Encode(genesis);

        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenRetainedGiftsAsync(destination.Path,
            WriteGenesis(destination, exact[..^1]), FarmClientSession.RetainedGiftTargetProfileVersion));
        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenRetainedGiftsAsync(destination.Path,
            WriteGenesis(destination, [.. exact, 0]), FarmClientSession.RetainedGiftTargetProfileVersion));
        // Both the lived source head and the transitioned head are complete worlds, never an original genesis.
        using (SqliteWorldAuthority lived = SqliteWorldAuthority.OpenExisting(source.Path))
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRetainedGiftsAsync(destination.Path,
                WriteGenesis(destination, WorldSnapshotCodec.Encode(lived.ReadStateForHost())),
                FarmClientSession.RetainedGiftTargetProfileVersion));
        }
        // A post-transition root cannot even decode without its archived anchor context.
        await Assert.ThrowsAsync<FormatException>(() => FarmClientSession.OpenRetainedGiftsAsync(destination.Path,
            WriteGenesis(destination, WorldSnapshotCodec.Encode(transitioned)), FarmClientSession.RetainedGiftTargetProfileVersion));
        Assert.Equal(closed, File.ReadAllBytes(destination.Path));
    }

    [Fact]
    public async Task An_untransitioned_coordinated_save_is_never_migrated_by_opening()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        WorldSnapshot lived = CreateLived(files.Path, genesis);
        byte[] closed = File.ReadAllBytes(files.Path);

        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRetainedGiftsAsync(files.Path,
            WriteGenesis(files, WorldSnapshotCodec.Encode(genesis)), FarmClientSession.RetainedGiftTargetProfileVersion));
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal((ushort)27, authority.ReadStateForHost().ProfileVersion);
            Assert.Equal(lived.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public async Task Fresh_saved_gift_and_ordinary_interpretations_are_not_retained_worlds()
    {
        using var fresh = new StorageFiles();
        using var ordinary = new StorageFiles();
        WorldSnapshot freshGenesis = WorldMaterialProposalAdmission.CreateFresh(RestorativeJourneyAdmission.CreateEmbedded(Seed, PlayerName));
        WorldSnapshot ordinaryGenesis = RestorativeJourneyAdmission.CreateEmbedded(Seed, PlayerName);
        CreateLived(fresh.Path, freshGenesis);
        CreateLived(ordinary.Path, ordinaryGenesis);
        byte[] freshBytes = File.ReadAllBytes(fresh.Path);
        byte[] ordinaryBytes = File.ReadAllBytes(ordinary.Path);

        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRetainedGiftsAsync(fresh.Path,
            WriteGenesis(fresh, WorldSnapshotCodec.Encode(freshGenesis)), FarmClientSession.RetainedGiftTargetProfileVersion));
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenRetainedGiftsAsync(ordinary.Path,
            WriteGenesis(ordinary, WorldSnapshotCodec.Encode(ordinaryGenesis)), FarmClientSession.RetainedGiftTargetProfileVersion));
        Assert.Equal(freshBytes, File.ReadAllBytes(fresh.Path));
        Assert.Equal(ordinaryBytes, File.ReadAllBytes(ordinary.Path));
    }

    [Fact]
    public async Task Existing_fresh_gift_and_coordinated_selectors_still_refuse_a_transitioned_save()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        Transition(source.Path, destination.Path, genesis);
        byte[] closed = File.ReadAllBytes(destination.Path);
        string original = WriteGenesis(destination, WorldSnapshotCodec.Encode(genesis));

        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenCoordinatedFixtureAsync(destination.Path, original));
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenGiftFixtureAsync(destination.Path, original));
        Assert.Throws<InvalidDataException>(() => FarmSaveOpener.ValidateSelection(
            SqliteWorldAuthority.OpenExisting(destination.Path).ReadAndDispose(), genesis));
        Assert.Equal(closed, File.ReadAllBytes(destination.Path));
    }

    [Fact]
    public async Task Damaged_later_history_refuses_selection_and_earlier_reads()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot genesis = Coordinated(Seed);
        WorldSnapshot transitioned = Transition(source.Path, destination.Path, genesis);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(destination.Path))
        {
            authority.Commit(new WorldStepBatch(RetainedGiftPreparation.NextId("selection-later-step", 0), transitioned.WorldId,
                transitioned.BranchId, transitioned.Revision, transitioned.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [],
                transitioned.ProfileVersion));
        }
        // Damage only the newest stored commit; an earlier state must not become readable through it.
        destination.Execute("UPDATE world_commits SET cause_id = x'00000000000000000000000000000001' "
            + "WHERE sequence = (SELECT max(sequence) FROM world_commits)");
        byte[] damaged = File.ReadAllBytes(destination.Path);

        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenRetainedGiftsAsync(destination.Path,
            WriteGenesis(destination, WorldSnapshotCodec.Encode(genesis)), FarmClientSession.RetainedGiftTargetProfileVersion));
        Assert.ThrowsAny<Exception>(() => RecordedWorldReader.ReadForHost(destination.Path, transitioned.Revision));
        Assert.Equal(damaged, File.ReadAllBytes(destination.Path));
    }

    internal static WorldSnapshot Coordinated(string seed, string playerName = PlayerName) =>
        WorldCoordinatedAdmission.CreateFresh(RestorativeJourneyAdmission.CreateEmbedded(seed, playerName));

    /// <summary>Two real empty advancing steps make the save a lived world without any command.</summary>
    internal static WorldSnapshot CreateLived(string path, WorldSnapshot genesis)
    {
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(path, genesis);
        WorldSnapshot state = authority.ReadStateForHost();
        for (ulong step = 0; step < 2; step++)
        {
            state = authority.Commit(new WorldStepBatch(RetainedGiftPreparation.NextId("selection-lived-step", step), state.WorldId,
                state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion)).Candidate;
        }
        return state;
    }

    /// <summary>The accepted explicit copy-and-publish transition; opening never performs it.</summary>
    internal static WorldSnapshot Transition(string sourcePath, string destinationPath, WorldSnapshot genesis)
    {
        WorldSnapshot lived = CreateLived(sourcePath, genesis);
        var request = WorldProposalTransition.ForHead(lived, RetainedTransitionFixture.Cause("retained-gift-selection.transition"));
        using SqliteWorldAuthority upgraded = SqliteWorldAuthority.UpgradeCoordinatedToProposals(sourcePath, destinationPath, request);
        return upgraded.ReadStateForHost();
    }

    internal static string WriteGenesis(StorageFiles files, byte[] genesis)
    {
        string path = Path.Combine(Path.GetDirectoryName(files.Path)!, "genesis-" + Guid.NewGuid().ToString("N") + ".ofc");
        File.WriteAllBytes(path, genesis);
        return path;
    }
}

internal static class RetainedGiftAuthorityReading
{
    /// <summary>Reads a recovered head and releases the save before any assertion about it.</summary>
    internal static WorldSnapshot ReadAndDispose(this SqliteWorldAuthority authority)
    {
        using (authority) return authority.ReadStateForHost();
    }
}

using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class LegacyWorldCompatibilityTests
{
    private const string FixtureHash = "ad7e10a2d60f80411d9ff2ca08a25ee5ebda046f347204c95dccfd5d526baa80";
    private const string HeadHash = "3cc9eb1f6303b0cfd594eddb8c9e0c7c61f6c54400448343fae988cd0da99215";

    [Fact]
    public void RetainedNativeSaveReplaysExactLegacyHistoryAndContinuesWithoutMigration()
    {
        using var files = new StorageFiles();
        CopyLegacyFixture(files.Path);
        LegacyTrace original = ReadTrace(files.Path);
        Assert.Equal(3, original.Commands.Count);
        WorldSnapshot continued;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldSnapshot state = authority.InspectForHost().State;
            Assert.Equal((ushort)1, state.ProfileVersion);
            Assert.Null(state.Gathering);
            Assert.Equal(32UL, state.Revision);
            Assert.Equal(30UL, state.AdvancingSteps);
            Assert.Equal(HeadHash, state.ContentHash.Value);
            Assert.Equal(original.Head, WorldSnapshotCodec.Encode(state));
            Assert.Equal(original.Head, WorldSnapshotCodecV1.Encode(state));
            AssertOriginalCommands(authority, original);
            foreach (EntityId identity in original.Identities)
            {
                Assert.True(authority.IsIdentityOccupiedForHost(identity));
            }
            WorldResident player = state.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);
            Assert.Equal(2UL, state.Inventory.Custody.GetStoredQuantity(player.InventoryCustodianId));
            var step = new WorldStepBatch(new EntityId(0x8172635445362718, 6000), state.WorldId, state.BranchId,
                state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], []);
            Assert.Equal(WorldStepCodecV1.Encode(step), WorldStepCodec.Encode(step));
            continued = authority.Commit(step).Candidate;
            Assert.Equal(33UL, continued.Revision);
            Assert.Equal(31UL, continued.AdvancingSteps);
            Assert.Null(continued.Gathering);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodecV1.Encode(continued), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        AssertOriginalCommands(reopened, original);
        foreach (EntityId identity in original.Identities)
        {
            Assert.True(reopened.IsIdentityOccupiedForHost(identity));
        }
    }

    [Fact]
    public void NewBatchProfileCannotSilentlyUpgradeAnExistingWorld()
    {
        using var files = new StorageFiles();
        CopyLegacyFixture(files.Path);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldSnapshot state = authority.InspectForHost().State;
            var incompatible = new WorldStepBatch(new EntityId(0x8172635445362718, 6001), state.WorldId,
                state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], 2);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(incompatible));
            Assert.Equal(HeadHash, authority.InspectForHost().State.ContentHash.Value);
            Assert.False(authority.IsIdentityOccupiedForHost(incompatible.CauseId));
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(HeadHash, reopened.InspectForHost().State.ContentHash.Value);
    }

    private static void AssertOriginalCommands(SqliteWorldAuthority authority, LegacyTrace original)
    {
        foreach (LegacyCommand stored in original.Commands)
        {
            WorldActionCommand command = WorldActionCodecV1.Decode(stored.Command);
            Assert.Equal(stored.Command, WorldActionCodec.Encode(command));
            WorldSnapshot current = authority.InspectForHost().State;
            ActorControlBinding binding = current.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
            var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
            WorldActionReceipt receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, delivery));
            Assert.Equal(stored.Receipt, WorldReceiptCodecV1.Encode(receipt));
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(command,
                new TrustedCommandDelivery(new EntityId(0x8172635445362718, 6002), binding.SessionId)));
        }
    }

    private static void CopyLegacyFixture(string destination)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "OpenFantasy.Hosting.Tests", "WorldStorage",
                "Fixtures", "legacy-farm-profile-v1.sqlite");
            if (File.Exists(candidate))
            {
                Assert.Equal(FixtureHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(candidate))).ToLowerInvariant());
                File.Copy(candidate, destination);
                return;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("The retained legacy native-world fixture is missing.");
    }

    private static LegacyTrace ReadTrace(string path)
    {
        SQLitePCL.Batteries_V2.Init();
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        byte[] head;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = "SELECT state FROM world_head WHERE singleton=1";
            head = (byte[])query.ExecuteScalar()!;
        }
        List<LegacyCommand> commands = [];
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = "SELECT command,receipt FROM world_command_outcomes ORDER BY sequence";
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
            {
                commands.Add(new LegacyCommand((byte[])reader.GetValue(0), (byte[])reader.GetValue(1)));
            }
        }
        List<EntityId> identities = [];
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = "SELECT identity FROM world_occupied_identity ORDER BY identity";
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
            {
                identities.Add(EntityId.FromBytes((byte[])reader.GetValue(0)));
            }
        }
        return new LegacyTrace(head, commands, identities);
    }

    private sealed record LegacyCommand(byte[] Command, byte[] Receipt);
    private sealed record LegacyTrace(byte[] Head, IReadOnlyList<LegacyCommand> Commands, IReadOnlyList<EntityId> Identities);
}

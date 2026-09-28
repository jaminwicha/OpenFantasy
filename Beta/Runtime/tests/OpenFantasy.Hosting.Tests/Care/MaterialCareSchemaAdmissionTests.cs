using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class MaterialCareSchemaAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recovery_rejects_a_genesis_stored_under_the_other_archive_interpretation(bool coordinated)
    {
        var fixture = new OrdinaryCareArchiveFixture();
        var genesis = coordinated ? WorldCoordinatedAdmission.CreateFresh(fixture.Genesis) : fixture.Genesis;
        using var files = new StorageFiles();
        using (var database = WorldDatabase.Open(files.Path, create: true,
            schemaVersion: coordinated ? WorldDatabaseSchemaSeven.Version : WorldDatabaseSchemaEight.Version))
        {
            database.Initialize(WorldSnapshotCodec.Encode(genesis));
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Fact]
    public void A_fresh_coordinated_world_keeps_both_archive_readers_empty_and_does_not_create_legacy_storage()
    {
        var fixture = new OrdinaryCareArchiveFixture();
        var genesis = WorldCoordinatedAdmission.CreateFresh(fixture.Genesis);
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadMaterialCareArchiveForHost(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadResourceArchiveForHost(0));
            Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        using var database = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(WorldDatabaseSchemaEight.Version, database.SchemaVersion);
        Assert.Empty(database.ReadMaterialCareArchives());
        Assert.Empty(database.ReadResourceArchives());
    }
}

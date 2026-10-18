using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

internal sealed partial class WorldDatabase
{
    internal bool SeparatesTerrain => SchemaVersion == WorldDatabaseSchemaThirteen.Version;
    internal int TerrainArchiveReads { get; private set; }
    internal Action? BeforeTerrainRead { get; set; }

    internal byte[] EncodeHead(WorldSnapshot world) => SeparatesTerrain
        ? ConstructionLocalityEncoding.Encode(world) : WorldSnapshotCodec.Encode(world);

    private void InitializeTerrain(WorldSnapshot genesis, SqliteTransaction transaction)
    {
        foreach (var region in genesis.Space.Regions)
        {
            byte[] bytes = TerrainRegionCodecV1.Encode(region);
            ChangeExactlyOne("INSERT INTO world_terrain VALUES($hash,$state)", transaction,
                ("$hash", ContentHash.Compute(bytes).ToByteArray()), ("$state", bytes));
        }
    }

    internal TerrainRegionSnapshot ReadTerrain(TerrainRegionReference reference, SqliteTransaction? transaction = null)
    {
        if (!SeparatesTerrain) throw new InvalidOperationException("This storage does not separate terrain.");
        BeforeTerrainRead?.Invoke();
        TerrainArchiveReads = checked(TerrainArchiveReads + 1);
        using var command = _lease.CreateCommand("SELECT state FROM world_terrain WHERE hash=$hash", transaction,
            ("$hash", reference.ContentHash.ToByteArray()));
        using var reader = command.ExecuteReader();
        RequireRow(reader, "A pinned terrain archive is missing.");
        byte[] bytes = WorldStorageBinary.ReadBlob(reader, 0, 1, TerrainRegionCodecV1.MaximumEncodedByteLength);
        if (ContentHash.Compute(bytes) != reference.ContentHash) throw new InvalidDataException("A terrain archive has changed.");
        var region = TerrainRegionCodecV1.Decode(bytes);
        if (TerrainRegionReference.Capture(region) != reference) throw new InvalidDataException("A terrain archive belongs to another region.");
        return region;
    }

    internal void VerifyTerrain(WorldSnapshot genesis)
    {
        if (!SeparatesTerrain) return;
        using (var command = _lease.CreateCommand("SELECT COUNT(*) FROM world_terrain"))
            if (Convert.ToInt64(command.ExecuteScalar()) != genesis.Space.Regions.Count)
                throw new InvalidDataException("The terrain directory differs from admitted genesis.");
        foreach (var region in genesis.Space.Regions)
        {
            var loaded = ReadTerrain(TerrainRegionReference.Capture(region));
            if (!TerrainRegionCodecV1.Encode(region).AsSpan().SequenceEqual(TerrainRegionCodecV1.Encode(loaded)))
                throw new InvalidDataException("Terrain differs from original admission.");
        }
    }
}

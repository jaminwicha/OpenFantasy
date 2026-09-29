using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

internal sealed partial class WorldDatabase
{
    internal IEnumerable<StoredWorldMaterialCareArchive> ReadMaterialCareArchives()
    {
        if (SchemaVersion != WorldDatabaseSchemaEight.Version)
        {
            yield break;
        }
        using SqliteCommand command = _lease.CreateCommand("""
            SELECT world_id,branch_id,ordinal,boundary_sequence,publication_sequence,boundary_hash,state
            FROM world_material_care_segments ORDER BY publication_sequence
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadMaterialCareArchive(reader);
        }
    }

    internal void WriteMaterialCareArchive(StoredWorldMaterialCareArchive value, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.Reference);
        if (SchemaVersion != WorldDatabaseSchemaEight.Version)
        {
            throw new InvalidOperationException("Material and care archives require their explicit storage schema.");
        }
        WorldStorageBinary.RequireBytes(value.State, MaximumStateByteLength, nameof(value.State));
        ValidateMaterialCareArchive(value);
        ValidateMaterialCareArchivePublication(value, transaction);
        WorldMaterialCareArchiveReference reference = value.Reference;
        ChangeExactlyOne("""
            INSERT INTO world_material_care_segments VALUES($world,$branch,$ordinal,$boundary,$publication,$hash,$state)
            """, transaction, ("$world", reference.WorldId.ToByteArray()),
            ("$branch", reference.BranchId.ToByteArray()), ("$ordinal", WorldStorageBinary.Number(reference.Ordinal)),
            ("$boundary", WorldStorageBinary.Number(reference.BoundarySequence)),
            ("$publication", WorldStorageBinary.Number(value.PublicationSequence)),
            ("$hash", reference.BoundaryHash.ToByteArray()), ("$state", value.State));
    }

    private void ValidateMaterialCareArchivePublication(StoredWorldMaterialCareArchive value, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        // Publication precedes the head update in the same transaction. The archive is the actual
        // stored prior root, not merely another root bearing a caller-supplied digest or scope.
        StoredWorldHead before = ReadHead(transaction);
        if (before.Sequence != value.Reference.BoundarySequence || !before.State.AsSpan().SequenceEqual(value.State))
        {
            throw new InvalidDataException("A material and care archive must close the exact stored prior world head.");
        }
        using (SqliteCommand genesis = _lease.CreateCommand("SELECT state,hash FROM world_genesis WHERE singleton=1", transaction))
        using (SqliteDataReader reader = genesis.ExecuteReader())
        {
            RequireRow(reader, "A material and care archive has no accepted world genesis.");
            WorldSnapshot world = WorldSnapshotCodec.Decode(WorldStorageBinary.ReadState(reader, 0, 1));
            if (world.ProfileVersion is not (27 or 28) || world.WorldId != value.Reference.WorldId || world.BranchId != value.Reference.BranchId)
            {
                throw new InvalidDataException("A material and care archive must retain the accepted world scope.");
            }
        }
        using SqliteCommand commit = _lease.CreateCommand(
            "SELECT before_hash FROM world_commits WHERE sequence=$sequence", transaction,
            ("$sequence", WorldStorageBinary.Number(value.PublicationSequence)));
        using SqliteDataReader publication = commit.ExecuteReader();
        RequireRow(publication, "A material and care archive requires its publishing commit in the same transaction.");
        if (WorldStorageBinary.ReadHash(publication, 0) != value.Reference.BoundaryHash)
        {
            throw new InvalidDataException("A material and care archive must match its publishing commit's prior hash.");
        }
    }

    private static StoredWorldMaterialCareArchive ReadMaterialCareArchive(SqliteDataReader reader)
    {
        WorldMaterialCareArchiveReference reference;
        try
        {
            reference = new WorldMaterialCareArchiveReference(WorldStorageBinary.ReadIdentity(reader, 0),
                WorldStorageBinary.ReadIdentity(reader, 1), WorldStorageBinary.ReadNumber(reader, 2),
                WorldStorageBinary.ReadNumber(reader, 3), WorldStorageBinary.ReadHash(reader, 5));
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException("A stored material and care archive has an invalid boundary reference.", error);
        }
        var value = new StoredWorldMaterialCareArchive(reference, WorldStorageBinary.ReadNumber(reader, 4),
            WorldStorageBinary.ReadBlob(reader, 6, 1, MaximumStateByteLength));
        ValidateMaterialCareArchive(value);
        return value;
    }

    private static void ValidateMaterialCareArchive(StoredWorldMaterialCareArchive value)
    {
        if (value.Reference.BoundarySequence == ulong.MaxValue
            || value.PublicationSequence != value.Reference.BoundarySequence + 1)
        {
            throw new InvalidDataException("A material and care archive must be published by the immediate successor commit.");
        }
        if (ContentHash.Compute(value.State) != value.Reference.BoundaryHash)
        {
            throw new InvalidDataException("Stored material and care archive bytes do not match their boundary hash.");
        }
    }
}

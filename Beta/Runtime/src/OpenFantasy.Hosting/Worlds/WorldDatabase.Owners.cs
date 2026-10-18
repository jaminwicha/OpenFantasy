using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Construction.Ownership;

namespace OpenFantasy.Hosting.Worlds;

internal sealed partial class WorldDatabase
{
    internal const long MaximumOwnerArchiveCount = 100_000;
    internal const long MaximumOwnerArchiveBytes = 256 * 1024 * 1024;
    internal bool SeparatesOwners => SchemaVersion == WorldDatabaseSchemaFourteen.Version;
    internal WorldStateEncoding StateEncoding => WorldStateImage.EncodingFor(SchemaVersion);

    internal void WriteOwners(ConstructionOwnerDirectory owners, SqliteTransaction transaction)
    {
        if (!SeparatesOwners) throw new InvalidOperationException("This schema does not store typed owner images.");
        foreach (var owner in owners.Owners)
        {
            byte[] bytes = owner.CopyBytes();
            using var command = _lease.CreateCommand("INSERT OR IGNORE INTO world_owners VALUES($hash,$state)", transaction,
                ("$hash", owner.Hash.ToByteArray()), ("$state", bytes));
            if (command.ExecuteNonQuery() == 0) VerifyOwner(owner, transaction);
        }
        var usage = ReadOwnerUsage(transaction);
        if (usage.Count > MaximumOwnerArchiveCount || usage.Bytes > MaximumOwnerArchiveBytes)
            throw new WorldRecoveryLimitException("The typed owner experiment reached its explicit archive envelope.");
    }

    internal void VerifyOwner(ConstructionOwnerPayload owner, SqliteTransaction? transaction = null)
    {
        using var command = _lease.CreateCommand("SELECT state FROM world_owners WHERE hash=$hash", transaction,
            ("$hash", owner.Hash.ToByteArray()));
        using var reader = command.ExecuteReader();
        RequireRow(reader, "A referenced typed owner image is missing.");
        byte[] bytes = WorldStorageBinary.ReadBlob(reader, 0, 1, ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength);
        if (!bytes.AsSpan().SequenceEqual(owner.CopyBytes()))
            throw new InvalidDataException("A typed owner image differs from its exact ordinary-history interpretation.");
    }

    internal (long Count, long Bytes) ReadOwnerUsage(SqliteTransaction? transaction = null)
    {
        using var command = _lease.CreateCommand("SELECT COUNT(*),COALESCE(SUM(length(state)),0) FROM world_owners", transaction);
        using var reader = command.ExecuteReader();
        RequireRow(reader, "Typed owner accounting is unavailable.");
        return (reader.GetInt64(0), reader.GetInt64(1));
    }
}

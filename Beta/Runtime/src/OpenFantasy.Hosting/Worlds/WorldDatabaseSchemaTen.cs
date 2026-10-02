namespace OpenFantasy.Hosting.Worlds;

/// <summary>Explicit operational recovery metadata; inherited canonical world rows retain schema-nine definitions.</summary>
internal static class WorldDatabaseSchemaTen
{
    internal const int Version = 10;
    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static readonly IReadOnlyDictionary<string, string> Indexes = CreateIndexes();

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaNine.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables.Add("controller_recovery_activation", """
            CREATE TABLE controller_recovery_activation (
                singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                policy BLOB NOT NULL CHECK(typeof(policy)='blob' AND length(policy)=32),
                provenance BLOB NOT NULL CHECK(typeof(provenance)='blob' AND length(provenance) BETWEEN 43 AND 235),
                hash BLOB NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32))
            """);
        tables.Add("controller_recovery_segment", """
            CREATE TABLE controller_recovery_segment (
                singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                generation BLOB NOT NULL CHECK(typeof(generation)='blob' AND length(generation)=8),
                hash BLOB NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32))
            """);
        tables.Add("controller_allowances", """
            CREATE TABLE controller_allowances (
                actor BLOB PRIMARY KEY NOT NULL CHECK(typeof(actor)='blob' AND length(actor)=16),
                revision BLOB NOT NULL CHECK(typeof(revision)='blob' AND length(revision)=8),
                episode BLOB UNIQUE NOT NULL CHECK(typeof(episode)='blob' AND length(episode)=16),
                elapsed BLOB NOT NULL CHECK(typeof(elapsed)='blob' AND length(elapsed)=8))
            """);
        return tables;
    }

    private static IReadOnlyDictionary<string, string> CreateIndexes()
    {
        var indexes = WorldDatabaseSchemaNine.Indexes.ToDictionary(pair => pair.Key, pair => pair.Value);
        indexes.Add("sqlite_autoindex_controller_allowances_1", "controller_allowances");
        indexes.Add("sqlite_autoindex_controller_allowances_2", "controller_allowances");
        return indexes;
    }
}

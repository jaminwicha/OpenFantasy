namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact fresh schema for finite care and atomic resource archive publication.</summary>
internal static class WorldDatabaseSchemaSeven
{
    internal const int Version = 7;

    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static readonly IReadOnlyDictionary<string, string> Indexes = CreateIndexes();

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaSix.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3,5,6,7,8)", "kind IN (1,2,3,5,6,7,8,9)", StringComparison.Ordinal);
        tables["world_resource_segments"] = $"""
            CREATE TABLE world_resource_segments (
                world_id BLOB NOT NULL CHECK(typeof(world_id)='blob' AND length(world_id)=16),
                branch_id BLOB NOT NULL CHECK(typeof(branch_id)='blob' AND length(branch_id)=16 AND branch_id!=world_id),
                ordinal BLOB NOT NULL CHECK(typeof(ordinal)='blob' AND length(ordinal)=8),
                boundary_sequence BLOB NOT NULL CHECK(typeof(boundary_sequence)='blob' AND length(boundary_sequence)=8 AND boundary_sequence!=x'0000000000000000'),
                publication_sequence BLOB NOT NULL REFERENCES world_commits(sequence) CHECK(typeof(publication_sequence)='blob' AND length(publication_sequence)=8 AND publication_sequence!=x'0000000000000000' AND publication_sequence>boundary_sequence),
                boundary_hash BLOB NOT NULL CHECK(typeof(boundary_hash)='blob' AND length(boundary_hash)=32),
                state BLOB NOT NULL CHECK(typeof(state)='blob' AND length(state) BETWEEN 1 AND {WorldDatabase.MaximumStateByteLength}),
                PRIMARY KEY(world_id,branch_id,ordinal),
                UNIQUE(publication_sequence))
            """;
        return tables;
    }

    private static IReadOnlyDictionary<string, string> CreateIndexes()
    {
        var indexes = WorldDatabaseSchemaSix.Indexes.ToDictionary(pair => pair.Key, pair => pair.Value);
        indexes.Add("sqlite_autoindex_world_resource_segments_1", "world_resource_segments");
        indexes.Add("sqlite_autoindex_world_resource_segments_2", "world_resource_segments");
        return indexes;
    }
}

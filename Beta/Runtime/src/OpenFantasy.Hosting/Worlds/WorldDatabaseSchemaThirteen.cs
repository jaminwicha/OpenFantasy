using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Explicit fresh legacy construction storage with immutable terrain and replay-verified locality images.</summary>
internal static class WorldDatabaseSchemaThirteen
{
    internal const int Version = 13;
    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static readonly IReadOnlyDictionary<string, string> Indexes =
        new Dictionary<string, string>(WorldDatabaseSchema.Indexes)
        {
            ["sqlite_autoindex_world_terrain_1"] = "world_terrain"
        };

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchema.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables.Add("world_terrain", $"""
            CREATE TABLE world_terrain (hash BLOB PRIMARY KEY NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32),
                state BLOB NOT NULL CHECK(typeof(state)='blob' AND length(state) BETWEEN 1 AND {TerrainRegionCodecV1.MaximumEncodedByteLength}))
            """);
        return tables;
    }
}

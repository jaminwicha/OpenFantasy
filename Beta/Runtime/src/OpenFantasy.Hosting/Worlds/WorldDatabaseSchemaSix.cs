namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact fresh schema for permanent encounter lifecycle boundaries and their existing timed commits.</summary>
internal static class WorldDatabaseSchemaSix
{
    internal const int Version = 6;

    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchemaFive.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaFive.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3,5,6,7)", "kind IN (1,2,3,5,6,7,8)", StringComparison.Ordinal);
        return tables;
    }
}

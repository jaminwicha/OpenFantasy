namespace OpenFantasy.Hosting.Worlds;

/// <summary>Fresh permission journals retain measured controller recovery and the exact earlier schemas.</summary>
internal static class WorldDatabaseSchemaEleven
{
    internal const int Version = 11;
    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchemaTen.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaTen.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3,5,6,7,8,9,10)", "kind IN (1,2,3,5,6,7,8,9,10,11)", StringComparison.Ordinal);
        return tables;
    }
}

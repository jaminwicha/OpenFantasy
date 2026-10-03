namespace OpenFantasy.Hosting.Worlds;

/// <summary>Fresh effective-control journals retain one writer and measured controller recovery.</summary>
internal static class WorldDatabaseSchemaTwelve
{
    internal const int Version = 12;
    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchemaEleven.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaEleven.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3,5,6,7,8,9,10,11)", "kind IN (1,2,3,5,6,7,8,9,10,11,12)", StringComparison.Ordinal);
        return tables;
    }
}

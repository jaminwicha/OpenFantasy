namespace OpenFantasy.Hosting.Worlds;

/// <summary>Controller-event storage; every previous schema keeps its original accepted commit kinds.</summary>
internal static class WorldDatabaseSchemaNine
{
    internal const int Version = 9;
    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchemaEight.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaEight.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3,5,6,7,8,9)", "kind IN (1,2,3,5,6,7,8,9,10)", StringComparison.Ordinal);
        return tables;
    }
}

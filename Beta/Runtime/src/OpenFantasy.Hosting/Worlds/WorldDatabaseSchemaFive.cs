namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact fresh schema for tactical ticks and zero-duration encounter boundaries.</summary>
internal static class WorldDatabaseSchemaFive
{
    internal const int Version = 5;

    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchemaThree.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaThree.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3)", "kind IN (1,2,3,5,6,7)", StringComparison.Ordinal);
        return tables;
    }
}

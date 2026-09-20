namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact additive schema for retained consequence continuation commits.</summary>
internal static class WorldDatabaseSchemaFour
{
    internal const int Version = 4;

    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchemaThree.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchemaThree.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("kind IN (1,2,3)", "kind IN (1,2,3,4)", StringComparison.Ordinal);
        return tables;
    }
}

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact additive schema for explicit production admission commits.</summary>
internal static class WorldDatabaseSchemaThree
{
    internal const int Version = 3;

    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static IReadOnlyDictionary<string, string> Indexes => WorldDatabaseSchema.Indexes;

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = WorldDatabaseSchema.Tables.ToDictionary(pair => pair.Key, pair => pair.Value);
        tables["world_commits"] = tables["world_commits"]
            .Replace("CREATE TABLE world_commits", "CREATE TABLE \"world_commits\"", StringComparison.Ordinal)
            .Replace("kind IN (1,2)", "kind IN (1,2,3)", StringComparison.Ordinal);
        tables["world_occupied_identity"] = tables["world_occupied_identity"]
            .Replace("CREATE TABLE world_occupied_identity", "CREATE TABLE \"world_occupied_identity\"", StringComparison.Ordinal)
            .Replace("kind IN (1,2,3,4)", "kind IN (1,2,3,4,5)", StringComparison.Ordinal);
        return tables;
    }
}

using OpenFantasy.Core.Construction.Ownership;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Fresh legacy construction directory and immutable owners, retaining the original terrain format.</summary>
internal static class WorldDatabaseSchemaFourteen
{
    internal const int Version = 14;
    internal static readonly IReadOnlyDictionary<string, string> Tables = CreateTables();
    internal static readonly IReadOnlyDictionary<string, string> Indexes =
        new Dictionary<string, string>(WorldDatabaseSchemaThirteen.Indexes)
        {
            ["sqlite_autoindex_world_owners_1"] = "world_owners"
        };

    private static IReadOnlyDictionary<string, string> CreateTables()
    {
        var tables = new Dictionary<string, string>(WorldDatabaseSchemaThirteen.Tables);
        tables.Add("world_owners", $"""
            CREATE TABLE world_owners (hash BLOB PRIMARY KEY NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32),
                state BLOB NOT NULL CHECK(typeof(state)='blob' AND length(state) BETWEEN 1 AND {ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength}))
            """);
        return tables;
    }
}

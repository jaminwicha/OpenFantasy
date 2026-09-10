namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact schema for compact local-world commits, distinct from the retained counted profile.</summary>
internal static class WorldDatabaseSchema
{
    internal const int Version = 2;

    internal static readonly IReadOnlyDictionary<string, string> Tables = new Dictionary<string, string>
    {
        ["world_genesis"] = $"""
            CREATE TABLE world_genesis (singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                state BLOB NOT NULL CHECK(typeof(state)='blob' AND length(state) BETWEEN 1 AND {WorldDatabase.MaximumStateByteLength}),
                hash BLOB NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32))
            """,
        ["world_head"] = $"""
            CREATE TABLE world_head (singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                sequence BLOB NOT NULL CHECK(typeof(sequence)='blob' AND length(sequence)=8),
                advancing_steps BLOB NOT NULL CHECK(typeof(advancing_steps)='blob' AND length(advancing_steps)=8),
                state BLOB NOT NULL CHECK(typeof(state)='blob' AND length(state) BETWEEN 1 AND {WorldDatabase.MaximumStateByteLength}),
                hash BLOB NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32))
            """,
        ["world_commits"] = $"""
            CREATE TABLE world_commits (
                sequence BLOB PRIMARY KEY NOT NULL CHECK(typeof(sequence)='blob' AND length(sequence)=8 AND sequence!=x'0000000000000000'),
                kind INTEGER NOT NULL CHECK(kind IN (1,2)),
                cause_id BLOB NOT NULL CHECK(typeof(cause_id)='blob' AND length(cause_id)=16),
                advancing_steps BLOB NOT NULL CHECK(typeof(advancing_steps)='blob' AND length(advancing_steps)=8),
                accepted_batch BLOB NOT NULL CHECK(typeof(accepted_batch)='blob' AND length(accepted_batch) BETWEEN 1 AND {WorldDatabase.MaximumBatchByteLength}),
                outcomes BLOB NOT NULL CHECK(typeof(outcomes)='blob' AND length(outcomes) BETWEEN 1 AND {WorldDatabase.MaximumOutcomesByteLength}),
                before_hash BLOB NOT NULL CHECK(typeof(before_hash)='blob' AND length(before_hash)=32),
                after_hash BLOB NOT NULL CHECK(typeof(after_hash)='blob' AND length(after_hash)=32),
                UNIQUE(cause_id))
            """,
        ["world_command_outcomes"] = $"""
            CREATE TABLE world_command_outcomes (
                principal BLOB NOT NULL CHECK(typeof(principal)='blob' AND length(principal)=16),
                command_id BLOB NOT NULL CHECK(typeof(command_id)='blob' AND length(command_id)=16),
                sequence BLOB NOT NULL REFERENCES world_commits(sequence) CHECK(typeof(sequence)='blob' AND length(sequence)=8 AND sequence!=x'0000000000000000'),
                command BLOB NOT NULL CHECK(typeof(command)='blob' AND length(command) BETWEEN 1 AND {WorldDatabase.MaximumCommandByteLength}),
                receipt BLOB NOT NULL CHECK(typeof(receipt)='blob' AND length(receipt) BETWEEN 1 AND {WorldDatabase.MaximumReceiptByteLength}),
                PRIMARY KEY(principal,command_id))
            """,
        ["world_occupied_identity"] = """
            CREATE TABLE world_occupied_identity (
                identity BLOB PRIMARY KEY NOT NULL CHECK(typeof(identity)='blob' AND length(identity)=16),
                sequence BLOB NOT NULL REFERENCES world_commits(sequence) CHECK(typeof(sequence)='blob' AND length(sequence)=8 AND sequence!=x'0000000000000000'),
                kind INTEGER NOT NULL CHECK(kind IN (1,2,3,4)))
            """,
        ["world_checkpoints"] = $"""
            CREATE TABLE world_checkpoints (
                sequence BLOB PRIMARY KEY NOT NULL CHECK(typeof(sequence)='blob' AND length(sequence)=8),
                advancing_steps BLOB NOT NULL CHECK(typeof(advancing_steps)='blob' AND length(advancing_steps)=8),
                state BLOB NOT NULL CHECK(typeof(state)='blob' AND length(state) BETWEEN 1 AND {WorldDatabase.MaximumStateByteLength}),
                hash BLOB NOT NULL CHECK(typeof(hash)='blob' AND length(hash)=32))
            """
    };

    internal static readonly IReadOnlyDictionary<string, string> Indexes = new Dictionary<string, string>
    {
        ["sqlite_autoindex_world_commits_1"] = "world_commits",
        ["sqlite_autoindex_world_commits_2"] = "world_commits",
        ["sqlite_autoindex_world_command_outcomes_1"] = "world_command_outcomes",
        ["sqlite_autoindex_world_occupied_identity_1"] = "world_occupied_identity",
        ["sqlite_autoindex_world_checkpoints_1"] = "world_checkpoints"
    };
}

using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Validates the complete bounded trace before readiness. Checkpoints are compared with replay;
/// they do not bypass journal validation or prove that an edited database came from a trusted host.
/// </summary>
internal sealed class WorldJournalRecovery
{
    internal RecoveredWorld Recover(WorldDatabase database)
    {
        try
        {
            return Replay(database);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The local world journal violates its replay contract.", error);
        }
    }

    private static RecoveredWorld Replay(WorldDatabase database)
    {
        byte[] genesisBytes = database.ReadGenesis();
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
        if (genesis.Revision != 0 || genesis.AdvancingSteps != 0 || genesis.HasReadingHold)
        {
            throw new InvalidDataException("The local world genesis does not start at an unheld commit zero.");
        }
        if (genesis.ProfileVersion == 3 && database.SchemaVersion != WorldDatabaseSchemaThree.Version)
        {
            throw new InvalidDataException("A production genesis requires its declared world schema.");
        }
        WorldSnapshot state = genesis;
        byte[] stateBytes = genesisBytes;
        ContentHash stateHash = ContentHash.Compute(stateBytes);
        var usage = new WorldJournalUsage(0, 0, 0, 0, 0);
        Dictionary<EntityId, StoredWorldIdentity> identities = [];
        Dictionary<(EntityId Principal, EntityId Command), StoredWorldCommandOutcome> commands = [];
        var steps = new WorldCommitPreparation();
        var migrations = new WorldMigrationPreparation();
        using IEnumerator<StoredWorldCheckpoint> checkpoints = database.ReadCheckpoints().GetEnumerator();
        if (!checkpoints.MoveNext())
        {
            throw new InvalidDataException("The world is missing its genesis checkpoint.");
        }
        VerifyCheckpoint(checkpoints.Current, 0, 0, genesisBytes);
        bool hasCheckpoint = checkpoints.MoveNext();
        foreach (StoredWorldCommit stored in database.ReadJournal())
        {
            if (stored.Sequence != checked(state.Revision + 1) || stored.BeforeHash != stateHash)
            {
                throw new InvalidDataException("The local world journal is discontinuous.");
            }
            ExpectedJournalEntry expected = PrepareEntry(database.SchemaVersion, stored, state,
                genesis.IdentityIndex, stateHash, identities.ContainsKey, steps, migrations);
            WorldJournalAdvance advance = WorldJournalPolicy.Advance(usage, stored.Sequence, stored.AdvancingSteps,
                (ulong)expected.Commands.Count, (ulong)stored.AcceptedBatch.Length, (ulong)stored.Outcomes.Length);
            VerifyCommit(stored, expected.Stored);
            foreach (StoredWorldCommandOutcome command in expected.Commands)
            {
                if (!commands.TryAdd((command.PrincipalId, command.CommandId), command))
                {
                    throw new InvalidDataException("The world journal repeats an actor command key.");
                }
            }
            foreach (StoredWorldIdentity identity in expected.Identities)
            {
                if (!identities.TryAdd(identity.Identity, identity))
                {
                    throw new InvalidDataException("The world journal repeats a committed identity.");
                }
            }
            if (advance.WriteCheckpoint)
            {
                if (!hasCheckpoint)
                {
                    throw new InvalidDataException("The world is missing a required checkpoint.");
                }
                VerifyCheckpoint(checkpoints.Current, stored.Sequence, stored.AdvancingSteps, expected.CandidateBytes);
                hasCheckpoint = checkpoints.MoveNext();
            }
            else if (hasCheckpoint && checkpoints.Current.Sequence <= stored.Sequence)
            {
                throw new InvalidDataException("The world contains an unscheduled checkpoint.");
            }
            state = expected.Candidate;
            stateBytes = expected.CandidateBytes;
            stateHash = expected.Stored.AfterHash;
            usage = advance.Usage;
        }
        if (hasCheckpoint)
        {
            throw new InvalidDataException("The world contains a checkpoint beyond its accepted journal.");
        }
        if (database.SchemaVersion == WorldDatabaseSchemaThree.Version && state.ProfileVersion != 3)
        {
            throw new InvalidDataException("The third world schema has no accepted production admission.");
        }
        StoredWorldHead head = database.ReadHead();
        if (head.Sequence != state.Revision || head.AdvancingSteps != state.AdvancingSteps
            || !head.State.AsSpan().SequenceEqual(stateBytes))
        {
            throw new InvalidDataException("The stored world head does not match exact replay.");
        }
        VerifyCommandTable(database, commands);
        VerifyIdentityTable(database, identities);
        return new RecoveredWorld(genesis, state, stateHash, usage);
    }

    private static ExpectedJournalEntry PrepareEntry(int schemaVersion, StoredWorldCommit stored,
        WorldSnapshot state, WorldIdentityIndex genesisIdentities, ContentHash stateHash,
        Func<EntityId, bool> historicallyOccupied, WorldCommitPreparation steps, WorldMigrationPreparation migrations)
    {
        if (stored.Kind == WorldCommitKind.ProductionMigration)
        {
            if (schemaVersion != WorldDatabaseSchemaThree.Version)
            {
                throw new InvalidDataException("This schema cannot contain a production migration.");
            }
            WorldProductionMigration request = WorldProductionMigrationCodecV1.Decode(stored.AcceptedBatch);
            PreparedWorldMigration expected = migrations.Prepare(state, genesisIdentities,
                stateHash, request, historicallyOccupied);
            return new(expected.Decision.Candidate, expected.CandidateBytes, expected.Stored, [], expected.Identities);
        }
        WorldStepBatch batch = WorldStepCodec.Decode(stored.AcceptedBatch);
        PreparedWorldCommit step = steps.Prepare(state, genesisIdentities,
            stateHash, batch, historicallyOccupied);
        return new(step.Decision.Candidate, step.CandidateBytes, step.Stored, step.Commands, step.Identities);
    }

    private static void VerifyCommit(StoredWorldCommit stored, StoredWorldCommit expected)
    {
        if (stored.Sequence != expected.Sequence || stored.Kind != expected.Kind || stored.CauseId != expected.CauseId
            || stored.AdvancingSteps != expected.AdvancingSteps || stored.BeforeHash != expected.BeforeHash
            || stored.AfterHash != expected.AfterHash || !stored.AcceptedBatch.AsSpan().SequenceEqual(expected.AcceptedBatch)
            || !stored.Outcomes.AsSpan().SequenceEqual(expected.Outcomes))
        {
            throw new InvalidDataException("A stored world batch or outcome differs from deterministic replay.");
        }
    }

    private static void VerifyCheckpoint(StoredWorldCheckpoint stored, ulong sequence, ulong steps, byte[] bytes)
    {
        if (stored.Sequence != sequence || stored.AdvancingSteps != steps || !stored.State.AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidDataException("A retained checkpoint differs from its exact replay boundary.");
        }
    }

    private static void VerifyCommandTable(WorldDatabase database,
        IReadOnlyDictionary<(EntityId Principal, EntityId Command), StoredWorldCommandOutcome> commands)
    {
        int count = 0;
        foreach (StoredWorldCommandOutcome stored in database.ReadCommandOutcomes())
        {
            if (!commands.TryGetValue((stored.PrincipalId, stored.CommandId), out StoredWorldCommandOutcome? expected)
                || stored.Sequence != expected.Sequence || !stored.Command.AsSpan().SequenceEqual(expected.Command)
                || !stored.Receipt.AsSpan().SequenceEqual(expected.Receipt))
            {
                throw new InvalidDataException("A stored actor command or receipt differs from its replayed commit.");
            }
            count++;
        }
        if (count != commands.Count)
        {
            throw new InvalidDataException("The retained actor-command table is incomplete.");
        }
    }

    private static void VerifyIdentityTable(WorldDatabase database, IReadOnlyDictionary<EntityId, StoredWorldIdentity> identities)
    {
        int count = 0;
        foreach (StoredWorldIdentity stored in database.ReadIdentities())
        {
            if (!identities.TryGetValue(stored.Identity, out StoredWorldIdentity? expected) || stored != expected)
            {
                throw new InvalidDataException("A retained identity differs from its replayed commit or role.");
            }
            count++;
        }
        if (count != identities.Count)
        {
            throw new InvalidDataException("The retained world-identity table is incomplete.");
        }
    }
}

internal sealed record ExpectedJournalEntry(WorldSnapshot Candidate, byte[] CandidateBytes, StoredWorldCommit Stored,
    IReadOnlyList<StoredWorldCommandOutcome> Commands, IReadOnlyList<StoredWorldIdentity> Identities);

internal sealed record RecoveredWorld(WorldSnapshot Genesis, WorldSnapshot State, ContentHash StateHash, WorldJournalUsage Usage);

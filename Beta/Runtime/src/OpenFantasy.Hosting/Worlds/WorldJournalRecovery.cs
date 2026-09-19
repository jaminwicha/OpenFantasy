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
    internal RecoveredWorld Recover(WorldDatabase database) => VerifyAndReplay(database, null).Head;

    internal RecordedWorldState ReadRecordedState(WorldDatabase database, ulong revision)
    {
        ReplayedWorld trace = VerifyAndReplay(database, revision);
        WorldSnapshot selected = trace.Selected
            ?? throw new ArgumentOutOfRangeException(nameof(revision), "The requested commit is not in the recorded world.");
        return new RecordedWorldState(selected, trace.Head.State.Revision, trace.Head.StateHash);
    }

    private static ReplayedWorld VerifyAndReplay(WorldDatabase database, ulong? selectedRevision)
    {
        try
        {
            return Replay(database, selectedRevision);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The local world journal violates its replay contract.", error);
        }
    }

    private static ReplayedWorld Replay(WorldDatabase database, ulong? selectedRevision)
    {
        byte[] genesisBytes = database.ReadGenesis();
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
        if (genesis.Revision != 0 || genesis.AdvancingSteps != 0 || genesis.HasReadingHold)
        {
            throw new InvalidDataException("The local world genesis does not start at an unheld commit zero.");
        }
        if (genesis.ProfileVersion is 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 && database.SchemaVersion != WorldDatabaseSchemaThree.Version)
        {
            throw new InvalidDataException("A production, quest or construction genesis requires its declared world schema.");
        }
        WorldSnapshot state = genesis;
        WorldSnapshot? selected = selectedRevision == 0 ? genesis : null;
        byte[] stateBytes = genesisBytes;
        ContentHash stateHash = ContentHash.Compute(stateBytes);
        var usage = new WorldJournalUsage(0, 0, 0, 0, 0);
        Dictionary<EntityId, StoredWorldIdentity> identities = [];
        Dictionary<(EntityId Principal, EntityId Command), StoredWorldCommandOutcome> commands = [];
        var steps = new WorldCommitPreparation();
        var migrations = new WorldProfileMigrationReplay();
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
            if (state.Revision == selectedRevision)
            {
                selected = state;
            }
            stateBytes = expected.CandidateBytes;
            stateHash = expected.Stored.AfterHash;
            usage = advance.Usage;
        }
        if (hasCheckpoint)
        {
            throw new InvalidDataException("The world contains a checkpoint beyond its accepted journal.");
        }
        if (database.SchemaVersion == WorldDatabaseSchemaThree.Version && state.ProfileVersion is not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10 and not 11 and not 12)
        {
            throw new InvalidDataException("The third world schema has no accepted production, quest or construction admission.");
        }
        StoredWorldHead head = database.ReadHead();
        if (head.Sequence != state.Revision || head.AdvancingSteps != state.AdvancingSteps
            || !head.State.AsSpan().SequenceEqual(stateBytes))
        {
            throw new InvalidDataException("The stored world head does not match exact replay.");
        }
        VerifyCommandTable(database, commands);
        VerifyIdentityTable(database, identities);
        return new ReplayedWorld(new RecoveredWorld(genesis, state, stateHash, usage), selected);
    }

    private static ExpectedJournalEntry PrepareEntry(int schemaVersion, StoredWorldCommit stored,
        WorldSnapshot state, WorldIdentityIndex genesisIdentities, ContentHash stateHash,
        Func<EntityId, bool> historicallyOccupied, WorldCommitPreparation steps, WorldProfileMigrationReplay migrations)
    {
        if (stored.Kind == WorldCommitKind.ProfileMigration)
        {
            if (schemaVersion != WorldDatabaseSchemaThree.Version)
            {
                throw new InvalidDataException("This schema cannot contain a profile migration.");
            }
            return migrations.Prepare(stored, state, genesisIdentities, stateHash, historicallyOccupied);
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

    private sealed record ReplayedWorld(RecoveredWorld Head, WorldSnapshot? Selected);
}

internal sealed record ExpectedJournalEntry(WorldSnapshot Candidate, byte[] CandidateBytes, StoredWorldCommit Stored,
    IReadOnlyList<StoredWorldCommandOutcome> Commands, IReadOnlyList<StoredWorldIdentity> Identities);

internal sealed record RecoveredWorld(WorldSnapshot Genesis, WorldSnapshot State, ContentHash StateHash, WorldJournalUsage Usage);

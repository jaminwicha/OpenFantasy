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

    internal RecordedWorldResourceArchive ReadResourceArchive(WorldDatabase database, ulong ordinal)
    {
        ReplayedWorld trace = VerifyAndReplay(database, null, ordinal);
        WorldResourceArchiveSegment selected = trace.SelectedArchive
            ?? throw new ArgumentOutOfRangeException(nameof(ordinal), "The requested resource archive is not in the recorded world.");
        return new(selected, trace.ArchivePublicationSequence, trace.Head.State.Revision, trace.Head.StateHash);
    }

    internal RecordedWorldMaterialCareArchive ReadMaterialCareArchive(WorldDatabase database, ulong ordinal)
    {
        ReplayedWorld trace = VerifyAndReplay(database, null, selectedMaterialCareOrdinal: ordinal);
        WorldMaterialCareArchiveSegment selected = trace.SelectedMaterialCareArchive
            ?? throw new ArgumentOutOfRangeException(nameof(ordinal), "The requested material and care archive is not in the recorded world.");
        return new(selected, trace.MaterialCarePublicationSequence, trace.Head.State.Revision, trace.Head.StateHash);
    }

    internal RecordedWorldMaterialEvidence ReadMaterialEvidence(WorldDatabase database, ulong sequence)
    {
        ReplayedWorld trace = VerifyAndReplay(database, null, selectedMaterialSequence: sequence);
        var selected = trace.SelectedMaterial
            ?? throw new ArgumentOutOfRangeException(nameof(sequence), "The requested material observation has no recorded commit.");
        return new(sequence, selected.BeforeHash, selected.AfterHash, selected.Evidence,
            trace.Head.State.Revision, trace.Head.StateHash);
    }

    /// <summary>
    /// One complete verified replay that inspects every retrieved closing root for the terminal
    /// outcome of one exact proposal identity. The result is unfiltered; the caller authorizes.
    /// </summary>
    internal RecordedWorldMaterialProposalOutcome ReadMaterialProposalOutcome(WorldDatabase database, EntityId proposalId)
    {
        ReplayedWorld trace = VerifyAndReplay(database, null, selectedProposalId: proposalId);
        return new(trace.SelectedProposalOutcome, trace.Head.State.Revision, trace.Head.StateHash);
    }

    /// <summary>
    /// The same complete verified replay, selecting the terminal outcome of one exact paid quote identity
    /// from the retrieved closing roots. The result is unfiltered; the caller authorizes.
    /// </summary>
    internal RecordedWorldPaidQuoteOutcome ReadPaidQuoteOutcome(WorldDatabase database, EntityId quoteId)
    {
        ReplayedWorld trace = VerifyAndReplay(database, null, selectedQuoteId: quoteId);
        return new(trace.SelectedQuoteOutcome, trace.Head.State.Revision, trace.Head.StateHash);
    }

    private static ReplayedWorld VerifyAndReplay(WorldDatabase database, ulong? selectedRevision, ulong? selectedOrdinal = null, ulong? selectedMaterialSequence = null, ulong? selectedMaterialCareOrdinal = null, EntityId? selectedProposalId = null, EntityId? selectedQuoteId = null)
    {
        try
        {
            return Replay(database, selectedRevision, selectedOrdinal, selectedMaterialSequence, selectedMaterialCareOrdinal, selectedProposalId, selectedQuoteId);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The local world journal violates its replay contract.", error);
        }
    }

    private static ReplayedWorld Replay(WorldDatabase database, ulong? selectedRevision, ulong? selectedOrdinal, ulong? selectedMaterialSequence, ulong? selectedMaterialCareOrdinal, EntityId? selectedProposalId, EntityId? selectedQuoteId)
    {
        byte[] genesisBytes = database.ReadGenesis();
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
        WorldGenesisAdmission.Validate(genesis);
        if (genesis.ProfileVersion is 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 19 or 20 or 21 && database.SchemaVersion != WorldDatabaseSchemaThree.Version)
        {
            throw new InvalidDataException("A production, quest or construction genesis requires its declared world schema.");
        }
        if ((genesis.ProfileVersion is 16 or 17 or 18) != (database.SchemaVersion == WorldDatabaseSchemaFour.Version))
        {
            throw new InvalidDataException("A consequence continuation genesis requires its exclusive world schema.");
        }
        if ((genesis.ProfileVersion is 22 or 23) != (database.SchemaVersion == WorldDatabaseSchemaFive.Version))
        {
            throw new InvalidDataException("An encounter pacing genesis requires its exclusive world schema.");
        }
        if ((genesis.ProfileVersion == 24) != (database.SchemaVersion == WorldDatabaseSchemaSix.Version))
        {
            throw new InvalidDataException("An encounter lifecycle genesis requires its exclusive world schema.");
        }
        if ((genesis.ProfileVersion is 25 or 26) != (database.SchemaVersion == WorldDatabaseSchemaSeven.Version))
        {
            throw new InvalidDataException("A care genesis requires its exclusive resource archive schema.");
        }
        if ((genesis.ProfileVersion is 27 or 28 or 30 or 32 or 33) != (database.SchemaVersion == WorldDatabaseSchemaEight.Version))
        {
            throw new InvalidDataException("A coordinated genesis requires its exclusive material and care archive schema.");
        }
        WorldSnapshot state = genesis;
        WorldSnapshot? selected = selectedRevision == 0 ? genesis : null;
        SelectedMaterialObservation? selectedMaterial = null;
        byte[] stateBytes = genesisBytes;
        ContentHash stateHash = ContentHash.Compute(stateBytes);
        var usage = new WorldJournalUsage(0, 0, 0, 0, 0);
        Dictionary<EntityId, StoredWorldIdentity> identities = [];
        Dictionary<(EntityId Principal, EntityId Command), StoredWorldCommandOutcome> commands = [];
        var steps = new WorldCommitPreparation();
        var migrations = new WorldProfileMigrationReplay();
        var transitions = new WorldProposalTransitionReplay();
        ulong transitionCount = 0;
        using var archives = new WorldResourceArchiveReplay(database, selectedOrdinal);
        using var materialCareArchives = new WorldMaterialCareArchiveReplay(database, selectedMaterialCareOrdinal,
            selectedProposalId, selectedQuoteId);
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
                genesis.IdentityIndex, stateHash, identities.ContainsKey, steps, migrations, transitions,
                stored.Sequence == selectedMaterialSequence);
            if (stored.Kind == WorldCommitKind.ProfileMigration && database.SchemaVersion == WorldDatabaseSchemaEight.Version)
            {
                transitionCount = checked(transitionCount + 1);
            }
            WorldJournalAdvance advance = WorldJournalPolicy.Advance(usage, stored.Sequence, stored.AdvancingSteps,
                (ulong)expected.Commands.Count, (ulong)stored.AcceptedBatch.Length, (ulong)stored.Outcomes.Length,
                state.PendingExcavation is null ? expected.Candidate.PendingExcavation : null,
                state.ProfileVersion, (ulong)(expected.ResourceArchive?.Encode().Length ?? 0),
                (ulong)(expected.MaterialCareArchive?.Encode().Length ?? 0));
            VerifyCommit(stored, expected.Stored);
            if (stored.Sequence == selectedMaterialSequence)
            {
                selectedMaterial = new(stored.BeforeHash, stored.AfterHash,
                    expected.MaterialEvidence ?? throw new InvalidDataException("The selected entry did not observe its material transaction."));
            }
            archives.Verify(expected.ResourceArchive, stored, stateBytes);
            materialCareArchives.Verify(expected.MaterialCareArchive, stored, stateBytes, state);
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
        archives.Complete();
        materialCareArchives.Complete();
        if (database.SchemaVersion == WorldDatabaseSchemaThree.Version && state.ProfileVersion is not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10 and not 11 and not 12 and not 13 and not 14 and not 15 and not 19 and not 20 and not 21)
        {
            throw new InvalidDataException("The third world schema has no accepted production, quest or construction admission.");
        }
        if ((state.ProfileVersion is 16 or 17 or 18) != (database.SchemaVersion == WorldDatabaseSchemaFour.Version))
        {
            throw new InvalidDataException("The consequence continuation world and its exclusive schema disagree.");
        }
        if ((state.ProfileVersion is 22 or 23) != (database.SchemaVersion == WorldDatabaseSchemaFive.Version))
        {
            throw new InvalidDataException("The encounter pacing world and its exclusive schema disagree.");
        }
        if ((state.ProfileVersion == 24) != (database.SchemaVersion == WorldDatabaseSchemaSix.Version))
        {
            throw new InvalidDataException("The encounter lifecycle world and its exclusive schema disagree.");
        }
        if ((state.ProfileVersion is 25 or 26) != (database.SchemaVersion == WorldDatabaseSchemaSeven.Version))
        {
            throw new InvalidDataException("The care world and its exclusive resource archive schema disagree.");
        }
        if ((state.ProfileVersion is 27 or 28 or 30 or 32 or 33) != (database.SchemaVersion == WorldDatabaseSchemaEight.Version))
        {
            throw new InvalidDataException("The coordinated world and its exclusive archive schema disagree.");
        }
        // The shared layout never converts one interpretation into another implicitly. A root keeps the
        // interpretation its genesis selected, except a lived coordinated genesis continued by exactly
        // one replayed explicit proposal transition; that commit's processor admits only 27 to 30.
        bool transitioned = genesis.ProfileVersion == WorldProposalTransition.SourceProfileVersion
            && state.ProfileVersion == WorldProposalTransition.TargetProfileVersion && transitionCount == 1;
        if (database.SchemaVersion == WorldDatabaseSchemaEight.Version
            && (state.ProfileVersion != genesis.ProfileVersion || transitionCount != 0) && !transitioned)
        {
            throw new InvalidDataException("The coordinated world departed from its genesis-selected interpretation.");
        }
        StoredWorldHead head = database.ReadHead();
        if (head.Sequence != state.Revision || head.AdvancingSteps != state.AdvancingSteps
            || !head.State.AsSpan().SequenceEqual(stateBytes))
        {
            throw new InvalidDataException("The stored world head does not match exact replay.");
        }
        VerifyCommandTable(database, commands);
        VerifyIdentityTable(database, identities);
        return new ReplayedWorld(new RecoveredWorld(genesis, state, stateHash, usage), selected,
            archives.Selected, archives.SelectedPublicationSequence, selectedMaterial,
            materialCareArchives.Selected, materialCareArchives.SelectedPublicationSequence,
            materialCareArchives.SelectedProposalOutcome, materialCareArchives.SelectedQuoteOutcome);
    }

    private static ExpectedJournalEntry PrepareEntry(int schemaVersion, StoredWorldCommit stored,
        WorldSnapshot state, WorldIdentityIndex genesisIdentities, ContentHash stateHash,
        Func<EntityId, bool> historicallyOccupied, WorldCommitPreparation steps, WorldProfileMigrationReplay migrations,
        WorldProposalTransitionReplay transitions, bool captureMaterialEvidence)
    {
        if (stored.Kind == WorldCommitKind.ProfileMigration)
        {
            if (captureMaterialEvidence)
                throw new InvalidDataException("Profile migrations have no live material observation interpretation.");
            if (schemaVersion == WorldDatabaseSchemaEight.Version)
            {
                return transitions.Prepare(stored, state, genesisIdentities, stateHash, historicallyOccupied);
            }
            if (schemaVersion != WorldDatabaseSchemaThree.Version)
            {
                throw new InvalidDataException("This schema cannot contain a profile migration.");
            }
            return migrations.Prepare(stored, state, genesisIdentities, stateHash, historicallyOccupied);
        }
        WorldStepBatch batch = WorldStepCodec.Decode(stored.AcceptedBatch);
        PreparedWorldCommit step = steps.Prepare(state, genesisIdentities,
            stateHash, batch, historicallyOccupied, captureMaterialEvidence);
        return new(step.Decision.Candidate, step.CandidateBytes, step.Stored, step.Commands, step.Identities,
            step.Decision.SealedResourceSegment, captureMaterialEvidence ? step.Decision.MaterialEvidence : null,
            step.Decision.SealedMaterialCareSegment);
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

    private sealed record ReplayedWorld(RecoveredWorld Head, WorldSnapshot? Selected,
        WorldResourceArchiveSegment? SelectedArchive, ulong ArchivePublicationSequence, SelectedMaterialObservation? SelectedMaterial,
        WorldMaterialCareArchiveSegment? SelectedMaterialCareArchive, ulong MaterialCarePublicationSequence,
        WorldMaterialProposalOutcome? SelectedProposalOutcome, WorldPaidQuoteOutcome? SelectedQuoteOutcome);

    private sealed record SelectedMaterialObservation(ContentHash BeforeHash, ContentHash AfterHash,
        IReadOnlyList<WorldMaterialActionEvidence> Evidence);
}

internal sealed record ExpectedJournalEntry(WorldSnapshot Candidate, byte[] CandidateBytes, StoredWorldCommit Stored,
    IReadOnlyList<StoredWorldCommandOutcome> Commands, IReadOnlyList<StoredWorldIdentity> Identities,
    WorldResourceArchiveSegment? ResourceArchive = null, IReadOnlyList<WorldMaterialActionEvidence>? MaterialEvidence = null,
    WorldMaterialCareArchiveSegment? MaterialCareArchive = null);

internal sealed record RecoveredWorld(WorldSnapshot Genesis, WorldSnapshot State, ContentHash StateHash, WorldJournalUsage Usage);

/// <summary>An archived terminal outcome, or none, bound to the source head its replay verified.</summary>
internal sealed record RecordedWorldMaterialProposalOutcome(WorldMaterialProposalOutcome? Outcome,
    ulong SourceHeadRevision, ContentHash SourceHeadHash);

/// <summary>An archived terminal paid-quote outcome, or none, bound to the source head its replay verified.</summary>
internal sealed record RecordedWorldPaidQuoteOutcome(WorldPaidQuoteOutcome? Outcome,
    ulong SourceHeadRevision, ContentHash SourceHeadHash);

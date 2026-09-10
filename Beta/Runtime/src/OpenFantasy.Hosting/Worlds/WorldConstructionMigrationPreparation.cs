using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact rows for one explicit construction and finite tool admission, preserving the existing root and clocks.</summary>
internal sealed class WorldConstructionMigrationPreparation
{
    private readonly WorldConstructionMigrationProcessor _processor = new();

    internal PreparedWorldConstructionMigration Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldConstructionMigration reconstruction, Func<EntityId, bool> historicallyOccupied)
    {
        if (reconstruction.ExpectedHeadHash != beforeHash)
        {
            throw new InvalidOperationException("The construction migration names a different retained head.");
        }
        WorldConstructionMigrationDecision decision = _processor.Decide(state, reconstruction,
            identity => genesisIdentities.Contains(identity) || historicallyOccupied(identity));
        byte[] candidate = WorldSnapshotCodec.Encode(decision.Candidate);
        byte[] reconstructionBytes = WorldConstructionMigrationCodecV1.Encode(reconstruction);
        byte[] outcome = WorldConstructionMigrationCodecV1.EncodeOutcome(decision);
        WorldStorageBinary.RequireBytes(reconstructionBytes, WorldDatabase.MaximumBatchByteLength, nameof(reconstruction));
        WorldStorageBinary.RequireBytes(outcome, WorldDatabase.MaximumOutcomesByteLength, nameof(outcome));
        ulong sequence = decision.Candidate.Revision;
        if (sequence != checked(state.Revision + 1) || decision.Candidate.AdvancingSteps != state.AdvancingSteps
            || decision.Candidate.ProfileVersion != 5 || decision.CauseId != reconstruction.CauseId)
        {
            throw new InvalidDataException("The construction migration changed an unrelated clock or profile.");
        }
        List<StoredWorldIdentity> identities = [new(reconstruction.CauseId, sequence, WorldIdentityKind.CommitCause)];
        identities.AddRange(decision.AdmittedDefinitionIds.Select(identity =>
            new StoredWorldIdentity(identity, sequence, WorldIdentityKind.AdmittedDefinition)));
        identities.AddRange(decision.AdmittedEntityIds.Select(identity =>
            new StoredWorldIdentity(identity, sequence, WorldIdentityKind.SpawnedEntity)));
        return new PreparedWorldConstructionMigration(decision, candidate,
            new StoredWorldCommit(sequence, WorldCommitKind.ProfileMigration, reconstruction.CauseId,
                state.AdvancingSteps, reconstructionBytes, outcome, beforeHash, ContentHash.Compute(candidate)),
            identities.AsReadOnly());
    }
}

internal sealed record PreparedWorldConstructionMigration(WorldConstructionMigrationDecision Decision, byte[] CandidateBytes,
    StoredWorldCommit Stored, IReadOnlyList<StoredWorldIdentity> Identities);

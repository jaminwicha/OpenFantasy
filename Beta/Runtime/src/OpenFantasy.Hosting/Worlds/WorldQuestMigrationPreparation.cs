using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Exact rows for one explicit funded quest admission, preserving the existing root and clocks.</summary>
internal sealed class WorldQuestMigrationPreparation
{
    private readonly WorldQuestMigrationProcessor _processor = new();

    internal PreparedWorldQuestMigration Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldQuestMigration request, Func<EntityId, bool> historicallyOccupied)
    {
        if (request.ExpectedHeadHash != beforeHash)
        {
            throw new InvalidOperationException("The quest migration names a different retained head.");
        }
        WorldQuestMigrationDecision decision = _processor.Decide(state, request,
            identity => genesisIdentities.Contains(identity) || historicallyOccupied(identity));
        byte[] candidate = WorldSnapshotCodec.Encode(decision.Candidate);
        byte[] requestBytes = WorldQuestMigrationCodecV1.Encode(request);
        byte[] outcome = WorldQuestMigrationCodecV1.EncodeOutcome(decision);
        WorldStorageBinary.RequireBytes(requestBytes, WorldDatabase.MaximumBatchByteLength, nameof(request));
        WorldStorageBinary.RequireBytes(outcome, WorldDatabase.MaximumOutcomesByteLength, nameof(outcome));
        ulong sequence = decision.Candidate.Revision;
        if (sequence != checked(state.Revision + 1) || decision.Candidate.AdvancingSteps != state.AdvancingSteps
            || decision.Candidate.ProfileVersion != 4 || decision.CauseId != request.CauseId)
        {
            throw new InvalidDataException("The quest migration changed an unrelated clock or profile.");
        }
        List<StoredWorldIdentity> identities = [new(request.CauseId, sequence, WorldIdentityKind.CommitCause)];
        identities.AddRange(decision.AdmittedDefinitionIds.Select(identity =>
            new StoredWorldIdentity(identity, sequence, WorldIdentityKind.AdmittedDefinition)));
        identities.AddRange(decision.AdmittedEntityIds.Select(identity =>
            new StoredWorldIdentity(identity, sequence, WorldIdentityKind.SpawnedEntity)));
        return new PreparedWorldQuestMigration(decision, candidate,
            new StoredWorldCommit(sequence, WorldCommitKind.ProfileMigration, request.CauseId,
                state.AdvancingSteps, requestBytes, outcome, beforeHash, ContentHash.Compute(candidate)),
            identities.AsReadOnly());
    }
}

internal sealed record PreparedWorldQuestMigration(WorldQuestMigrationDecision Decision, byte[] CandidateBytes,
    StoredWorldCommit Stored, IReadOnlyList<StoredWorldIdentity> Identities);

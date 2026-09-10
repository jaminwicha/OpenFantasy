using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Canonical storage rows for the single supported production admission transition.</summary>
internal sealed class WorldMigrationPreparation
{
    private readonly WorldProductionMigrationProcessor _processor = new();

    internal PreparedWorldMigration Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldProductionMigration request, Func<EntityId, bool> historicallyOccupied)
    {
        if (request.ExpectedHeadHash != beforeHash)
        {
            throw new InvalidOperationException("The production migration names a different retained head.");
        }
        WorldProductionMigrationDecision decision = _processor.Decide(state, request,
            identity => genesisIdentities.Contains(identity) || historicallyOccupied(identity));
        byte[] candidate = WorldSnapshotCodec.Encode(decision.Candidate);
        byte[] requestBytes = WorldProductionMigrationCodecV1.Encode(request);
        byte[] outcome = WorldProductionMigrationCodecV1.EncodeOutcome(decision);
        ulong sequence = decision.Candidate.Revision;
        if (sequence != checked(state.Revision + 1) || decision.Candidate.AdvancingSteps != state.AdvancingSteps
            || decision.Candidate.ProfileVersion != 3 || decision.CauseId != request.CauseId)
        {
            throw new InvalidDataException("The production migration changed an unrelated clock or profile.");
        }
        List<StoredWorldIdentity> identities = [new(request.CauseId, sequence, WorldIdentityKind.CommitCause)];
        identities.AddRange(decision.AdmittedDefinitionIds.Select(identity =>
            new StoredWorldIdentity(identity, sequence, WorldIdentityKind.AdmittedDefinition)));
        return new PreparedWorldMigration(decision, candidate,
            new StoredWorldCommit(sequence, WorldCommitKind.ProductionMigration, request.CauseId,
                state.AdvancingSteps, requestBytes, outcome, beforeHash, ContentHash.Compute(candidate)),
            identities.AsReadOnly());
    }
}

internal sealed record PreparedWorldMigration(WorldProductionMigrationDecision Decision, byte[] CandidateBytes,
    StoredWorldCommit Stored, IReadOnlyList<StoredWorldIdentity> Identities);

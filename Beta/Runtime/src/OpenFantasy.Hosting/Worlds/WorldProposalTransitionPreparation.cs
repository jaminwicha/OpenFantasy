using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Exact rows for one explicit proposal transition: the typed migration commit, the joint archive that
/// closes the lived source head, the cause identity and the saved-proposal head. Clocks stay exact.
/// </summary>
internal sealed class WorldProposalTransitionPreparation
{
    private readonly WorldProposalTransitionProcessor _processor = new();

    internal PreparedWorldProposalTransition Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldProposalTransition request, Func<EntityId, bool> historicallyOccupied)
    {
        if (request.ExpectedHeadHash != beforeHash)
        {
            throw new InvalidOperationException("The proposal transition names a different retained head.");
        }
        WorldProposalTransitionDecision decision = _processor.Decide(state, request,
            identity => genesisIdentities.Contains(identity) || historicallyOccupied(identity));
        byte[] candidate = WorldSnapshotCodec.Encode(decision.Candidate);
        byte[] requestBytes = WorldProposalTransitionCodecV1.Encode(request);
        byte[] outcome = WorldProposalTransitionCodecV1.EncodeOutcome(decision);
        WorldStorageBinary.RequireBytes(requestBytes, WorldDatabase.MaximumBatchByteLength, nameof(request));
        WorldStorageBinary.RequireBytes(outcome, WorldDatabase.MaximumOutcomesByteLength, nameof(outcome));
        ulong sequence = decision.Candidate.Revision;
        byte[] closing = decision.Archive.Encode();
        if (sequence != checked(state.Revision + 1) || decision.Candidate.AdvancingSteps != state.AdvancingSteps
            || decision.Candidate.Calendar != state.Calendar
            || decision.Candidate.ProfileVersion != WorldProposalTransition.TargetProfileVersion
            || decision.CauseId != request.CauseId || decision.Archive.Reference.BoundaryHash != beforeHash
            || ContentHash.Compute(closing) != beforeHash)
        {
            throw new InvalidDataException("The proposal transition changed an unrelated clock, root or profile.");
        }
        return new PreparedWorldProposalTransition(decision, candidate,
            new StoredWorldCommit(sequence, WorldCommitKind.ProfileMigration, request.CauseId,
                state.AdvancingSteps, requestBytes, outcome, beforeHash, ContentHash.Compute(candidate)),
            [new StoredWorldIdentity(request.CauseId, sequence, WorldIdentityKind.CommitCause)],
            new StoredWorldMaterialCareArchive(decision.Archive.Reference, sequence, closing));
    }
}

internal sealed record PreparedWorldProposalTransition(WorldProposalTransitionDecision Decision, byte[] CandidateBytes,
    StoredWorldCommit Stored, IReadOnlyList<StoredWorldIdentity> Identities, StoredWorldMaterialCareArchive Archive);

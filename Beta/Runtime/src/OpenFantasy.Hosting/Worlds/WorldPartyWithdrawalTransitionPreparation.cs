using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Prepares one interpretation commit without a new archive, actor receipt or resource baseline.</summary>
internal sealed class WorldPartyWithdrawalTransitionPreparation
{
    private readonly WorldPartyWithdrawalTransitionProcessor _processor = new();

    internal ExpectedJournalEntry Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldPartyWithdrawalTransition request, Func<EntityId, bool> historicallyOccupied)
    {
        if (request.ExpectedHeadHash != beforeHash)
        {
            throw new InvalidOperationException("The withdrawal transition names another retained head.");
        }
        WorldPartyWithdrawalTransitionDecision decision = _processor.Decide(state, request,
            identity => genesisIdentities.Contains(identity) || historicallyOccupied(identity));
        byte[] candidate = WorldSnapshotCodec.Encode(decision.Candidate);
        byte[] requestBytes = WorldPartyWithdrawalTransitionCodecV1.Encode(request);
        byte[] outcome = WorldPartyWithdrawalTransitionCodecV1.EncodeOutcome(decision);
        WorldStorageBinary.RequireBytes(requestBytes, WorldDatabase.MaximumBatchByteLength, nameof(request));
        WorldStorageBinary.RequireBytes(outcome, WorldDatabase.MaximumOutcomesByteLength, nameof(outcome));
        ulong sequence = decision.Candidate.Revision;
        return new(decision.Candidate, candidate,
            new StoredWorldCommit(sequence, WorldCommitKind.ProfileMigration, request.CauseId,
                state.AdvancingSteps, requestBytes, outcome, beforeHash, ContentHash.Compute(candidate)),
            [], [new StoredWorldIdentity(request.CauseId, sequence, WorldIdentityKind.CommitCause)]);
    }

    internal ExpectedJournalEntry Replay(StoredWorldCommit stored, WorldSnapshot state,
        WorldIdentityIndex genesisIdentities, ContentHash beforeHash, Func<EntityId, bool> historicallyOccupied) =>
        Prepare(state, genesisIdentities, beforeHash,
            WorldPartyWithdrawalTransitionCodecV1.Decode(stored.AcceptedBatch), historicallyOccupied);
}

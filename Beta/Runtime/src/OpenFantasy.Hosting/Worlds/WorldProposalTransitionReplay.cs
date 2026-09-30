using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Closed schema-eight migration dispatch: the only typed transition this layout admits is the
/// proposal transition. Its strict decoder refuses every other tag, version or interpretation pair.
/// </summary>
internal sealed class WorldProposalTransitionReplay
{
    private readonly WorldProposalTransitionPreparation _preparation = new();

    internal ExpectedJournalEntry Prepare(StoredWorldCommit stored, WorldSnapshot state,
        WorldIdentityIndex genesisIdentities, ContentHash stateHash, Func<EntityId, bool> historicallyOccupied)
    {
        WorldProposalTransition request = WorldProposalTransitionCodecV1.Decode(stored.AcceptedBatch);
        PreparedWorldProposalTransition expected = _preparation.Prepare(state, genesisIdentities, stateHash,
            request, historicallyOccupied);
        return new(expected.Decision.Candidate, expected.CandidateBytes, expected.Stored, [], expected.Identities,
            MaterialCareArchive: expected.Decision.Archive);
    }
}

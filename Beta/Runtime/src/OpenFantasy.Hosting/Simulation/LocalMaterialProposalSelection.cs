using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Admits local offer input and protects a valid bilateral choice from competing NPC work.</summary>
internal sealed class LocalMaterialProposalSelection
{
    internal void Validate(WorldSnapshot state, WorldActionCommand command)
    {
        if (command.Intent is not MaterialProposalIntent)
        {
            return;
        }
        if (state.ProfileVersion != 30 || state.HasReadingHold || state.PendingExcavation is not null
            || state.Care?.Pending is not null || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration)
        {
            throw new AuthorityDispatchException("Saved material proposals require their admitted unheld exploration boundary.");
        }
    }

    internal EntityId? FindReservedSender(SqliteWorldAuthority authority, WorldSnapshot state,
        EntityId causeId, WorldActionCommand? command)
    {
        if (command?.Intent is not AcceptMaterialProposalIntent accept
            || state.Coordination?.Proposals?.FindOpen(accept.ProposalId) is not { } proposal
            || proposal.Terms.RecipientActorId != command.ActorId)
        {
            return null;
        }
        var single = new WorldStepBatch(causeId, state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], state.ProfileVersion);
        try
        {
            // This is advice from complete authority evaluation, not a grant or a publication.
            // A stale, foreign or otherwise invalid request cannot suppress the sender's own work.
            WorldStepDecision preview = authority.Preview(single);
            return preview.Receipts.Single().Rejection == WorldActionRejection.None
                ? proposal.Terms.SenderActorId : null;
        }
        catch (AuthorityDispatchException)
        {
            return null;
        }
    }
}

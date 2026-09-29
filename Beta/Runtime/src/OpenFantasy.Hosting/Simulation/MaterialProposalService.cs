using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public enum MaterialProposalStatus { Open = 1, Accepted = 2, Declined = 3, Withdrawn = 4, Expired = 5 }

/// <summary>
/// What one participant may know about one proposal: its identity, exact terms, expiry and state.
/// It carries no world root, no other participant's command stream and no unrelated inventory.
/// </summary>
public sealed record MaterialProposalView(EntityId ProposalId, ConsentedMaterialTerms Terms,
    CommandContentHash GrantHash, ulong ExpiresAtAdvancingStep, MaterialProposalStatus Status,
    ulong? ClosedAtSequence, bool ViewerIsSender, ulong? SenderPresenceRevision)
{
    /// <summary>The recipient's own approval of exactly this inspected offer.</summary>
    public AcceptMaterialProposalIntent PrepareAcceptance() => new(ProposalId, GrantHash, Terms,
        SenderPresenceRevision ?? throw new InvalidOperationException("The sender is not present to be observed."));
}

/// <summary>
/// The local application boundary of saved material proposals. Every submission and every query
/// brings its own delivery; the durable owner authorizes it, selects and publishes under one lock.
/// </summary>
public sealed class MaterialProposalService
{
    private readonly SqliteWorldAuthority _authority;
    private readonly Func<EntityId> _nextCause;

    public MaterialProposalService(SqliteWorldAuthority authority, Func<EntityId> nextCause)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(nextCause);
        _authority = authority;
        _nextCause = nextCause;
    }

    /// <summary>Creates, accepts, declines or withdraws; an exact retry returns its original receipt.</summary>
    public WorldActionReceipt Submit(WorldActionCommand command, TrustedCommandDelivery delivery) =>
        _authority.SubmitMaterialProposalCommand(command, delivery, _nextCause());

    /// <summary>The caller's own receipt, after current authorization of the supplied delivery.</summary>
    public WorldActionReceipt? FindReceipt(WorldActionCommand command, TrustedCommandDelivery delivery) =>
        _authority.FindReceipt(command, delivery);

    /// <summary>One proposal the controlled actor takes part in; otherwise nothing, whatever exists.</summary>
    public MaterialProposalView? Find(EntityId proposalId, EntityId actorId, TrustedCommandDelivery delivery) =>
        _authority.ReadForController(actorId, delivery, state =>
            Project(state, actorId).FirstOrDefault(view => view.ProposalId == proposalId));

    /// <summary>Every open proposal and current-segment outcome the controlled actor takes part in.</summary>
    public IReadOnlyList<MaterialProposalView> List(EntityId actorId, TrustedCommandDelivery delivery) =>
        _authority.ReadForController(actorId, delivery, state => Project(state, actorId).ToArray());

    /// <summary>
    /// One proposal by exact identity, including its terminal outcome after a coordinated cut
    /// archived it. A missing identity and another pair's proposal both give nothing. An archived
    /// outcome costs one complete verified journal replay: synchronous non-frame work.
    /// </summary>
    public MaterialProposalView? FindRecorded(EntityId proposalId, EntityId actorId, TrustedCommandDelivery delivery) =>
        _authority.ReadRecordedProposalForController(proposalId, actorId, delivery, (state, archived) =>
            ProjectRecorded(state, archived, proposalId, actorId));

    internal static MaterialProposalView? ProjectRecorded(WorldSnapshot state,
        WorldMaterialProposalOutcome? archived, EntityId proposalId, EntityId actorId) =>
        Project(state, actorId).FirstOrDefault(view => view.ProposalId == proposalId)
        ?? (archived is null ? null : Closed(state, archived, actorId));

    internal static IEnumerable<MaterialProposalView> Project(WorldSnapshot state, EntityId actorId)
    {
        if (state.Coordination?.Proposals is not { } book)
        {
            yield break;
        }
        foreach (WorldMaterialProposal open in book.Open.Where(value => value.HasParticipant(actorId)))
        {
            // An offer that is due is already unacceptable, before the next exploration step closes it.
            yield return View(state, open, open.IsDue(state.AdvancingSteps) ? MaterialProposalStatus.Expired
                : MaterialProposalStatus.Open, null, actorId);
        }
        foreach (WorldMaterialProposalOutcome outcome in book.Outcomes.Where(value => value.Proposal.HasParticipant(actorId)))
        {
            yield return Closed(state, outcome, actorId);
        }
    }

    private static MaterialProposalView Closed(WorldSnapshot state, WorldMaterialProposalOutcome outcome,
        EntityId actorId) => View(state, outcome.Proposal, outcome.Kind switch
        {
            WorldMaterialProposalOutcomeKind.Accepted => MaterialProposalStatus.Accepted,
            WorldMaterialProposalOutcomeKind.Declined => MaterialProposalStatus.Declined,
            WorldMaterialProposalOutcomeKind.Withdrawn => MaterialProposalStatus.Withdrawn,
            _ => MaterialProposalStatus.Expired
        }, outcome.WorldSequence, actorId);

    private static MaterialProposalView View(WorldSnapshot state, WorldMaterialProposal proposal,
        MaterialProposalStatus status, ulong? closedAt, EntityId actorId) => new(proposal.ProposalId, proposal.Terms,
        proposal.GrantHash, proposal.ExpiresAtAdvancingStep, status, closedAt,
        proposal.Terms.SenderActorId == actorId,
        state.Space.Actors.FirstOrDefault(value => value.ActorId == proposal.Terms.SenderActorId)?.Revision);
}

using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// Decides one raw counted-lot transfer that both current participants approve exactly. The owner of
/// the receiving storage supplies an authenticated acceptance of this transaction; access policies
/// stay as admitted and ordinary unilateral transfer keeps its rules. The authority performs no IO,
/// stores no offer, proves no spatial reach and advances no clock.
/// </summary>
public sealed class ConsentedTransferAuthority
{
    private readonly CustodyTransferPlanner _planner = new();

    /// <param name="historicallyOccupied">
    /// Complete-history identity occupancy for this world, including retired lots and every earlier
    /// command, acceptance and spawned identity. Current state alone cannot establish freshness.
    /// </param>
    public ConsentedTransferDecision Decide(CountedAuthorityState state, DeliveredTransferCommand sender,
        DeliveredTransferAcceptance recipient, CommandCommitIdentity nextCommit,
        Func<EntityId, bool> historicallyOccupied)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(nextCommit);
        ArgumentNullException.ThrowIfNull(historicallyOccupied);
        ConsentedTransferCommand command = sender.Command;
        ConsentedTransferAcceptance acceptance = recipient.Acceptance;
        if (command.Terms.WorldId != state.WorldId || nextCommit.WorldId != state.WorldId)
        {
            throw new ArgumentException("Dispatch must select this world's authority and commit sequence.");
        }

        if (!IsApprovedByBothParticipants(state, sender, recipient))
        {
            return Reject(state, command, acceptance, nextCommit, CommandRejectionReason.NotAuthorized);
        }

        if (command.CommandId == acceptance.AcceptanceId
            || IsOccupied(state, historicallyOccupied, command.CommandId)
            || IsOccupied(state, historicallyOccupied, acceptance.AcceptanceId))
        {
            return Reject(state, command, acceptance, nextCommit, CommandRejectionReason.ResourceUnavailable);
        }

        try
        {
            ConsentedTransferTerms terms = command.Terms;
            if (state.Custody.Lots.Count == CustodySnapshot.MaximumLots
                && terms.Quantity < state.Custody.GetLot(terms.LotId).Quantity)
            {
                return Reject(state, command, acceptance, nextCommit, CommandRejectionReason.ResourceUnavailable);
            }

            CustodyTransferPlan plan = _planner.Plan(state.Custody, command.ToMovementRequest());
            bool spawnsChild = plan.MovedLotId != terms.LotId;
            if (spawnsChild && (plan.MovedLotId == acceptance.AcceptanceId
                || IsOccupied(state, historicallyOccupied, plan.MovedLotId)))
            {
                return Reject(state, command, acceptance, nextCommit, CommandRejectionReason.ResourceUnavailable);
            }

            CountedAuthorityState candidate = state.WithCustody(plan.Candidate);
            return new ConsentedTransferDecision(state, candidate,
                ConsentedTransferReceipt.CreateApplied(command, acceptance, nextCommit, plan.MovedLotId));
        }
        catch (CustodyConflictException)
        {
            return Reject(state, command, acceptance, nextCommit, CommandRejectionReason.ResourceUnavailable);
        }
        catch (OverflowException)
        {
            return Reject(state, command, acceptance, nextCommit, CommandRejectionReason.ResourceUnavailable);
        }
    }

    private static bool IsApprovedByBothParticipants(CountedAuthorityState state, DeliveredTransferCommand sender,
        DeliveredTransferAcceptance recipient)
    {
        ConsentedTransferCommand command = sender.Command;
        ConsentedTransferAcceptance acceptance = recipient.Acceptance;
        ConsentedTransferTerms terms = command.Terms;
        return terms.BranchId == state.BranchId
            && acceptance.Binds(command)
            && IsCurrentController(state, sender.Delivery, command.PrincipalId, command.OriginSessionId,
                terms.SenderActorId)
            && IsCurrentController(state, recipient.Delivery, acceptance.PrincipalId, acceptance.OriginSessionId,
                terms.RecipientActorId)
            && IsOwner(state, terms.SenderActorId, terms.SourceCustodianId)
            && IsOwner(state, terms.RecipientActorId, terms.DestinationCustodianId);
    }

    // Submitted claims must equal the host-verified delivery; neither is derived from the other.
    private static bool IsCurrentController(CountedAuthorityState state, TrustedCommandDelivery delivery,
        EntityId claimedPrincipalId, EntityId claimedSessionId, EntityId actorId) =>
        claimedPrincipalId == delivery.PrincipalId && claimedSessionId == delivery.SessionId
        && state.CanControl(delivery, actorId);

    private static bool IsOwner(CountedAuthorityState state, EntityId actorId, EntityId custodianId) =>
        state.Policies.Any(policy => policy.CustodianId == custodianId && policy.OwnerActorId == actorId);

    private static bool IsOccupied(CountedAuthorityState state, Func<EntityId, bool> historicallyOccupied,
        EntityId identity) => state.ContainsIdentity(identity) || historicallyOccupied(identity);

    private static ConsentedTransferDecision Reject(CountedAuthorityState state, ConsentedTransferCommand command,
        ConsentedTransferAcceptance acceptance, CommandCommitIdentity commit, CommandRejectionReason reason) =>
        new(state, state, ConsentedTransferReceipt.CreateRejected(command, acceptance, commit, reason));
}

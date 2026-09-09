using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority;

/// <summary>Fresh command admission and actual counted transfer decisions without IO or retry storage.</summary>
public sealed class CountedTransferAuthority
{
    private const int RequiredRevisionCount = 4;
    private readonly CustodyTransferPlanner _planner = new();

    public AuthorityDecision Decide(CountedAuthorityState state, CommandEnvelope command,
        TrustedCommandDelivery delivery, CommandCommitIdentity nextCommit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(nextCommit);
        if (command.WorldId != state.WorldId || nextCommit.WorldId != state.WorldId)
        {
            throw new ArgumentException("Dispatch must select this world's authority and commit sequence.");
        }

        TransferLotIntent intent = command.Payload;
        if (command.BranchId != state.BranchId || command.PrincipalId != delivery.PrincipalId
            || command.OriginSessionId != delivery.SessionId
            || !state.CanControl(delivery, command.ControlledActorId)
            || !state.AllowsTransfer(command.ControlledActorId, intent.SourceCustodianId, intent.DestinationCustodianId))
        {
            return Reject(state, command, nextCommit, CommandRejectionReason.NotAuthorized);
        }

        // Exact target admission prevents an extra expected revision from probing unrelated objects.
        if (!HasOnlyRequiredRevisionTargets(command) || state.ContainsIdentity(command.CommandId))
        {
            return Reject(state, command, nextCommit, CommandRejectionReason.ResourceUnavailable);
        }

        try
        {
            if (state.Custody.Lots.Count == CustodySnapshot.MaximumLots
                && intent.Quantity < state.Custody.GetLot(intent.LotId).Quantity)
            {
                return Reject(state, command, nextCommit, CommandRejectionReason.ResourceUnavailable);
            }

            CustodyTransferPlan plan = _planner.Plan(state.Custody, command);
            if (plan.MovedLotId != intent.LotId && state.ContainsIdentity(plan.MovedLotId))
            {
                return Reject(state, command, nextCommit, CommandRejectionReason.ResourceUnavailable);
            }

            CountedAuthorityState candidate = state.WithCustody(plan.Candidate);
            return new AuthorityDecision(state, candidate, CommandReceipt.CreateApplied(command, nextCommit), plan.MovedLotId);
        }
        catch (CustodyConflictException)
        {
            return Reject(state, command, nextCommit, CommandRejectionReason.ResourceUnavailable);
        }
        catch (OverflowException)
        {
            return Reject(state, command, nextCommit, CommandRejectionReason.ResourceUnavailable);
        }
    }

    private static bool HasOnlyRequiredRevisionTargets(CommandEnvelope command)
    {
        if (command.ExpectedRevisions.Count != RequiredRevisionCount)
        {
            return false;
        }

        TransferLotIntent intent = command.Payload;
        return command.ExpectedRevisions.All(revision => revision.TargetKind switch
        {
            RevisionTargetKind.Branch => revision.TargetId == command.BranchId,
            RevisionTargetKind.Entity => revision.TargetId == intent.LotId
                || revision.TargetId == intent.SourceCustodianId || revision.TargetId == intent.DestinationCustodianId,
            _ => false
        });
    }

    private static AuthorityDecision Reject(CountedAuthorityState state, CommandEnvelope command,
        CommandCommitIdentity commit, CommandRejectionReason reason) =>
        new(state, state, CommandReceipt.CreateRejected(command, commit, reason), null);
}

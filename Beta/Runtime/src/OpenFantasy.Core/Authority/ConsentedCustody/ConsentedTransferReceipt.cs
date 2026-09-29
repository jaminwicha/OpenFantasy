using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// The immutable result of one decision, tied to both submitted approvals by identity and content
/// hash. Constructing or decoding it commits nothing; only a durable caller may publish it.
/// World and branch are those named by the sender command.
/// </summary>
public sealed class ConsentedTransferReceipt
{
    public const ulong CurrentVersion = 1;

    internal ConsentedTransferReceipt(EntityId worldId, EntityId branchId, CommandCommitIdentity commit,
        CommandReceiptStatus status, CommandRejectionReason rejectionReason, EntityId commandId,
        EntityId senderPrincipalId, CommandContentHash commandHash, EntityId acceptanceId,
        EntityId recipientPrincipalId, CommandContentHash acceptanceHash, EntityId? movedLotId)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(commandHash);
        ArgumentNullException.ThrowIfNull(acceptanceHash);
        if (commit.WorldId != worldId)
        {
            throw new ArgumentException("The receipt and commit must belong to the same world.", nameof(commit));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool applied = status == CommandReceiptStatus.Applied;
        bool supportedReason = rejectionReason is CommandRejectionReason.None
            or CommandRejectionReason.NotAuthorized or CommandRejectionReason.ResourceUnavailable;
        if (!supportedReason || applied != (rejectionReason == CommandRejectionReason.None))
        {
            throw new ArgumentException("Applied results have no rejection; rejected results require a reason.",
                nameof(rejectionReason));
        }

        if (applied != movedLotId.HasValue)
        {
            throw new ArgumentException("Exactly the applied results identify the lot that changed custody.",
                nameof(movedLotId));
        }

        WorldId = worldId;
        BranchId = branchId;
        Commit = commit;
        Status = status;
        RejectionReason = rejectionReason;
        CommandId = commandId;
        SenderPrincipalId = senderPrincipalId;
        CommandHash = commandHash;
        AcceptanceId = acceptanceId;
        RecipientPrincipalId = recipientPrincipalId;
        AcceptanceHash = acceptanceHash;
        MovedLotId = movedLotId;
    }

    public ulong Version => CurrentVersion;
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public CommandCommitIdentity Commit { get; }
    public CommandReceiptStatus Status { get; }
    public CommandRejectionReason RejectionReason { get; }
    public EntityId CommandId { get; }
    public EntityId SenderPrincipalId { get; }
    public CommandContentHash CommandHash { get; }
    public EntityId AcceptanceId { get; }
    public EntityId RecipientPrincipalId { get; }
    public CommandContentHash AcceptanceHash { get; }

    /// <summary>The whole original lot, or the child spawned for a partial amount; absent when rejected.</summary>
    public EntityId? MovedLotId { get; }

    /// <summary>Matches claims and checks result plausibility; it does not establish an actual committed outcome.</summary>
    public bool Records(ConsentedTransferCommand command, ConsentedTransferAcceptance acceptance)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acceptance);
        bool matchesClaims = WorldId == command.Terms.WorldId && BranchId == command.Terms.BranchId
            && SenderPrincipalId == command.PrincipalId && RecipientPrincipalId == acceptance.PrincipalId
            && CommandId == command.CommandId && AcceptanceId == acceptance.AcceptanceId
            && CommandHash == command.ComputeContentHash() && AcceptanceHash == acceptance.ComputeContentHash();
        if (!matchesClaims)
        {
            return false;
        }

        if (Status == CommandReceiptStatus.Rejected)
        {
            return true;
        }

        // Without the original state this can establish only the possible whole/split identities.
        return acceptance.Binds(command) && (MovedLotId == command.Terms.LotId
            || MovedLotId == EntityId.DeriveSpawned(command.CommandId, CustodyTransferPlanner.SplitChildOrdinal));
    }

    internal static ConsentedTransferReceipt CreateApplied(ConsentedTransferCommand command,
        ConsentedTransferAcceptance acceptance, CommandCommitIdentity commit, EntityId movedLotId) =>
        Create(command, acceptance, commit, CommandReceiptStatus.Applied, CommandRejectionReason.None, movedLotId);

    internal static ConsentedTransferReceipt CreateRejected(ConsentedTransferCommand command,
        ConsentedTransferAcceptance acceptance, CommandCommitIdentity commit, CommandRejectionReason reason) =>
        Create(command, acceptance, commit, CommandReceiptStatus.Rejected, reason, null);

    private static ConsentedTransferReceipt Create(ConsentedTransferCommand command,
        ConsentedTransferAcceptance acceptance, CommandCommitIdentity commit, CommandReceiptStatus status,
        CommandRejectionReason reason, EntityId? movedLotId) => new(command.Terms.WorldId, command.Terms.BranchId,
        commit, status, reason, command.CommandId, command.PrincipalId, command.ComputeContentHash(),
        acceptance.AcceptanceId, acceptance.PrincipalId, acceptance.ComputeContentHash(), movedLotId);
}

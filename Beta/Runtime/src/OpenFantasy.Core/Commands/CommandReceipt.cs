using System.Security.Cryptography;

namespace OpenFantasy.Core.Commands;

public enum CommandReceiptStatus
{
    Applied = 1,
    Rejected = 2
}

public enum CommandRejectionReason
{
    None = 0,
    RevisionConflict = 1,
    NotAuthorized = 2,
    ResourceUnavailable = 3,
    InvalidAction = 4
}

public enum CommandRetryComparison
{
    SameContent,
    ConflictingContent,
    DifferentCommand
}

/// <summary>
/// The immutable result recorded with one authority commit. Constructing or decoding this value
/// does not commit anything. The host may publish it only after its transaction is durable.
/// </summary>
public sealed class CommandReceipt
{
    public const ulong CurrentVersion = 1;

    internal CommandReceipt(EntityId commandId, EntityId principalId, EntityId worldId, EntityId branchId,
        CommandContentHash contentHash, CommandCommitIdentity commit,
        CommandReceiptStatus status, CommandRejectionReason rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        ArgumentNullException.ThrowIfNull(commit);
        if (commit.WorldId != worldId)
        {
            throw new ArgumentException("The receipt and commit must belong to the same world.", nameof(commit));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (!Enum.IsDefined(rejectionReason) ||
            (status == CommandReceiptStatus.Applied) != (rejectionReason == CommandRejectionReason.None))
        {
            throw new ArgumentException("Applied results have no rejection; rejected results require a reason.",
                nameof(rejectionReason));
        }

        CommandId = commandId;
        PrincipalId = principalId;
        WorldId = worldId;
        BranchId = branchId;
        ContentHash = contentHash;
        Commit = commit;
        Status = status;
        RejectionReason = rejectionReason;
    }

    public ulong Version => CurrentVersion;
    public EntityId CommandId { get; }
    public EntityId PrincipalId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public CommandContentHash ContentHash { get; }
    public CommandCommitIdentity Commit { get; }
    public CommandReceiptStatus Status { get; }
    public CommandRejectionReason RejectionReason { get; }

    /// <summary>First sixteen SHA-256 bytes of the canonical receipt; collision handling belongs to the host.</summary>
    public EntityId ResultId => EntityId.FromBytes(SHA256.HashData(CommandReceiptCodecV1.Encode(this))
        .AsSpan(0, EntityId.ByteLength));

    public static CommandReceipt CreateApplied(CommandEnvelope command, CommandCommitIdentity commit)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandReceipt(command.CommandId, command.PrincipalId, command.WorldId, command.BranchId,
            command.ComputeContentHash(), commit, CommandReceiptStatus.Applied, CommandRejectionReason.None);
    }

    public static CommandReceipt CreateRejected(CommandEnvelope command, CommandCommitIdentity commit,
        CommandRejectionReason reason)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandReceipt(command.CommandId, command.PrincipalId, command.WorldId, command.BranchId,
            command.ComputeContentHash(), commit, CommandReceiptStatus.Rejected, reason);
    }

    /// <summary>Compares supplied data only. It neither locates a receipt nor authorizes access to it.</summary>
    public CommandRetryComparison CompareRetry(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.WorldId != WorldId || command.PrincipalId != PrincipalId || command.CommandId != CommandId)
        {
            return CommandRetryComparison.DifferentCommand;
        }

        return command.BranchId == BranchId && command.ComputeContentHash() == ContentHash
            ? CommandRetryComparison.SameContent
            : CommandRetryComparison.ConflictingContent;
    }
}

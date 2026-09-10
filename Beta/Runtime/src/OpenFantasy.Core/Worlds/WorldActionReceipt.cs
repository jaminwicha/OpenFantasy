using System.Security.Cryptography;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

public enum WorldActionRejection
{
    None = 0,
    NotAuthorized = 1,
    Unavailable = 2,
    RevisionConflict = 3,
    OutOfReach = 4,
    UnableToCommunicate = 5
}

/// <summary>The committed outcome of one exact actor request, independently retrievable from its containing step.</summary>
public sealed class WorldActionReceipt
{
    internal WorldActionReceipt(EntityId commandId, EntityId principalId, EntityId worldId, EntityId branchId,
        ContentHash commandHash, ulong commitSequence, CommandReceiptStatus status, WorldActionRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(commandHash);
        if (commitSequence == 0 || !Enum.IsDefined(status) || !Enum.IsDefined(rejection)
            || (status == CommandReceiptStatus.Applied) != (rejection == WorldActionRejection.None))
        {
            throw new ArgumentException("The world action receipt fields are inconsistent.");
        }
        CommandId = commandId;
        PrincipalId = principalId;
        WorldId = worldId;
        BranchId = branchId;
        CommandHash = commandHash;
        CommitSequence = commitSequence;
        Status = status;
        Rejection = rejection;
    }

    public EntityId CommandId { get; }
    public EntityId PrincipalId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ContentHash CommandHash { get; }
    public ulong CommitSequence { get; }
    public CommandReceiptStatus Status { get; }
    public WorldActionRejection Rejection { get; }
    public EntityId ResultId => EntityId.FromBytes(SHA256.HashData(WorldReceiptCodecV1.Encode(this)).AsSpan(0, 16));

    internal static WorldActionReceipt Create(WorldActionCommand command, ulong sequence, WorldActionRejection rejection) =>
        new(command.CommandId, command.PrincipalId, command.WorldId, command.BranchId, command.ContentHash,
            sequence, rejection == WorldActionRejection.None ? CommandReceiptStatus.Applied : CommandReceiptStatus.Rejected,
            rejection);
}

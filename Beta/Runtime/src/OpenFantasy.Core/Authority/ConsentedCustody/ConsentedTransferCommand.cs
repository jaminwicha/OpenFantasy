using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// The sender's immutable submitted claims and exact terms. No identifier here proves authentication,
/// control or ownership, and constructing a command neither offers, reserves nor moves anything.
/// </summary>
public sealed class ConsentedTransferCommand
{
    public const ulong CurrentVersion = 1;

    public ConsentedTransferCommand(EntityId commandId, EntityId principalId, EntityId originSessionId,
        ConsentedTransferTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        CommandId = commandId;
        PrincipalId = principalId;
        OriginSessionId = originSessionId;
        Terms = terms;
    }

    public ulong Version => CurrentVersion;
    public EntityId CommandId { get; }
    public EntityId PrincipalId { get; }
    public EntityId OriginSessionId { get; }
    public ConsentedTransferTerms Terms { get; }

    public CommandContentHash ComputeContentHash() =>
        CommandContentHash.Compute(ConsentedTransferCodecV1.Encode(this));

    /// <summary>
    /// A private input for the shared movement planner, carrying exactly the four approved revisions.
    /// It is never a deliverable ordinary command and receives no ordinary receipt.
    /// </summary>
    internal CommandEnvelope ToMovementRequest() => new(CommandId, PrincipalId, OriginSessionId,
        Terms.SenderActorId, Terms.WorldId, Terms.BranchId,
        [
            new(RevisionTargetKind.Branch, Terms.BranchId, Terms.ExpectedBranchRevision),
            new(RevisionTargetKind.Entity, Terms.LotId, Terms.ExpectedLotRevision),
            new(RevisionTargetKind.Entity, Terms.SourceCustodianId, Terms.ExpectedSourceRevision),
            new(RevisionTargetKind.Entity, Terms.DestinationCustodianId, Terms.ExpectedDestinationRevision)
        ],
        new TransferLotIntent(Terms.LotId, Terms.SourceCustodianId, Terms.DestinationCustodianId, Terms.Quantity));
}

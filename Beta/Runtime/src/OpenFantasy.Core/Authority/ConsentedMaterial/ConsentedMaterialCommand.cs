using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Authority.ConsentedMaterial;

/// <summary>
/// The sender's immutable submitted claims and exact material terms. No identifier here proves
/// authentication, control or ownership, and constructing a command neither offers, reserves nor
/// moves anything.
/// </summary>
public sealed class ConsentedMaterialCommand
{
    public const ulong CurrentVersion = 1;

    public ConsentedMaterialCommand(EntityId commandId, EntityId principalId, EntityId originSessionId,
        ConsentedMaterialTerms terms)
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
    public ConsentedMaterialTerms Terms { get; }

    public CommandContentHash ComputeContentHash() =>
        CommandContentHash.Compute(ConsentedMaterialCodecV1.Encode(this));

    /// <summary>
    /// A private input for the shared raw movement planner. Only the lot revision is approved; the
    /// custody and storage revisions are execution state the executor read from the actual current
    /// owners. Valid terms name three distinct revision owners in one branch, so construction cannot
    /// fail. It is never a deliverable ordinary command and receives no ordinary receipt.
    /// </summary>
    internal CommandEnvelope ToRawMovementRequest(ulong currentCustodyRevision, ulong currentSourceRevision,
        ulong currentDestinationRevision) => new(CommandId, PrincipalId, OriginSessionId,
        Terms.SenderActorId, Terms.WorldId, Terms.BranchId,
        [
            new(RevisionTargetKind.Branch, Terms.BranchId, currentCustodyRevision),
            new(RevisionTargetKind.Entity, Terms.LotId, Terms.ExpectedLotRevision),
            new(RevisionTargetKind.Entity, Terms.SourceCustodianId, currentSourceRevision),
            new(RevisionTargetKind.Entity, Terms.DestinationCustodianId, currentDestinationRevision)
        ],
        new TransferLotIntent(Terms.LotId, Terms.SourceCustodianId, Terms.DestinationCustodianId, Terms.Quantity));
}

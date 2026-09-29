namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// The complete transaction both participants approve: scope, named parties, one raw counted lot,
/// both storages, the amount and the four revisions observed. Terms are compared claims; they grant
/// no control, ownership, spatial reach or elapsed time.
/// </summary>
public sealed record ConsentedTransferTerms
{
    public ConsentedTransferTerms(EntityId worldId, EntityId branchId, EntityId senderActorId,
        EntityId recipientActorId, EntityId lotId, EntityId sourceCustodianId, EntityId destinationCustodianId,
        ulong quantity, ulong expectedBranchRevision, ulong expectedLotRevision,
        ulong expectedSourceRevision, ulong expectedDestinationRevision)
    {
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A consented transfer moves a positive amount.");
        }

        if (senderActorId == recipientActorId)
        {
            throw new ArgumentException("A consented transfer names two different participants.",
                nameof(recipientActorId));
        }

        // Distinct revision owners keep the shared movement request constructible for every valid terms value.
        if (sourceCustodianId == destinationCustodianId || lotId == sourceCustodianId
            || lotId == destinationCustodianId)
        {
            throw new ArgumentException("The lot and both storages must be three different identities.",
                nameof(destinationCustodianId));
        }

        WorldId = worldId;
        BranchId = branchId;
        SenderActorId = senderActorId;
        RecipientActorId = recipientActorId;
        LotId = lotId;
        SourceCustodianId = sourceCustodianId;
        DestinationCustodianId = destinationCustodianId;
        Quantity = quantity;
        ExpectedBranchRevision = expectedBranchRevision;
        ExpectedLotRevision = expectedLotRevision;
        ExpectedSourceRevision = expectedSourceRevision;
        ExpectedDestinationRevision = expectedDestinationRevision;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId SenderActorId { get; }
    public EntityId RecipientActorId { get; }
    public EntityId LotId { get; }
    public EntityId SourceCustodianId { get; }
    public EntityId DestinationCustodianId { get; }

    /// <summary>Indivisible counted units taken from the start of the lot's source interval.</summary>
    public ulong Quantity { get; }
    public ulong ExpectedBranchRevision { get; }
    public ulong ExpectedLotRevision { get; }
    public ulong ExpectedSourceRevision { get; }
    public ulong ExpectedDestinationRevision { get; }
}

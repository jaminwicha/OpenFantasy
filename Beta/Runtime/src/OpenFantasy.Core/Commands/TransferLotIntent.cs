namespace OpenFantasy.Core.Commands;

/// <summary>
/// Requests transfer of indivisible inventory units. It does not reserve, consume or move a lot.
/// Physical measures, splitting rules and custody permission require later rule adjudication.
/// </summary>
public sealed record TransferLotIntent
{
    public TransferLotIntent(EntityId lotId, EntityId sourceCustodianId, EntityId destinationCustodianId,
        ulong quantity)
    {
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Transfer quantity must be positive.");
        }

        if (sourceCustodianId == destinationCustodianId)
        {
            throw new ArgumentException("A transfer requires different source and destination custodians.",
                nameof(destinationCustodianId));
        }

        LotId = lotId;
        SourceCustodianId = sourceCustodianId;
        DestinationCustodianId = destinationCustodianId;
        Quantity = quantity;
    }

    public EntityId LotId { get; }
    public EntityId SourceCustodianId { get; }
    public EntityId DestinationCustodianId { get; }
    public ulong Quantity { get; }
}

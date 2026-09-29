using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// The material facts of one recorded custody change of produced stock, projected from the
/// complete world's ordered consent evidence. It carries no approval and grants nothing; the
/// evidence owner keeps the participants, identities and owner hashes.
/// </summary>
public sealed record ProducedCustodyMovement
{
    public ProducedCustodyMovement(EntityId lotId, ulong lotRevision, EntityId movedLotId,
        EntityId sourceCustodianId, EntityId destinationCustodianId, ulong quantity,
        ProductionOutputInterval? expectedOutput = null, CountedItemDefinition? expectedDefinition = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(quantity);
        if (sourceCustodianId == destinationCustodianId || lotId == sourceCustodianId
            || lotId == destinationCustodianId || movedLotId == sourceCustodianId
            || movedLotId == destinationCustodianId)
        {
            throw new ArgumentException("A custody movement names distinct lots and storages.", nameof(movedLotId));
        }

        LotId = lotId;
        LotRevision = lotRevision;
        MovedLotId = movedLotId;
        SourceCustodianId = sourceCustodianId;
        DestinationCustodianId = destinationCustodianId;
        Quantity = quantity;
        ExpectedOutput = expectedOutput;
        ExpectedDefinition = expectedDefinition;
    }

    /// <summary>The approved lot, which keeps its identity and the unmoved suffix after a split.</summary>
    public EntityId LotId { get; }

    /// <summary>The lot revision the movement spent; the lot then holds the next revision.</summary>
    public ulong LotRevision { get; }

    /// <summary>The approved lot itself for a whole movement, or the child holding the moved prefix.</summary>
    public EntityId MovedLotId { get; }
    public EntityId SourceCustodianId { get; }
    public EntityId DestinationCustodianId { get; }
    public ulong Quantity { get; }
    /// <summary>The recorded producer and exact moved prefix, when supplied by complete-world evidence.</summary>
    public ProductionOutputInterval? ExpectedOutput { get; }
    /// <summary>The item definition and version approved in complete-world evidence.</summary>
    public CountedItemDefinition? ExpectedDefinition { get; }
    public bool IsSplit => MovedLotId != LotId;
}

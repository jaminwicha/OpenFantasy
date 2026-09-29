using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority.ConsentedMaterial;

/// <summary>
/// The complete material transaction both participants approve: scope, named parties, one raw or
/// produced lot with its exact definition pin, both storages, the amount and the approved lot
/// revision. Global production and custody revisions and storage revisions are execution state read
/// from the actual owners when the transaction is decided; they are never part of consent.
/// Terms are compared claims; they grant no control, ownership, spatial reach or elapsed time.
/// </summary>
public sealed record ConsentedMaterialTerms
{
    public ConsentedMaterialTerms(EntityId worldId, EntityId branchId, EntityId senderActorId,
        EntityId recipientActorId, ConsentedMaterialLotKind lotKind, EntityId lotId,
        CountedItemDefinition definition, EntityId sourceCustodianId, EntityId destinationCustodianId,
        ulong quantity, ulong expectedLotRevision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(lotKind))
        {
            throw new ArgumentOutOfRangeException(nameof(lotKind), "A consented transfer names one known lot owner.");
        }

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
        LotKind = lotKind;
        LotId = lotId;
        Definition = definition;
        SourceCustodianId = sourceCustodianId;
        DestinationCustodianId = destinationCustodianId;
        Quantity = quantity;
        ExpectedLotRevision = expectedLotRevision;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId SenderActorId { get; }
    public EntityId RecipientActorId { get; }
    public ConsentedMaterialLotKind LotKind { get; }
    public EntityId LotId { get; }

    /// <summary>The exact item identity and content version the lot must actually hold.</summary>
    public CountedItemDefinition Definition { get; }
    public EntityId SourceCustodianId { get; }
    public EntityId DestinationCustodianId { get; }

    /// <summary>Indivisible counted units taken from the start of the lot's source or output interval.</summary>
    public ulong Quantity { get; }

    /// <summary>
    /// The approved revision of the named lot. Any spending, movement or split of that lot advances
    /// it, so consent to one interval never spends a different surviving remainder.
    /// </summary>
    public ulong ExpectedLotRevision { get; }
}

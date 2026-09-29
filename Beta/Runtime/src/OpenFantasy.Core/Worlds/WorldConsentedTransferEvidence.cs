using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// World-owned evidence of one applied paired material transaction: both participants' submitted
/// claims, the exact moved rights and the content hashes of the inventory and production owners
/// before and after. Those owner hashes never include this evidence, and this evidence never
/// includes a world root or journal publication hash, so the binding has no cycle. The recorded
/// deliveries are historical claims; they never authenticate a current caller.
/// </summary>
public sealed class WorldConsentedTransferEvidence
{
    public const int MaximumByteLength = 4_096;

    internal WorldConsentedTransferEvidence(ulong worldSequence, uint offerActionIndex, uint acceptanceActionIndex,
        ConsentedMaterialApproval approval, EntityId movedLotId, EntityId rightsOwnerId, uint outputIndex,
        ulong movedStart, ulong movedEnd, ContentHash originalInventoryHash, ContentHash originalProductionHash,
        ContentHash candidateInventoryHash, ContentHash candidateProductionHash)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ConsentedMaterialTerms terms = approval.Command.Terms;
        bool split = movedLotId != terms.LotId;
        if (worldSequence == 0 || offerActionIndex == acceptanceActionIndex
            || offerActionIndex >= WorldStepBatch.MaximumActorsPerBatch
            || acceptanceActionIndex >= WorldStepBatch.MaximumActorsPerBatch || !approval.Acceptance.Binds(approval.Command)
            || approval.Command.CommandId == approval.Acceptance.AcceptanceId
            || movedEnd <= movedStart || movedEnd - movedStart != terms.Quantity
            || terms.LotKind == ConsentedMaterialLotKind.CountedSource && outputIndex != 0
            || split && movedLotId != EntityId.DeriveSpawned(approval.Command.CommandId, CustodyTransferPlanner.SplitChildOrdinal)
            || movedLotId == approval.Command.CommandId || movedLotId == approval.Acceptance.AcceptanceId)
        {
            throw new ArgumentException("Consent evidence requires bound approvals, distinct identity roles and the exact moved interval.");
        }
        WorldSequence = worldSequence;
        OfferActionIndex = offerActionIndex;
        AcceptanceActionIndex = acceptanceActionIndex;
        Approval = approval;
        MovedLotId = movedLotId;
        RightsOwnerId = rightsOwnerId;
        OutputIndex = outputIndex;
        MovedStart = movedStart;
        MovedEnd = movedEnd;
        OriginalInventoryHash = originalInventoryHash;
        OriginalProductionHash = originalProductionHash;
        CandidateInventoryHash = candidateInventoryHash;
        CandidateProductionHash = candidateProductionHash;
        ContentHash = ContentHash.Compute(Encode());
    }

    /// <summary>The world revision published by the step that applied the transaction.</summary>
    public ulong WorldSequence { get; }
    public uint OfferActionIndex { get; }
    public uint AcceptanceActionIndex { get; }
    public ConsentedMaterialApproval Approval { get; }
    public EntityId CommandId => Approval.Command.CommandId;
    public EntityId AcceptanceId => Approval.Acceptance.AcceptanceId;

    /// <summary>The whole approved lot, or the child spawned from the offer command for a partial amount.</summary>
    public EntityId MovedLotId { get; }

    /// <summary>The producing batch of produced stock, or the admitted source of raw stock.</summary>
    public EntityId RightsOwnerId { get; }
    public uint OutputIndex { get; }
    public ulong MovedStart { get; }
    public ulong MovedEnd { get; }
    public ContentHash OriginalInventoryHash { get; }
    public ContentHash OriginalProductionHash { get; }
    public ContentHash CandidateInventoryHash { get; }
    public ContentHash CandidateProductionHash { get; }
    public ContentHash ContentHash { get; }

    /// <summary>The material facts production replays; absent for raw stock, which custody owns.</summary>
    public ProducedCustodyMovement? ToProducedMovement()
    {
        ConsentedMaterialTerms terms = Approval.Command.Terms;
        return terms.LotKind != ConsentedMaterialLotKind.Produced ? null
            : new ProducedCustodyMovement(terms.LotId, terms.ExpectedLotRevision, MovedLotId,
                terms.SourceCustodianId, terms.DestinationCustodianId, terms.Quantity,
                new ProductionOutputInterval(RightsOwnerId, OutputIndex, MovedStart, MovedEnd), terms.Definition);
    }

    public byte[] Encode() => CanonicalEncodingV1.Frame("world.consented-transfer.v1",
        CanonicalEncodingV1.Unsigned64(WorldSequence), CanonicalEncodingV1.Unsigned64(OfferActionIndex),
        CanonicalEncodingV1.Unsigned64(AcceptanceActionIndex), ConsentedMaterialCodecV1.Encode(Approval.Command),
        Approval.SenderDelivery.PrincipalId.ToByteArray(), Approval.SenderDelivery.SessionId.ToByteArray(),
        ConsentedMaterialCodecV1.Encode(Approval.Acceptance),
        Approval.RecipientDelivery.PrincipalId.ToByteArray(), Approval.RecipientDelivery.SessionId.ToByteArray(),
        MovedLotId.ToByteArray(), RightsOwnerId.ToByteArray(), CanonicalEncodingV1.Unsigned64(OutputIndex),
        CanonicalEncodingV1.Unsigned64(MovedStart), CanonicalEncodingV1.Unsigned64(MovedEnd),
        OriginalInventoryHash.ToByteArray(), OriginalProductionHash.ToByteArray(),
        CandidateInventoryHash.ToByteArray(), CandidateProductionHash.ToByteArray());
}

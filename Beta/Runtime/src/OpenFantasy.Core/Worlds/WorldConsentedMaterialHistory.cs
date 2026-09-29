using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Bounded ordered consent evidence of one world segment. Order follows the world sequence and the
/// offer's action position; every command, acceptance and spawned child is one distinct permanent
/// identity. It owns no inventory and moves nothing; material owners keep every lot.
/// </summary>
public sealed class WorldConsentedMaterialHistory
{
    public const int MaximumTransfers = 256;
    public const int MaximumByteLength = (WorldConsentedTransferEvidence.MaximumByteLength + sizeof(uint))
        * MaximumTransfers + 128;

    public WorldConsentedMaterialHistory(IEnumerable<WorldConsentedTransferEvidence> transfers)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        var ordered = new List<WorldConsentedTransferEvidence>();
        HashSet<EntityId> identities = [];
        HashSet<(ulong Sequence, uint Index)> positions = [];
        foreach (WorldConsentedTransferEvidence transfer in transfers)
        {
            ArgumentNullException.ThrowIfNull(transfer);
            bool follows = ordered.Count == 0 || transfer.WorldSequence > ordered[^1].WorldSequence
                || transfer.WorldSequence == ordered[^1].WorldSequence
                    && transfer.OfferActionIndex > ordered[^1].OfferActionIndex;
            bool split = transfer.MovedLotId != transfer.Approval.Command.Terms.LotId;
            if (ordered.Count == MaximumTransfers || !follows || !identities.Add(transfer.CommandId)
                || !identities.Add(transfer.AcceptanceId) || split && !identities.Add(transfer.MovedLotId)
                || !positions.Add((transfer.WorldSequence, transfer.OfferActionIndex))
                || !positions.Add((transfer.WorldSequence, transfer.AcceptanceActionIndex)))
            {
                throw new ArgumentException("Consent evidence requires bounded exact order and distinct permanent identities.");
            }
            ordered.Add(transfer);
        }
        ValidateActionOrder(ordered);
        Transfers = ordered.AsReadOnly();
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("world.consented-material-history.v1",
            ordered.Select(value => (ReadOnlyMemory<byte>)value.ContentHash.ToByteArray()).ToArray()));
    }

    public ReadOnlyCollection<WorldConsentedTransferEvidence> Transfers { get; }
    public ContentHash ContentHash { get; }

    /// <summary>The ordered produced-stock movements that production's custody validation replays.</summary>
    public IEnumerable<ProducedCustodyMovement> ProducedMovements =>
        Transfers.Select(value => value.ToProducedMovement()).OfType<ProducedCustodyMovement>();

    public bool ContainsIdentity(EntityId identity) => Transfers.Any(value => value.CommandId == identity
        || value.AcceptanceId == identity || value.MovedLotId == identity);

    public WorldConsentedMaterialHistory Append(WorldConsentedTransferEvidence transfer) => new([.. Transfers, transfer]);

    private static void ValidateActionOrder(IEnumerable<WorldConsentedTransferEvidence> transfers)
    {
        foreach (var batch in transfers.GroupBy(transfer => transfer.WorldSequence))
        {
            var participants = batch.SelectMany(transfer => new[]
            {
                (Index: transfer.OfferActionIndex, Actor: transfer.Approval.Command.Terms.SenderActorId),
                (Index: transfer.AcceptanceActionIndex, Actor: transfer.Approval.Command.Terms.RecipientActorId)
            }).OrderBy(participant => participant.Index).ToArray();
            for (int index = 1; index < participants.Length; index++)
            {
                // Other, possibly refused actions may occupy the gaps. Retained participants
                // must still be a strict subsequence of the batch's unique actor ordering.
                if (participants[index - 1].Actor.CompareTo(participants[index].Actor) >= 0)
                {
                    throw new ArgumentException("Consent evidence action positions must follow unique actor order within each world sequence.");
                }
            }
        }
    }
}

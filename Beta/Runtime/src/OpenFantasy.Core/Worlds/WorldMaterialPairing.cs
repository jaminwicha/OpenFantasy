using OpenFantasy.Core.Authority;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Matches two actual commands in a privileged host-selected batch. A claim embedded in an offer
/// never substitutes for the named recipient's submission. Matching authenticates no transport.
/// </summary>
internal sealed class WorldMaterialPairing
{
    private readonly Dictionary<EntityId, WorldMaterialPair> _offers = [];
    private readonly HashSet<EntityId> _acceptances = [];

    internal WorldMaterialPairing(WorldStepBatch batch)
    {
        var byActor = batch.Actions.Select((command, index) => (Command: command, Index: checked((uint)index)))
            .ToDictionary(value => value.Command.ActorId);
        foreach (var offered in byActor.Values)
        {
            if (offered.Command.Intent is not OfferMaterialIntent terms
                || !byActor.TryGetValue(terms.Terms.RecipientActorId, out var accepted)
                || accepted.Command.Intent is not AcceptMaterialIntent consent
                || consent.OfferCommandId != offered.Command.CommandId)
            {
                continue;
            }
            _offers.Add(offered.Command.CommandId,
                new(offered.Command, offered.Index, accepted.Command, accepted.Index));
            _acceptances.Add(accepted.Command.CommandId);
        }
    }

    internal bool DefersAcceptance(WorldActionCommand command) => _acceptances.Contains(command.CommandId);
    internal WorldMaterialPair? FindOffer(WorldActionCommand command) => _offers.GetValueOrDefault(command.CommandId);

    internal static WorldActionRejection UnmatchedRejection(WorldSnapshot world, WorldActionCommand command)
    {
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        bool isParticipant = command.Intent switch
        {
            OfferMaterialIntent offer => offer.Terms.SenderActorId == command.ActorId,
            AcceptMaterialIntent accept => accept.Terms.RecipientActorId == command.ActorId,
            _ => false
        };
        return isParticipant && world.Inventory.CanControl(delivery, command.ActorId)
            ? WorldActionRejection.Unavailable : WorldActionRejection.NotAuthorized;
    }
}

internal sealed record WorldMaterialPair(WorldActionCommand Offer, uint OfferIndex,
    WorldActionCommand Acceptance, uint AcceptanceIndex)
{
    // Core batches are privileged selected inputs (also used for trusted journal replay). These
    // identities record that selection; a network/offer owner must authenticate both before batching.
    internal TrustedCommandDelivery SenderDelivery => new(Offer.PrincipalId, Offer.OriginSessionId);
    internal TrustedCommandDelivery RecipientDelivery => new(Acceptance.PrincipalId, Acceptance.OriginSessionId);
}

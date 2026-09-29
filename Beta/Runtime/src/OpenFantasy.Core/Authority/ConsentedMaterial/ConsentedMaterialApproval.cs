namespace OpenFantasy.Core.Authority.ConsentedMaterial;

/// <summary>
/// Both submitted approvals, each paired with the identity the host verified for its own delivery.
/// Constructing the value authenticates nothing; neither delivery can stand in for the other and
/// neither is derived from submitted claims.
/// </summary>
public sealed class ConsentedMaterialApproval
{
    public ConsentedMaterialApproval(ConsentedMaterialCommand command, TrustedCommandDelivery senderDelivery,
        ConsentedMaterialAcceptance acceptance, TrustedCommandDelivery recipientDelivery)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(senderDelivery);
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentNullException.ThrowIfNull(recipientDelivery);
        Command = command;
        SenderDelivery = senderDelivery;
        Acceptance = acceptance;
        RecipientDelivery = recipientDelivery;
    }

    public ConsentedMaterialCommand Command { get; }
    public TrustedCommandDelivery SenderDelivery { get; }
    public ConsentedMaterialAcceptance Acceptance { get; }
    public TrustedCommandDelivery RecipientDelivery { get; }

    /// <summary>
    /// Checks the supplied current state: the acceptance binds this command, each participant's claims
    /// equal their own verified delivery and current control, and each owns the named storage.
    /// Deposit policies are neither consulted nor changed.
    /// </summary>
    public bool IsApprovedByBothParticipants(CountedAuthorityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ConsentedMaterialTerms terms = Command.Terms;
        return terms.WorldId == state.WorldId && terms.BranchId == state.BranchId
            && Acceptance.Binds(Command)
            && IsCurrentController(state, SenderDelivery, Command.PrincipalId, Command.OriginSessionId,
                terms.SenderActorId)
            && IsCurrentController(state, RecipientDelivery, Acceptance.PrincipalId, Acceptance.OriginSessionId,
                terms.RecipientActorId)
            && IsOwner(state, terms.SenderActorId, terms.SourceCustodianId)
            && IsOwner(state, terms.RecipientActorId, terms.DestinationCustodianId);
    }

    // Submitted claims must equal the host-verified delivery; neither is derived from the other.
    private static bool IsCurrentController(CountedAuthorityState state, TrustedCommandDelivery delivery,
        EntityId claimedPrincipalId, EntityId claimedSessionId, EntityId actorId) =>
        claimedPrincipalId == delivery.PrincipalId && claimedSessionId == delivery.SessionId
        && state.CanControl(delivery, actorId);

    private static bool IsOwner(CountedAuthorityState state, EntityId actorId, EntityId custodianId) =>
        state.Policies.Any(policy => policy.CustodianId == custodianId && policy.OwnerActorId == actorId);
}

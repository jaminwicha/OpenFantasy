namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// A submitted recipient acceptance paired with the identity the host verified for its own
/// delivery. The sender's delivery can never stand in for this one.
/// </summary>
public sealed class DeliveredTransferAcceptance
{
    public DeliveredTransferAcceptance(ConsentedTransferAcceptance acceptance, TrustedCommandDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentNullException.ThrowIfNull(delivery);
        Acceptance = acceptance;
        Delivery = delivery;
    }

    public ConsentedTransferAcceptance Acceptance { get; }
    public TrustedCommandDelivery Delivery { get; }
}

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// A submitted sender command paired with the identity the host verified for the delivery that
/// carried it. Constructing the pair authenticates nothing and must not copy submitted claims.
/// </summary>
public sealed class DeliveredTransferCommand
{
    public DeliveredTransferCommand(ConsentedTransferCommand command, TrustedCommandDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        Command = command;
        Delivery = delivery;
    }

    public ConsentedTransferCommand Command { get; }
    public TrustedCommandDelivery Delivery { get; }
}

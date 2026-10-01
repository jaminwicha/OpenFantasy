using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>
    /// Accepts one paid spot-exchange submission with its own independently supplied delivery. Under one
    /// lock it authorizes that delivery against the current world, returns the original receipt of an exact
    /// retry without another transfer, and otherwise selects and publishes the single-command advancing step.
    /// Inventory, production, balances, quotes, receipts and identities publish in that one SQL transaction.
    /// </summary>
    public WorldActionReceipt SubmitPaidExchangeCommand(WorldActionCommand command, TrustedCommandDelivery delivery,
        EntityId causeId) => SubmitPaidExchangeCommand(command, delivery, causeId, null);

    internal WorldActionReceipt SubmitPaidExchangeCommand(WorldActionCommand command, TrustedCommandDelivery delivery,
        EntityId causeId, Action<AuthorityCommitBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (_state.ProfileVersion != WorldPaidExchangeAdmission.ProfileVersion || command.Intent is not PaidExchangeIntent)
            {
                throw new AuthorityDispatchException("This boundary accepts paid exchange submissions of their own world profile.");
            }
            // Authorization precedes every disclosure, including whether an earlier outcome exists.
            WorldActionReceipt? prior = FindReceipt(command, delivery);
            if (prior is not null)
            {
                return prior;
            }
            WorldStepBatch batch = SelectMaterialProposalBatch(_state, command, causeId);
            WorldStepDecision decision = Commit(batch, fault);
            return decision.Receipts.Single(receipt => receipt.CommandId == command.CommandId);
        }
    }
}

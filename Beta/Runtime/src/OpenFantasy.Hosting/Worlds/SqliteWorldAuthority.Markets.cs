using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Observations;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>
    /// Accepts one local-market submission (rule-priced issuance or in-person observation) with its own
    /// independently supplied delivery. Under one lock it authorizes that delivery against the current world,
    /// returns the original receipt of an exact retry without another effect, and otherwise selects and
    /// publishes the single-command advancing step in one SQL transaction.
    /// </summary>
    public WorldActionReceipt SubmitMarketCommand(WorldActionCommand command, TrustedCommandDelivery delivery, EntityId causeId)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (_state.ProfileVersion != WorldMarketAdmission.ProfileVersion || command.Intent is not MarketIntent)
            {
                throw new AuthorityDispatchException("This boundary accepts market submissions of their own world profile.");
            }
            // Authorization precedes every disclosure, including whether an earlier outcome exists.
            WorldActionReceipt? prior = FindReceipt(command, delivery);
            if (prior is not null)
            {
                return prior;
            }
            WorldStepBatch batch = SelectMaterialProposalBatch(_state, command, causeId);
            WorldStepDecision decision = Commit(batch, null);
            return decision.Receipts.Single(receipt => receipt.CommandId == command.CommandId);
        }
    }

    /// <summary>
    /// The dated price observations one actor holds, read for that actor's current controller only. The
    /// delivery is authorized before anything is read, so a foreign caller learns neither the memory nor
    /// whether it exists. Reading changes nothing and never refreshes or extends an observation.
    /// </summary>
    public IReadOnlyList<MarketPriceObservation> ReadMarketKnowledge(EntityId actorId, TrustedCommandDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (_state.Coordination?.Markets is not { } markets || !_state.Inventory.CanControl(delivery, actorId))
            {
                throw new AuthorityDispatchException("Price knowledge is readable only by its holder's current controller.");
            }
            return markets.KnowledgeOf(actorId).ToList().AsReadOnly();
        }
    }
}

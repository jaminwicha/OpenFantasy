using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins physical storage and outstanding receiving obligations to the complete world material candidate.</summary>
internal static class WorldReservoirBinding
{
    internal static void Validate(LiquidReservoirState reservoirs, CountedAuthorityState inventory, ProductionSnapshot? production)
    {
        if (production is null || !Matches(reservoirs, inventory, production))
        {
            throw new ArgumentException("Reservoir ownership requires the world's exact inventory and Production state.");
        }
    }

    internal static bool TryReconcile(LiquidReservoirState? current, CountedAuthorityState inventory,
        ProductionSnapshot? production, out LiquidReservoirState? candidate,
        WorldConsentedTransferEvidence? transfer = null, WorldCustodyWitness? settlement = null)
    {
        candidate = current;
        if (current is null || Matches(current, inventory, production))
        {
            return true;
        }
        try
        {
            // A saved proposal's settlement supplies the same owner hashes as paired evidence.
            WorldCustodyWitness? witness = settlement ?? (transfer is null ? null : new WorldCustodyWitness(
                transfer.Approval.Command.Terms.LotKind == Authority.ConsentedMaterial.ConsentedMaterialLotKind.Produced,
                transfer.OriginalInventoryHash, transfer.OriginalProductionHash, transfer.CandidateInventoryHash,
                transfer.CandidateProductionHash));
            bool producedTransfer = witness?.Produced == true;
            if (producedTransfer && (witness!.Value.OriginalInventoryHash != current.Capacity.Inventory.ContentHash
                || witness.Value.OriginalProductionHash != current.Capacity.Production?.ContentHash
                || witness.Value.CandidateInventoryHash != inventory.ContentHash
                || witness.Value.CandidateProductionHash != production?.ContentHash))
            {
                return false;
            }
            var capacity = producedTransfer
                ? current.Capacity.PlanReconcileCustody(inventory, production!)
                : current.Capacity.PlanReconcile(inventory, production);
            candidate = current.PlanApply(capacity).ApplyTo(current);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static bool Matches(LiquidReservoirState state, CountedAuthorityState inventory, ProductionSnapshot? production) =>
        (ReferenceEquals(state.Capacity.Inventory, inventory) || state.Capacity.Inventory.ContentHash == inventory.ContentHash)
        && (ReferenceEquals(state.Capacity.Production, production) || state.Capacity.Production?.ContentHash == production?.ContentHash);
}

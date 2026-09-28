using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded qualification of the already admitted fixed domain at one exact material cut.</summary>
internal sealed class RunoffMaterialContinuation
{
    private RunoffMaterialContinuation(ContentHash domainHash, ContentHash contextHash)
    {
        DomainHash = domainHash;
        ContextHash = contextHash;
    }

    private ContentHash DomainHash { get; }
    private ContentHash ContextHash { get; }

    internal static RunoffMaterialContinuation Capture(WatershedRunoffCapacityState closing, ProductionSnapshot next)
    {
        if (next.Continuation is not { } context
            || context.Basis.InventoryHash != closing.Inventory.ContentHash
            || context.Basis.ProductionHash != closing.Production?.ContentHash)
        {
            throw new ArgumentException("Runoff continuation must derive from the actual admitted closing owners.");
        }
        return new(closing.Domain.ContentHash, context.ContentHash);
    }

    internal void Validate(WatershedRunoffDomain domain, ProductionSnapshot production)
    {
        if (domain.ContentHash != DomainHash || production.Continuation?.ContentHash != ContextHash)
        {
            throw new ArgumentException("Continued runoff requires its qualified fixed domain and exact production context.");
        }
    }
}

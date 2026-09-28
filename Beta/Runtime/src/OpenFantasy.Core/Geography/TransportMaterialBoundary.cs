using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded observation of the complete actual closing transport owner; never a second material authority.</summary>
public sealed class TransportMaterialBoundary
{
    private TransportMaterialBoundary(ReservoirTransportState owner)
    {
        ClosingTransportHash = owner.ContentHash;
        CatalogHash = Catalog(owner);
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("geography.transport-material-boundary.v1",
            ClosingTransportHash.ToByteArray(), CatalogHash.ToByteArray()));
    }

    public ContentHash ClosingTransportHash { get; }
    public ContentHash CatalogHash { get; }
    public ContentHash ContentHash { get; }

    internal static TransportMaterialBoundary Capture(ReservoirTransportState owner) => new(owner);

    internal static ContentHash Catalog(ReservoirTransportState owner) => ContentHash.Compute(CanonicalEncodingV1.Frame(
        "geography.transport-material-catalog.v1", owner.Reservoirs.Capacity.Domain.ContentHash.ToByteArray(),
        CanonicalEncodingV1.Frame("geography.transport-material-geometries.v1", owner.Reservoirs.Geometries
            .Select(value => (ReadOnlyMemory<byte>)value.ContentHash.ToByteArray()).ToArray()),
        ReservoirTransportCodecV1.EncodeConnections(owner.Connections)));
}

/// <summary>Qualification minted only when the complete admitted transport owner closes its material segment.</summary>
internal sealed class TransportMaterialContinuation
{
    private readonly ContentHash _contextHash;
    private readonly ContentHash _catalogHash;

    private TransportMaterialContinuation(ContentHash contextHash, ContentHash catalogHash)
    {
        _contextHash = contextHash;
        _catalogHash = catalogHash;
    }

    internal static TransportMaterialContinuation Capture(ReservoirTransportState closing, Production.ProductionSnapshot production)
    {
        var context = production.Continuation;
        if (context?.ReservedTransport is not { } boundary || boundary.ClosingTransportHash != closing.ContentHash
            || context.Basis.InventoryHash != closing.Reservoirs.Capacity.Inventory.ContentHash
            || context.Basis.ProductionHash != closing.Reservoirs.Capacity.Production?.ContentHash)
            throw new ArgumentException("Reserved transport continuation requires the actual complete closing owner.");
        return new(context.ContentHash, boundary.CatalogHash);
    }

    internal void Validate(ReservoirTransportState owner)
    {
        if (owner.Reservoirs.Capacity.Production?.Continuation?.ContentHash != _contextHash
            || TransportMaterialBoundary.Catalog(owner) != _catalogHash)
            throw new ArgumentException("Continued transport requires its exact fixed catalog and qualified material context.");
    }
}

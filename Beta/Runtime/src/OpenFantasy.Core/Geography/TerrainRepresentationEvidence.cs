using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Opaque actual representation result, retaining bounded canonical selections and exact result digests without parent owners.</summary>
public sealed class TerrainRepresentationEvidence
{
    internal TerrainRepresentationEvidence(TerrainMaterialRegistry before, TerrainRepresentationBoundary binding,
        TerrainMaterialPartitionResult result, EntityId allocationId, EntityId causeId, bool refining,
        CellCoordinate[] cells, ContentHash? windowHash)
    {
        AllocationId = allocationId;
        CauseId = causeId;
        Refining = refining;
        Cells = Array.AsReadOnly(cells.ToArray());
        CreatedIds = Array.AsReadOnly(result.CreatedIds.ToArray());
        CatalogHash = binding.CatalogHash;
        BeforeRepresentationHash = binding.ContentHash;
        AfterRepresentationHash = binding.Change(allocationId, result.ActiveCells).ContentHash;
        BeforeInventoryHash = before.Inventory.ContentHash;
        BeforeProductionHash = before.Production!.ContentHash;
        AfterInventoryHash = result.Inventory.ContentHash;
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("terrain.representation-evidence.v1",
            AllocationId.ToByteArray(), CauseId.ToByteArray(), CanonicalEncodingV1.Unsigned64(refining ? 1UL : 0UL),
            TerrainRepresentationBoundary.EncodeCells(Cells), CatalogHash.ToByteArray(), BeforeRepresentationHash.ToByteArray(),
            AfterRepresentationHash.ToByteArray(), BeforeInventoryHash.ToByteArray(), BeforeProductionHash.ToByteArray(),
            AfterInventoryHash.ToByteArray(), windowHash?.ToByteArray() ?? Array.Empty<byte>(),
            CanonicalEncodingV1.Frame("terrain.representation-created.v1", CreatedIds.Select(value => (ReadOnlyMemory<byte>)value.ToByteArray()).ToArray())));
    }

    public EntityId AllocationId { get; }
    public EntityId CauseId { get; }
    public bool Refining { get; }
    public ReadOnlyCollection<CellCoordinate> Cells { get; }
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public ContentHash CatalogHash { get; }
    public ContentHash BeforeRepresentationHash { get; }
    public ContentHash AfterRepresentationHash { get; }
    public ContentHash BeforeInventoryHash { get; }
    public ContentHash BeforeProductionHash { get; }
    public ContentHash AfterInventoryHash { get; }
    public ContentHash ContentHash { get; }
}
